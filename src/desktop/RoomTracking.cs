using System.Diagnostics;
using System.Text.Json;
using Link360;
namespace Link360Desktop;

internal record RoomPoint(double X,double Y,int Pan,int Tilt);
internal sealed class LocalBodySession : IAsyncDisposable {
 readonly Process process;
 readonly CancellationTokenRegistration stop;
 public LocalBodySession(CancellationToken ct,bool pose=false,bool wholeBody=false,bool torso=false) {
  process=Process.Start(new ProcessStartInfo(Environment.ProcessPath!,"--body-worker --gpu"+(pose?" --pose":"")+(torso?" --torso":"")+(pose||wholeBody||torso?" --whole-body":"")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true})??throw new IOException("Cannot start GPU body worker.");
  stop=ct.Register(Kill);
 }
 void Kill(){try{if(!process.HasExited)process.Kill(true);}catch(InvalidOperationException){}}
 public async Task<BodyResult> Infer(byte[] frame,CancellationToken ct,double? minimumScore=null) {
  using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(15000);
  try {
   string request=(minimumScore??TrackingConfidence.Minimum).ToString(System.Globalization.CultureInfo.InvariantCulture)+"|"+Convert.ToBase64String(frame);
   await process.StandardInput.WriteLineAsync(request.AsMemory(),deadline.Token).ConfigureAwait(false);
   var line=await process.StandardOutput.ReadLineAsync(deadline.Token).ConfigureAwait(false)??throw new IOException("GPU body worker exited.");ct.ThrowIfCancellationRequested();
   var result=JsonSerializer.Deserialize<BodyResult>(line,Program.Json)??throw new IOException("Invalid body inference.");if(result.Error!=null)throw new IOException(result.Error);return result;
  }catch{Kill();throw;}
 }
 public async ValueTask DisposeAsync(){stop.Dispose();Kill();await process.WaitForExitAsync().ConfigureAwait(false);process.Dispose();}
}

internal sealed class OverviewFrames : IAsyncDisposable {
 const string Source="Backup - Acer Camera";
 readonly IObsRpc rpc=new LocalObsRpc();
 string? device;
 public async Task<byte[]> Read(CancellationToken ct) {
  var settings=await rpc.Call("GetInputSettings",new{inputName=Source},ct);var input=settings.GetProperty("inputSettings");
  string id=input.GetProperty("video_device_id").GetString()??"";
  if(settings.GetProperty("inputKind").GetString()!="dshow_input"||!id.StartsWith("Acer FHD User Facing:",StringComparison.Ordinal))throw new IOException("Overview source is not the verified Acer camera.");
  if(device!=null&&id!=device)throw new IOException("Overview camera changed; recalibrate before room tracking.");device=id;
  if(input.TryGetProperty("active",out var active)&&!active.GetBoolean())throw new IOException("Acer source disabled; enable capture in OBS first.");
  var frame=await rpc.Call("GetSourceScreenshot",new{sourceName=Source,imageFormat="jpg",imageWidth=640,imageHeight=360,imageCompressionQuality=80},ct);
  var raw=frame.GetProperty("imageData").GetString()??"";
  if(!raw.StartsWith("data:image/jpg;base64,",StringComparison.Ordinal)&&!raw.StartsWith("data:image/jpeg;base64,",StringComparison.Ordinal))throw new IOException("Unexpected overview frame.");
  return Convert.FromBase64String(raw[(raw.IndexOf(',')+1)..]);
 }
 public ValueTask DisposeAsync()=>rpc.DisposeAsync();
}
