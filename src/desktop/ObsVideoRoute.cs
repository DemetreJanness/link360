using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Link360Desktop;

internal interface IObsRpc : IAsyncDisposable {
 Task<JsonElement> Call(string type,object? data,CancellationToken cancel);
}

internal sealed class LocalObsRpc : IObsRpc {
 readonly SemaphoreSlim gate=new(1);
 ClientWebSocket? socket;
 static string Hash(string text)=>Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
 async Task<JsonElement> Receive(CancellationToken cancel) {
  using var message=new MemoryStream();var buffer=new byte[16384];
  while(true) {
   var part=await socket!.ReceiveAsync(buffer,cancel);
   if(part.MessageType==WebSocketMessageType.Close)throw new IOException("OBS disconnected.");
   if(part.MessageType!=WebSocketMessageType.Text)throw new IOException("Unexpected OBS message.");
   message.Write(buffer,0,part.Count);
   if(message.Length>2_000_000)throw new IOException("OBS response exceeded local limits.");
   if(part.EndOfMessage)break;
  }
  using var parsed=JsonDocument.Parse(message.ToArray());return parsed.RootElement.Clone();
 }
 Task Send(object packet,CancellationToken cancel)=>socket!.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(packet)),WebSocketMessageType.Text,true,cancel);
 async Task Connect(CancellationToken cancel) {
  var configFile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"obs-studio/plugin_config/obs-websocket/config.json");
  using var config=JsonDocument.Parse(File.ReadAllText(configFile));
  int port=config.RootElement.GetProperty("server_port").GetInt32();if(port<1||port>65535)throw new IOException("Invalid local OBS port.");
  string password=config.RootElement.TryGetProperty("server_password",out var p)?p.GetString()??"":"";
  socket?.Dispose();socket=new ClientWebSocket();socket.Options.Proxy=null;
  await socket.ConnectAsync(new Uri($"ws://127.0.0.1:{port}"),cancel);
  var hello=await Receive(cancel);if(hello.GetProperty("op").GetInt32()!=0)throw new IOException("Unexpected OBS handshake.");
  var identify=new Dictionary<string,object>{{"rpcVersion",1},{"eventSubscriptions",0}};
  if(hello.GetProperty("d").TryGetProperty("authentication",out var auth))
   identify["authentication"]=Hash(Hash(password+auth.GetProperty("salt").GetString())+auth.GetProperty("challenge").GetString());
  await Send(new{op=1,d=identify},cancel);
  if((await Receive(cancel)).GetProperty("op").GetInt32()!=2)throw new IOException("OBS authentication failed.");
 }
 public async Task<JsonElement> Call(string type,object? data,CancellationToken cancel) {
  await gate.WaitAsync(cancel);using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancel);deadline.CancelAfter(5000);
  try {
   if(socket?.State!=WebSocketState.Open)await Connect(deadline.Token);
   string id=Guid.NewGuid().ToString("N");
   await Send(new{op=6,d=new{requestType=type,requestId=id,requestData=data??new{}}},deadline.Token);
   while(true) {
    var packet=await Receive(deadline.Token);if(packet.GetProperty("op").GetInt32()!=7)continue;
    var response=packet.GetProperty("d");if(response.GetProperty("requestId").GetString()!=id)continue;
    if(!response.GetProperty("requestStatus").GetProperty("result").GetBoolean())throw new IOException($"OBS rejected {type}.");
    return response.TryGetProperty("responseData",out var value)?value.Clone():JsonSerializer.SerializeToElement(new{});
   }
  }catch{socket?.Dispose();socket=null;throw;}finally{gate.Release();}
 }
 public ValueTask DisposeAsync(){socket?.Dispose();socket=null;return ValueTask.CompletedTask;}
}

internal sealed class ObsVideoRoute : IAsyncDisposable {
 const string Source="Main Camera - SELECT INSTA360 LINK";
 readonly IObsRpc rpc;
 readonly Func<DateTime> now;
 readonly SemaphoreSlim gate=new(1);
 string? lastHash;
 int repeats,restarts;
 DateTime lastRestart=DateTime.MinValue;
 bool disconnected;
 public ObsVideoRoute(IObsRpc rpc,Func<DateTime>? now=null){this.rpc=rpc;this.now=now??(()=>DateTime.UtcNow);}
 public void DeviceChanged(){disconnected=true;lastHash=null;repeats=0;restarts=0;}
 public async Task<byte[]> TrackingFrame(CancellationToken ct,bool allowHidden=false) {
  await gate.WaitAsync(ct);try {
   if(!await SourceEnabled(ct))throw new IOException("OBS Link source is disabled.");
   var active=await rpc.Call("GetSourceActive",new{sourceName=Source},ct);
   if(!allowHidden&&!active.GetProperty("videoShowing").GetBoolean())throw new IOException("OBS Link source is hidden.");
   var frame=await rpc.Call("GetSourceScreenshot",new{sourceName=Source,imageFormat="jpg",imageWidth=320,imageHeight=180,imageCompressionQuality=80},ct);
   var raw=frame.GetProperty("imageData").GetString()??"";
   if(!raw.StartsWith("data:image/jpg;base64,",StringComparison.Ordinal)&&!raw.StartsWith("data:image/jpeg;base64,",StringComparison.Ordinal))throw new IOException("Unexpected local tracking frame.");
   return Convert.FromBase64String(raw[(raw.IndexOf(',')+1)..]);
  }finally{gate.Release();}
 }
 async Task<bool> SourceEnabled(CancellationToken ct) {
  var settings=await rpc.Call("GetInputSettings",new{inputName=Source},ct);
  if(settings.GetProperty("inputKind").GetString()!="dshow_input")throw new IOException("Expected an OBS Windows camera source.");
  var input=settings.GetProperty("inputSettings");
  if(!input.TryGetProperty("video_device_id",out var device)||!device.GetString()!.Contains("vid_2e1a&pid_4c01",StringComparison.OrdinalIgnoreCase))
   throw new IOException("OBS source is not configured for the verified Insta360 Link.");
  return !input.TryGetProperty("active",out var active)||active.GetBoolean();
 }
 async Task Restart(CancellationToken ct) {
  // Installed OBS advertises PressInputPropertiesButton; 'activate' is the
  // documented win-dshow property button, verified on this installation.
  if(await SourceEnabled(ct)) {
   await rpc.Call("PressInputPropertiesButton",new{inputName=Source,propertyName="activate"},ct);
   await Task.Delay(300,ct);
  }
  // Re-read the flag: never accidentally leave an already-disabled source off.
  if(!await SourceEnabled(ct))await rpc.Call("PressInputPropertiesButton",new{inputName=Source,propertyName="activate"},ct);
  lastRestart=now();restarts++;repeats=0;lastHash=null;disconnected=false;
 }
 public async Task StartShared(CancellationToken ct) {
  await gate.WaitAsync(ct);try {
   if(!await SourceEnabled(ct))throw new IOException("Activate the Link source in OBS first.");
   var status=await rpc.Call("GetVirtualCamStatus",null,ct);
   if(!status.GetProperty("outputActive").GetBoolean())await rpc.Call("StartVirtualCam",null,ct);
  }finally{gate.Release();}
 }
 public async Task Repair(CancellationToken ct){await gate.WaitAsync(ct);try{await Restart(ct);}finally{gate.Release();}}
 public async Task<string> Check(bool cameraConnected,CancellationToken ct) {
  await gate.WaitAsync(ct);try {
   if(!cameraConnected){disconnected=true;return "Camera disconnected · pending commands discarded";}
   if(!await SourceEnabled(ct))return "OBS camera source disabled · activation remains manual";
   var visibility=await rpc.Call("GetSourceActive",new{sourceName=Source},ct);
   if(!visibility.GetProperty("videoShowing").GetBoolean())return "OBS Link source is hidden · capture recovery paused";
   if(disconnected&&now()-lastRestart>TimeSpan.FromSeconds(10)&&restarts<3){await Restart(ct);return "OBS capture reactivated after device change";}
   var image=await rpc.Call("GetSourceScreenshot",new{sourceName=Source,imageFormat="png",imageWidth=160,imageHeight=90},ct);
   string raw=image.GetProperty("imageData").GetString()??"";
   if(!raw.StartsWith("data:image/png;base64,",StringComparison.Ordinal))throw new IOException("Unexpected local frame response.");
   string hash=Convert.ToHexString(SHA256.HashData(Convert.FromBase64String(raw[22..])));
   bool hadFrame=lastHash!=null,changed=hadFrame&&hash!=lastHash;
   repeats=hash==lastHash?repeats+1:0;lastHash=hash;
   var output=await rpc.Call("GetVirtualCamStatus",null,ct);
   bool shared=output.GetProperty("outputActive").GetBoolean();
   if(repeats>=3) {
    if(restarts<3&&now()-lastRestart>TimeSpan.FromSeconds(10)){await Restart(ct);return "Stalled OBS frames · capture reactivated";}
    return "OBS frames unchanged · repair required (bounded retries)";
   }
   if(changed)restarts=0;
   if(!hadFrame)return "Checking OBS frame freshness";
   return shared?"Fresh OBS frames · Virtual Camera running":"Fresh OBS frames · Start shared camera for Discord";
  }finally{gate.Release();}
 }
 public ValueTask DisposeAsync()=>rpc.DisposeAsync();
}
