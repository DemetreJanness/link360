using System.Diagnostics;
using System.Text.Json;
using Link360;

namespace Link360Desktop;

internal static class Program {
 internal static readonly JsonSerializerOptions Json = new() {IncludeFields=true,WriteIndented=true};
 [STAThread] static void Main(string[] args) {
  Environment.SetEnvironmentVariable("ORT_DISABLE_TELEMETRY","1");
  if(args.Contains("--video-worker")){VideoWorker.Run(args.Last(),args.Contains("--scene-detail"));return;}
  if(args.Contains("--body-worker")) {
   var wire=new JsonSerializerOptions(){IncludeFields=true};
   try{using IBodyDetector detector=args.Contains("--whole-body")?new LocalWholeBodyDetector(args.Contains("--gpu")):new LocalBodyDetector(args.Contains("--gpu"));bool torso=args.Contains("--torso");using var pose=args.Contains("--pose")||torso?new LocalPoseDetector(args.Contains("--gpu")):null;string? line;while((line=Console.ReadLine())!=null){int separator=line.IndexOf('|');if(separator>=0){TrackingConfidence.Minimum=double.Parse(line[..separator],System.Globalization.CultureInfo.InvariantCulture);line=line[(separator+1)..];}var bytes=Convert.FromBase64String(line);var result=detector.Detect(bytes);if(torso)result=result with{AnchorKind="missing"};bool headLocked=false;if(torso&&result.People.Length==1&&result.Width==320&&result.Height==180){var head=HeadFramingAnchor.Select(LocalFaceDetector.Detect(bytes),result.People[0]);if(head is {} h){result=result with{People=[result.People[0] with{CenterX=h.X,CenterY=h.Y}],AnchorKind="head"};headLocked=true;}}if(pose!=null&&result.People.Length==1&&(!headLocked||args.Contains("--pose"))){var gesture=pose.Detect(bytes,result.People[0]);result=result with{ArmsOut=gesture.Arms,PoseMs=gesture.Ms,PoseReason=gesture.Reason};if(torso&&!headLocked&&gesture.AnchorKind=="upper-body"&&gesture.TorsoX.HasValue&&gesture.TorsoY.HasValue){var person=result.People[0];if(gesture.TorsoX>=person.X&&gesture.TorsoX<=person.X+person.Width&&gesture.TorsoY>=person.Y&&gesture.TorsoY<=person.Y+person.Height)result=result with{People=[person with{CenterX=gesture.TorsoX.Value,CenterY=gesture.TorsoY.Value}],AnchorKind=gesture.AnchorKind};}}Console.WriteLine(JsonSerializer.Serialize(result,wire));}}
   catch(Exception e){Console.WriteLine(JsonSerializer.Serialize(new BodyResult([],0,0,"unavailable",0,e.Message),wire));Environment.ExitCode=1;}
   return;
  }
  if(args.Contains("--detect-worker")) {
   var wire=new JsonSerializerOptions(){IncludeFields=true};
   try{string? line;while((line=Console.ReadLine())!=null){Console.WriteLine(JsonSerializer.Serialize(LocalFaceDetector.Detect(Convert.FromBase64String(line)),wire));}}
   catch(Exception e){Console.WriteLine(JsonSerializer.Serialize(new FaceResult([],0,0,e.Message),wire));Environment.ExitCode=1;}
   return;
  }
  if(args.Contains("--camera-session")||args.Contains("--continuous-camera-session")) {
   var wire=new JsonSerializerOptions(){IncludeFields=true};
   try {Action<Func<TrackingMotion>,Action<CameraReading>> serve=args.Contains("--continuous-camera-session")?(receive,respond)=>CameraProbe.ServeContinuous(receive,respond,args.Contains("--limited-tracking")):CameraProbe.ServeTracking;serve(()=>{
    var line=Console.ReadLine();if(line==null)return null!;
    var request=JsonSerializer.Deserialize<WorkerRequest>(line,Json)??throw new IOException("Invalid tracking request.");
    if(request.NativeTracking.HasValue||request.ReadNativeTracking||request.NativeTargetX.HasValue||request.NativeTargetY.HasValue||request.ReadRelativeCapabilities)throw new IOException("Capability/extension requests require a separate worker.");
    if(request.RelativePan.HasValue!=request.RelativeTilt.HasValue||(request.RelativePan.HasValue&&request.Target==null))throw new IOException("Relative corrections need both axes and a pose request");
    if(request.Control!=null)throw new IOException("Tracking session accepts bounded poses only.");
    if(request.Target==null)return new TrackingMotion{DurationMs=0,Hold=request.Hold};
    if(Process.GetProcessesByName("Insta360 Link Controller").Length>0)throw new IOException("Close Insta360 Link Controller first.");
    if(!double.IsFinite(request.SpeedScale)||request.SpeedScale<.15||request.SpeedScale>1)throw new IOException("Tracking speed outside bounds.");
    if((request.RelativePan.HasValue||request.RelativeTilt.HasValue)&&!args.Contains("--continuous-camera-session"))throw new IOException("Relative corrections require the continuous worker");
    return new TrackingMotion{Pan=request.Target.Pan,Tilt=request.Target.Tilt,Zoom=request.Target.Zoom,DurationMs=request.DurationMs,SpeedScale=request.SpeedScale,RelativePan=request.RelativePan,RelativeTilt=request.RelativeTilt};
   },r=>Console.WriteLine(JsonSerializer.Serialize(r,wire)));}
   catch(Exception e){Console.WriteLine(JsonSerializer.Serialize(new{Error=e.Message,ErrorKind="camera_unavailable"},wire));Environment.ExitCode=1;}
   return;
  }
  if(args.Contains("--worker")) {
   WorkerRequest? request=null;
   try {
    request=JsonSerializer.Deserialize<WorkerRequest>(Console.ReadLine()??"",Json)!;
    if(request.RelativePan.HasValue||request.RelativeTilt.HasValue)throw new IOException("Relative corrections require the continuous worker");
    if(request.ReadRelativeCapabilities&&(request.Target!=null||request.Control!=null||request.NativeTracking.HasValue||request.ReadNativeTracking))throw new IOException("Choose one capability query or control operation");
    if((request.ReadNativeTracking||request.NativeTracking.HasValue)&&(request.Target!=null||request.Control!=null))throw new IOException("Choose either native tracking or a PTZ request.");
    if(request.ReadNativeTracking&&request.NativeTracking.HasValue)throw new IOException("Choose native read or write.");
    if(request.Control!=null&&request.Target!=null)throw new Exception("Choose one control request type.");
    if(Process.GetProcessesByName("Insta360 Link Controller").Length>0 && (request.Control!=null||request.Target!=null)) throw new Exception("Close Insta360 Link Controller first.");
    if(request.NativeTracking.HasValue&&Process.GetProcessesByName("Insta360 Link Controller").Length>0)throw new IOException("Close Insta360 Link Controller first.");
    var result=request.ReadRelativeCapabilities ? CameraProbe.ReadRelativeCapabilities() : request.ReadNativeTracking||request.NativeTracking.HasValue ? CameraProbe.NativeTracking(request.NativeTracking,request.NativeTargetX,request.NativeTargetY) : request.Target!=null ? CameraProbe.MoveSmooth(request.Target.Pan,request.Target.Tilt,request.Target.Zoom,request.DurationMs)
     : request.Control==null ? CameraProbe.Read() : CameraProbe.SetControl(request.Control,request.Value);
    if(result.Count!=1) throw new Exception("Connect exactly one Insta360 Link camera.");
    if(result[0].Error!=null) throw new Exception(result[0].Error);
    Console.Write(JsonSerializer.Serialize(result[0],Json));
   } catch(Exception e) { Console.Write(JsonSerializer.Serialize(new {Error=e.Message,ErrorKind=request?.Control==null&&request?.Target==null?"camera_unavailable":"command_failed"},Json)); Environment.ExitCode=1; }
   return;
  }
  ApplicationConfiguration.Initialize();
  using var instance=new Mutex(true,"Local\\LINK360.Desktop",out bool first);
  if(!first){MessageBox.Show("LINK360 is already running.");return;}
  Application.Run(new ControllerForm());
 }
}
internal record WorkerRequest(string? Control=null,int Value=0,Pose? Target=null,int DurationMs=1400,bool Hold=false,bool? NativeTracking=null,bool ReadNativeTracking=false,double? NativeTargetX=null,double? NativeTargetY=null,double SpeedScale=1,int? RelativePan=null,int? RelativeTilt=null,bool ReadRelativeCapabilities=false);
internal record Pose(int Pan,int Tilt,int Zoom);
internal record Preset(string Name,Pose Position);
internal sealed class CameraWorkerException(string message,string kind) : IOException(message) {
 public string Kind {get;}=kind;
}
internal sealed class Settings {
 public int Version {get;set;}=1;
 public Preset?[] Presets {get;set;}=new Preset?[4];
 public int TransitionMs {get;set;}=1400;
 public RoomPoint[] RoomPoints {get;set;}=[];
 public int RoomMapVersion {get;set;}
 public int FramingMode {get;set;}
 public RoomPoint[] PreviousRoomPoints {get;set;}=[];
 public SceneReference? SceneHome {get;set;}
 public string? RoomMapSceneId {get;set;}
 public bool CameraPlacementChanged {get;set;}
}
internal sealed class CameraEngine {
 readonly SemaphoreSlim gate=new(1);
 readonly string executable;
 public CameraEngine(string? executable=null){this.executable=executable??Environment.ProcessPath!;}
 public async Task<CameraReading> Execute(WorkerRequest request,CancellationToken cancel,LocalCameraSession? session=null) {
  await gate.WaitAsync(cancel).ConfigureAwait(false);
  try {
   if(session!=null)return await session.Execute(request,cancel).ConfigureAwait(false);
   var info=new ProcessStartInfo(executable,"--worker") {UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
   using var process=Process.Start(info)??throw new Exception("Cannot start camera worker");
   using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancel);
   deadline.CancelAfter(TimeSpan.FromSeconds(8));
   try {
    await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request)).ConfigureAwait(false);
    process.StandardInput.Close();
    var output=process.StandardOutput.ReadToEndAsync(deadline.Token);
    var errors=process.StandardError.ReadToEndAsync(deadline.Token);
    await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
    var result=JsonSerializer.Deserialize<CameraReading>(await output.ConfigureAwait(false),Program.Json)??throw new Exception("Invalid camera response");
    await errors.ConfigureAwait(false);
    if(process.ExitCode!=0 || result.Error!=null) throw new CameraWorkerException(result.Error??"Camera worker failed",result.ErrorKind??"command_failed");
    return result;
   } catch(OperationCanceledException) {
    if(!process.HasExited) process.Kill(true);
    if(cancel.IsCancellationRequested)throw new OperationCanceledException("Action cancelled; refresh to confirm camera state.",cancel);
    throw new TimeoutException("Camera timeout; refresh to reconnect.");
   }
  } finally {gate.Release();}
 }
}
