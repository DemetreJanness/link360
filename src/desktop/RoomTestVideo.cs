using OpenCvSharp;
using Point=OpenCvSharp.Point;
using Size=OpenCvSharp.Size;
namespace Link360Desktop;

// Explicit test-only, local video: paired latest views, no audio or identities.
// A single replaceable packet prevents disk/encoding work from blocking tracking.
internal sealed class RoomTestVideo : IDisposable {
 sealed record Packet(FeedFrame? Room,FeedFrame? Link,RoomTestSample Sample);
 readonly string path;readonly CancellationTokenSource stop=new();readonly Task worker;
 Packet? latest;public string? Error{get;private set;}
 public RoomTestVideo(string path){this.path=path;worker=Task.Run(Write);}
 public void Push(FeedFrame? room,FeedFrame? link,RoomTestSample sample)=>Volatile.Write(ref latest,new(room,link,sample));
 async Task Write(){try{
  using var writer=new VideoWriter(path,VideoWriter.FourCC('M','J','P','G'),30,new Size(960,360));if(!writer.IsOpened())throw new IOException("Local diagnostic video codec unavailable");
  var clock=System.Diagnostics.Stopwatch.StartNew();using var canvas=new Mat(360,960,MatType.CV_8UC3);using var timer=new PeriodicTimer(TimeSpan.FromSeconds(1.0/30));
  while(await timer.WaitForNextTickAsync(stop.Token)){
   var packet=Volatile.Read(ref latest);if(packet==null)continue;if(clock.Elapsed>TimeSpan.FromSeconds(62)||new FileInfo(path).Length>90_000_000)break;
   canvas.SetTo(new Scalar(16,20,26));Draw(canvas,packet.Room,0,"ACER");Draw(canvas,packet.Link,480,"LINK");var s=packet.Sample;
   string source=s.Outcome=="move"?(s.RelativePan.HasValue?"LINK fine":"ACER map"):s.Outcome;
   if(s.RoomX is double rx&&s.RoomY is double ry)Cv2.Circle(canvas,new Point((int)(rx*480),(int)(ry*270)),5,new Scalar(80,230,150),2);
   if(s.LinkErrorX is double lx&&s.LinkErrorY is double ly)Cv2.Circle(canvas,new Point(480+(int)((lx+.5)*480),(int)((ly+.5)*270)),5,s.Anchor=="pose"?new Scalar(80,230,150):new Scalar(0,180,255),2);
   Text(canvas,$"t={s.ElapsedMs/1000.0:F1}s  {source} {(s.ForcedAcer?"FORCED ACER TEST":"")}  anchor={s.Anchor}  speed={s.SpeedScale:F2}",12,295);
   Text(canvas,$"Room age {s.RoomAgeMs:F0}ms | Link age {s.LinkAgeMs:F0}ms | issued {s.Commanded?.Pan},{s.Commanded?.Tilt}",12,320);
   Text(canvas,$"Goal {s.ResolvedTarget?.Pan},{s.ResolvedTarget?.Tilt} | error {s.LinkErrorX:F3},{s.LinkErrorY:F3} | driver {s.After?.Pan},{s.After?.Tilt}",12,344);
   writer.Write(canvas);
  }
 }catch(OperationCanceledException)when(stop.IsCancellationRequested){}catch(Exception e){Error=e.Message;}}
 static void Text(Mat target,string text,int x,int y)=>Cv2.PutText(target,text,new Point(x,y),HersheyFonts.HersheySimplex,.45,new Scalar(230,235,240),1,LineTypes.AntiAlias);
 static void Draw(Mat canvas,FeedFrame? frame,int x,string name){if(frame==null){Text(canvas,name+" unavailable",x+12,30);return;}using var image=Cv2.ImDecode(frame.Bytes,ImreadModes.Color);if(image.Empty())return;using var tile=new Mat();Cv2.Resize(image,tile,new Size(480,270));using var roi=new Mat(canvas,new Rect(x,0,480,270));tile.CopyTo(roi);Cv2.Rectangle(canvas,new Rect(x+223,119,34,32),new Scalar(80,220,150),1);Text(canvas,name+" | read "+(frame.ReadAtUtc?.ToString("HH:mm:ss.fff")??"OBS receipt"),x+10,22);}
 public void Dispose(){stop.Cancel();try{worker.GetAwaiter().GetResult();}finally{stop.Dispose();}}
}
