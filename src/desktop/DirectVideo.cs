using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text.Json;
using OpenCvSharp;
using Link360;

namespace Link360 {
 public record VideoDevice(int Index,string Name,string Path);
 public static partial class CameraProbe {
  public static List<VideoDevice> VideoDevices() {
   var devices=new List<VideoDevice>();object? dev=null;IEnumMoniker? enumerator=null;
   try {
    dev=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("62BE5D10-60EB-11D0-BD3B-00A0C911CE86"))!);
    var category=new Guid("860BB310-5D01-11D0-BD3B-00A0C911CE86");int hr=((ICreateDevEnum)dev!).CreateClassEnumerator(ref category,out enumerator!,0);
    if(hr==1)return devices;Marshal.ThrowExceptionForHR(hr);var monikers=new IMoniker[1];int index=0;
    while(enumerator!.Next(1,monikers,IntPtr.Zero)==0) {
     object? bag=null;try{var id=typeof(IPropertyBag).GUID;monikers[0].BindToStorage(null!,null!,ref id,out bag);Marshal.ThrowExceptionForHR(((IPropertyBag)bag).Read("FriendlyName",out var name,IntPtr.Zero));string path;monikers[0].GetDisplayName(null!,null!,out path);devices.Add(new(index,Convert.ToString(name)??"",path));}
     finally{Release(bag!);Release(monikers[0]);index++;}
    }
   }finally{Release(enumerator!);Release(dev!);}return devices;
  }
 }
}
namespace Link360Desktop {
 internal record VideoPacket(string? Jpeg=null,long Sequence=0,string? Error=null,long ReadAtUtcMilliseconds=0);
 internal record FeedFrame(byte[] Bytes,DateTime? ReadAtUtc,DateTime ReceivedUtc);
 internal record CaptureDetail(int Width,int Height,int JpegQuality) {
  public static CaptureDetail For(string role,bool scene)=>new(role=="link"&&!scene?320:640,role=="link"&&!scene?180:360,scene?92:80);
 }
 internal static class VideoWorker {
  public static void Run(string role,bool sceneDetail=false) {
   var wire=new JsonSerializerOptions();try {
    string name=role=="room"?"Acer FHD User Facing":role=="link"?"Insta360 Link":throw new IOException("Unknown camera role.");
    var matches=CameraProbe.VideoDevices().Where(d=>d.Name.Equals(name,StringComparison.OrdinalIgnoreCase)).ToArray();
    if(matches.Length!=1)throw new IOException($"Connect exactly one {name} camera.");var device=matches[0];
    bool mediaFoundation=false;var capture=new VideoCapture(device.Index,VideoCaptureAPIs.DSHOW);
    if(!capture.IsOpened()){
     capture.Dispose();var alternate=MediaFoundationDevices.Match(device);capture=new VideoCapture(alternate.Index,VideoCaptureAPIs.MSMF);mediaFoundation=true;
     if(!capture.IsOpened()){capture.Dispose();throw new IOException($"Cannot open {name} with DirectShow or Media Foundation. Check other camera clients or reconnect the camera.");}
     if(MediaFoundationDevices.Match(device).Path!=alternate.Path){capture.Dispose();throw new IOException("Camera inventory changed during fallback capture.");}
    }
    // Keep MF's negotiated native format; changing width/height separately can
    // leave this Acer driver with a selected format that produces no samples.
    if(!mediaFoundation){capture.Set(VideoCaptureProperties.FrameWidth,640);capture.Set(VideoCaptureProperties.FrameHeight,360);capture.Set(VideoCaptureProperties.Fps,30);}
    var current=CameraProbe.VideoDevices().SingleOrDefault(d=>d.Index==device.Index);
    if(current?.Path!=device.Path)throw new IOException("Camera inventory changed while opening preview.");
    object gate=new();byte[]? latest=null;long sequence=0,readAt=0;string? error=null;using var stop=new CancellationTokenSource();
    var detail=CaptureDetail.For(role,sceneDetail);
    var reader=Task.Run(()=>{try{using var mat=new Mat();while(!stop.IsCancellationRequested){if(!capture.Read(mat)||mat.Empty())throw new IOException("Camera frames stopped; restart preview after reconnect.");long returned=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();using var resized=new Mat();Cv2.Resize(mat,resized,new OpenCvSharp.Size(detail.Width,detail.Height));var bytes=resized.ToBytes(".jpg",new ImageEncodingParam(ImwriteFlags.JpegQuality,detail.JpegQuality));lock(gate){latest=bytes;readAt=returned;sequence++;}}}catch(Exception e){lock(gate)error=e.Message;}});
    try{long delivered=0;string? line;while((line=Console.ReadLine())!=null){var clock=Stopwatch.StartNew();byte[]? frame=null;long number=0,stamp=0;while(clock.ElapsedMilliseconds<4000){lock(gate){if(error!=null)throw new IOException(error);frame=latest;number=sequence;stamp=readAt;}if(frame!=null&&number!=delivered)break;Thread.Sleep(2);}if(frame==null||number==delivered)throw new IOException("Camera returned no new frames; restart capture after checking other clients.");delivered=number;Console.WriteLine(JsonSerializer.Serialize(new VideoPacket(Convert.ToBase64String(frame),number,ReadAtUtcMilliseconds:stamp),wire));}}
    finally{stop.Cancel();if(reader.Wait(500))capture.Dispose();/* Process teardown releases a blocked driver; never dispose while Read is running. */}
   }catch(Exception e){Console.WriteLine(JsonSerializer.Serialize(new VideoPacket(Error:e.Message),wire));Environment.ExitCode=1;}
  }
 }
 internal sealed class DirectFrames : IAsyncDisposable {
  readonly string role;readonly bool sceneDetail;readonly string executable;readonly SemaphoreSlim gate=new(1);Process? process;string? identity;long lastSequence;DateTime changed;
  public DirectFrames(string role,bool sceneDetail=false,string? executable=null){this.role=role;this.sceneDetail=sceneDetail;this.executable=executable??Environment.ProcessPath!;}
  void Kill(){try{if(process is {HasExited:false})process.Kill(true);}catch{} }
  public async Task<FeedFrame> Read(CancellationToken ct) {
   await gate.WaitAsync(ct);using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(7000);using var cancellation=deadline.Token.Register(Kill);
   try {
    if(process==null){var matches=CameraProbe.VideoDevices().Where(d=>d.Name==(role=="room"?"Acer FHD User Facing":"Insta360 Link")).ToArray();if(matches.Length!=1)throw new IOException("Expected one camera for direct preview.");if(identity!=null&&identity!=matches[0].Path)throw new IOException("Camera changed; reopen preview and recalibrate.");identity=matches[0].Path;
     var start=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true};start.ArgumentList.Add("--video-worker");if(sceneDetail)start.ArgumentList.Add("--scene-detail");start.ArgumentList.Add(role);process=Process.Start(start)??throw new IOException("Cannot start local capture worker.");lastSequence=0;changed=DateTime.UtcNow;}
    await process.StandardInput.WriteLineAsync("frame".AsMemory(),deadline.Token).ConfigureAwait(false);
    string line=await process.StandardOutput.ReadLineAsync(deadline.Token).ConfigureAwait(false)??throw new IOException("Local camera worker exited.");
    var packet=JsonSerializer.Deserialize<VideoPacket>(line)??throw new IOException("Invalid camera frame.");if(packet.Error!=null)throw new IOException(packet.Error);
    if(packet.Sequence!=lastSequence){lastSequence=packet.Sequence;changed=DateTime.UtcNow;}if(DateTime.UtcNow-changed>TimeSpan.FromSeconds(2))throw new IOException("Direct camera frames stopped; restart preview.");
    return new(Convert.FromBase64String(packet.Jpeg??throw new IOException("Missing camera image.")),packet.ReadAtUtcMilliseconds>0?DateTimeOffset.FromUnixTimeMilliseconds(packet.ReadAtUtcMilliseconds).UtcDateTime:null,DateTime.UtcNow);
   }catch{Kill();process?.Dispose();process=null;throw;}finally{gate.Release();}
  }
  public async ValueTask DisposeAsync(){await gate.WaitAsync().ConfigureAwait(false);try{Kill();process?.Dispose();process=null;}finally{gate.Release();}}
 }
}
