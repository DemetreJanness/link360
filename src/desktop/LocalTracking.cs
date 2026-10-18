using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Link360;
using OpenCvSharp;

namespace Link360Desktop;

internal record FaceBox(double X,double Y,double Width,double Height,double Confidence);
internal record FaceResult(FaceBox[] Faces,int Width,int Height,string? Error=null);

internal static class LocalFaceDetector {
 static readonly Lazy<LocalOnnxFaceDetector> detector=new(()=>new());
 public static FaceResult Detect(byte[] bytes)=>detector.Value.Detect(bytes);
}
// Retained reference backend for offline numerical/performance comparison.
internal static class OpenCvFaceDetector {
 static FaceDetectorYN? detector;
 static FaceDetectorYN Load() {
  string root=Path.Combine(AppContext.BaseDirectory,"models");
  using var provenance=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"provenance.json")));
  string model=Path.Combine(root,"face_detection_yunet_2023mar.onnx");
  if(!Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(model))).Equals(provenance.RootElement.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase))throw new IOException("Local tracking model checksum mismatch.");
  Cv2.SetNumThreads(1);return FaceDetectorYN.Create(model,"",new OpenCvSharp.Size(320,180),0.85f,0.3f,100);
 }
 public static FaceResult Detect(byte[] png) {
  using var image=Cv2.ImDecode(png,ImreadModes.Color);
  if(image.Empty()||image.Width!=320||image.Height!=180)throw new IOException("Expected a 320x180 tracking image.");
  detector??=Load();
  using var faces=new Mat();detector.Detect(image,faces);
  var result=new List<FaceBox>();
  int rows=faces.Rows;for(int i=0;i<rows;i++)result.Add(new(faces.At<float>(i,0),faces.At<float>(i,1),faces.At<float>(i,2),faces.At<float>(i,3),faces.At<float>(i,14)));
  return new(result.ToArray(),image.Width,image.Height);
 }
}
internal sealed class LocalFaceSession : IAsyncDisposable {
 readonly Process p;
 readonly CancellationTokenRegistration stop;
 public LocalFaceSession(CancellationToken ct){p=Process.Start(new ProcessStartInfo(Environment.ProcessPath!,"--detect-worker"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true})??throw new IOException("Cannot start local inference worker.");stop=ct.Register(()=>{try{if(!p.HasExited)p.Kill(true);}catch(InvalidOperationException){};});}
 public async Task<FaceResult> Infer(byte[] png,CancellationToken ct) {
  using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(3000);
  try {
   await p.StandardInput.WriteLineAsync(Convert.ToBase64String(png).AsMemory(),deadline.Token).ConfigureAwait(false);
   var text=await p.StandardOutput.ReadLineAsync(deadline.Token).ConfigureAwait(false)??throw new IOException("Local inference worker exited.");
   var result=JsonSerializer.Deserialize<FaceResult>(text,Program.Json)??throw new IOException("Invalid local inference result.");
   if(result.Error!=null)throw new IOException(result.Error);return result;
  } catch {if(!p.HasExited)p.Kill(true);throw;}
 }
 public async ValueTask DisposeAsync(){stop.Dispose();if(!p.HasExited){p.Kill(true);await p.WaitForExitAsync().ConfigureAwait(false);}p.Dispose();}
}

// Shared geometric centering. No recognition, identity storage or zoom changes.
internal sealed class TrackingPlanner {
 (double X,double Y)? previous;
 int stable;
 readonly AdaptiveAim aim=new();readonly AimDeadband horizontal=new(),vertical=new();readonly FeedbackTravelBudget feedback=new();
 public double SpeedScale {get;private set;}=.15;
 public void Reset(){previous=null;stable=0;aim.Reset();horizontal.Reset();vertical.Reset();feedback.Reset();}
 public Pose? Plan(FaceResult result,CameraReading state,bool reversePan,bool reverseTilt,DateTime? now=null) {
  if(result.Error!=null||result.Width<=0||result.Height<=0||result.Faces.Length!=1){Reset();return null;}
  var face=result.Faces[0];
  if(!double.IsFinite(face.X+face.Y+face.Width+face.Height+face.Confidence)||face.Confidence<0.85||face.Width<16||face.Height<16||face.X<0||face.Y<0||face.X+face.Width>result.Width||face.Y+face.Height>result.Height){Reset();return null;}
  double x=(face.X+face.Width/2)/result.Width,y=(face.Y+face.Height/2)/result.Height;
  return PlanCenter(x,y,state,reversePan,reverseTilt,now??DateTime.UtcNow);
 }
 public Pose? PlanBody(BodyResult result,CameraReading state,bool reversePan,bool reverseTilt,DateTime? now=null) {
  if(!LinkLock.HasUsableTorso(result)){Reset();return null;}
  if(result.Error!=null||result.Width<=0||result.Height<=0||result.People.Length!=1){Reset();return null;}var p=result.People[0];
  if(p.Confidence<TrackingConfidence.Minimum||!double.IsFinite(p.X+p.Y+p.Width+p.Height+p.CenterX+p.CenterY+p.Confidence)||p.Width<16||p.Height<24||p.X<0||p.Y<0||p.X+p.Width>result.Width||p.Y+p.Height>result.Height||p.CenterX<0||p.CenterX>result.Width||p.CenterY<0||p.CenterY>result.Height){Reset();return null;}
  return PlanCenter(p.CenterX/result.Width,p.CenterY/result.Height,state,reversePan,reverseTilt,now??DateTime.UtcNow);
 }
 Pose? PlanCenter(double x,double y,CameraReading state,bool reversePan,bool reverseTilt,DateTime now) {
  feedback.Observe(state,now);double rawX=x-.5,rawY=y-.5;
  if(previous!=null&&Math.Abs(x-previous.Value.X)<0.18&&Math.Abs(y-previous.Value.Y)<0.18)stable++;else stable=1;
  previous=(x,y);if(stable<2)return null;
  (x,y)=aim.Update(x,y,now);
  SpeedScale=Math.Clamp(.15+Math.Max(0,Math.Max(Math.Abs(x-.5),Math.Abs(y-.5))-.065)*3+aim.Speed*.4,.15,1);
  int Value(string name)=>state.Controls.Single(c=>c.Name==name).Value;
  int Adjust(string name,double error,double rawError,int sign) {
   var c=state.Controls.Single(c=>c.Name==name);
   if(c.RangeHResult!=0||c.GetHResult!=0||c.Step<=0||(c.Capabilities&2)==0)throw new IOException("Tracking control unavailable.");
   // Image-space filtering must not prolong a correction after the newest
   // observation is framed or has crossed to the opposite side.
   if(error==0||Math.Abs(rawError)<.035||Math.Sign(error)!=Math.Sign(rawError))return c.Value;
   int units=(int)Math.Clamp(Math.Round(Math.Abs(error)*16),0,5);if(units<c.Step)return c.Value;
   int delta=feedback.Limit(name,Math.Max(c.Step,units/c.Step*c.Step)*Math.Sign(error)*sign);
   return Math.Clamp(c.Value+delta,c.Min,c.Max);
  }
  int pan=Adjust("Pan",horizontal.Error(x-0.5),rawX,reversePan?-1:1),tilt=Adjust("Tilt",vertical.Error(y-0.5),rawY,reverseTilt?1:-1);
  return pan==Value("Pan")&&tilt==Value("Tilt")?null:new(pan,tilt,Value("Zoom"));
 }
}
