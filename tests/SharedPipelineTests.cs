using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Windows.Forms;
using Link360;
using Link360Desktop;

// Exercises the actual private Track loop on a hidden WinForms message thread.
// Child worker requests are intercepted by this test executable: no camera,
// capture, OBS, GPU model, application Shown event or hotkey is started.
internal static class SharedPipelineTests {
 const string WorkerFolder="LINK360_PIPELINE_TEST_WORKERS";
 static readonly JsonSerializerOptions Wire=new(){IncludeFields=true};
 static readonly BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
 static readonly List<object> results=[];
 static int failures;
 static void Set(ControllerForm form,string field,object? value)=>typeof(ControllerForm).GetField(field,Fields)!.SetValue(form,value);
 static T Get<T>(ControllerForm form,string field)=>(T)typeof(ControllerForm).GetField(field,Fields)!.GetValue(form)!;
 static CameraReading Reading(int pan=0,int tilt=0)=>new(){SnapshotUtcMilliseconds=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),CommandedPan=pan,CommandedTilt=tilt,Controls=[new(){Name="Pan",Value=pan,Min=-145,Max=145,Step=1,Capabilities=2},new(){Name="Tilt",Value=tilt,Min=-90,Max=90,Step=1,Capabilities=2},new(){Name="Zoom",Value=100,Min=100,Max=400,Step=1,Capabilities=2}]};
 static BodyResult Body()=>new([new(120,20,150,150,224,90,.95)],320,180,"synthetic",0,AnchorKind:"head");
 static void Publish(ControllerForm form,DateTime at){Set(form,"linkObservation",Body());Set(form,"lastLinkFrame",new FeedFrame([1],at,DateTime.UtcNow));Set(form,"linkObservationTime",at);typeof(ControllerForm).GetMethod("SignalTrackingFrame",Fields)!.Invoke(form,null);}
 static void Check(string name,bool pass,string detail=""){results.Add(new{name,pass,detail});if(!pass)failures++;Console.WriteLine($"{(pass?"PASS":"FAIL")}: {name} {detail}");}

 static byte[] CaptureFixtureFrame(long sequence){using var image=new OpenCvSharp.Mat(180,320,OpenCvSharp.MatType.CV_8UC3,new OpenCvSharp.Scalar(sequence%2==0?40:90,70,110));return image.ToBytes(".png");}
 public static bool TryWorker(string[] args){
  string? folder=Environment.GetEnvironmentVariable(WorkerFolder);
  if(folder==null||!args.Any(a=>a is "--continuous-camera-session" or "--speed-camera-session" or "--body-worker" or "--worker" or "--video-worker"))return false;
  Directory.CreateDirectory(folder);bool camera=args.Contains("--continuous-camera-session")||args.Contains("--speed-camera-session");
  bool video=args.Contains("--video-worker"),body=args.Contains("--body-worker"),allowed=File.Exists(Path.Combine(folder,"allow-fallback"));
  bool captureFixture=File.Exists(Path.Combine(folder,"capture-centred"));
  string kind=camera?"camera":allowed&&video?"video":allowed&&body?"body":captureFixture&&args.Contains("--worker")?"native":"unexpected";
  string path=Path.Combine(folder,$"{kind}-{Environment.ProcessId}.jsonl");File.WriteAllText(path,JsonSerializer.Serialize(new{started=true,args})+"\n");
  int pan=0,tilt=0;long sequence=0;string? line;
  while((line=Console.ReadLine())!=null){
   if(allowed&&video){if(sequence==0&&File.Exists(Path.Combine(folder,"slow-video-start")))Thread.Sleep(8000);Thread.Sleep(60);Console.WriteLine(JsonSerializer.Serialize(new VideoPacket(Convert.ToBase64String(captureFixture?CaptureFixtureFrame(sequence):[1]),++sequence,ReadAtUtcMilliseconds:DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())));continue;}
   if(allowed&&body){Console.WriteLine(JsonSerializer.Serialize(captureFixture?Body() with{People=[new(80,10,160,160,160,90,.95)]}:Body(),Wire));continue;}
   var request=JsonSerializer.Deserialize<WorkerRequest>(line,Wire)!;
   if(captureFixture&&request.ReadNativeTracking){
    if(camera)Console.WriteLine("{\"Error\":\"Capability/extension requests require a separate worker.\"}");
    else{var native=Reading();native.NativeTrackingEnabled=false;Console.WriteLine(JsonSerializer.Serialize(native,Wire));}continue;
   }
   if(!camera){Console.WriteLine("{\"Error\":\"Unexpected duplicate worker in shared-pipeline fixture\"}");continue;}
   File.AppendAllText(path,JsonSerializer.Serialize(new{at=DateTime.UtcNow,request})+"\n");
   if(!request.Hold&&request.Target is {} target){pan=target.Pan;tilt=target.Tilt;}
   var reading=Reading(pan,tilt);if(args.Contains("--speed-camera-session")){reading.NativePanSpeed=request.NativePanSpeed??0;reading.NativeTiltSpeed=request.NativeTiltSpeed??0;reading.Operation="NativeSpeedTracking";}Console.WriteLine(JsonSerializer.Serialize(reading,Wire));
  }
  return true;
 }
 static WorkerRequest[] Commands(string folder){var commands=new List<WorkerRequest>();foreach(var path in Directory.GetFiles(folder,"camera-*.jsonl")){try{using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);using var reader=new StreamReader(stream);string? line;while((line=reader.ReadLine())!=null){try{using var json=JsonDocument.Parse(line);if(json.RootElement.TryGetProperty("request",out var request))commands.Add(request.Deserialize<WorkerRequest>(Wire)!);}catch(JsonException){/* Worker may still be appending the final line; next poll retries. */}}}catch(IOException){/* Short writer-open race; next bounded poll retries. */}}return commands.ToArray();}
 static int Moves(string folder)=>Commands(folder).Count(r=>r.Target!=null&&!r.Hold);
 static int Holds(string folder)=>Commands(folder).Count(r=>r.Hold);
 static async Task Until(Func<bool> condition,int timeout=2000){var clock=Stopwatch.StartNew();while(!condition()&&clock.ElapsedMilliseconds<timeout)await Task.Delay(25);}
 static async Task Scenario(string root,string name,Func<ControllerForm,CancellationTokenSource,string,Task> run,Action<ControllerForm,string>? setup=null){
  string folder=Path.Combine(root,name);Directory.CreateDirectory(folder);Environment.SetEnvironmentVariable(WorkerFolder,folder);
  using var form=new ControllerForm(Path.Combine(folder,"fixture-settings.json"));using var preview=new CancellationTokenSource();using var tracking=new CancellationTokenSource();
  Get<CameraFeeds>(form,"feeds").AllowObsFallback=false;Set(form,"cameraAvailable",true);Set(form,"state",Reading());Set(form,"trackingOrigin",new Pose(0,0,100));Set(form,"linkPreview",preview);
  setup?.Invoke(form,folder);Set(form,"tracking",tracking);Task? loop=null;
  try{loop=(Task)typeof(ControllerForm).GetMethod("Track",Fields)!.Invoke(form,[tracking,Get<RoomMapTest?>(form,"roomTest")])!;await run(form,tracking,folder);}
  finally{try{tracking.Cancel();}catch(ObjectDisposedException){}if(loop!=null)await loop.WaitAsync(TimeSpan.FromSeconds(4));await Get<CameraFeeds>(form,"feeds").DisposeAsync();}
  Check(name+": no duplicate inference/capture worker",Directory.GetFiles(folder,"unexpected-*.jsonl").Length==0);
  Check(name+": one camera worker",Directory.GetFiles(folder,"camera-*.jsonl").Length==1);
  var pids=Directory.GetFiles(folder,"*.jsonl").Select(f=>int.Parse(Path.GetFileNameWithoutExtension(f).Split('-')[1]));
  Check(name+": cancellation ends worker",pids.All(pid=>{try{using var p=Process.GetProcessById(pid);return p.HasExited;}catch(ArgumentException){return true;}}));
 }
 public static async Task Run(string root){
  root=Path.GetFullPath(root);if(Directory.Exists(root))throw new IOException("Choose a new fixture output folder to preserve earlier results.");Directory.CreateDirectory(root);
  results.Clear();failures=0;string? previous=Environment.GetEnvironmentVariable(WorkerFolder);var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
  var thread=new Thread(()=>{
   using var dispatcher=new Control();_ = dispatcher.Handle;
   dispatcher.BeginInvoke((Action)(async()=>{
    try{
     await Scenario(root,"fresh-duplicate-stale",async(form,tracking,folder)=>{
      await Until(()=>Holds(folder)>0);
      for(int i=0;i<5;i++){Publish(form,DateTime.UtcNow);await Task.Delay(80);}
      await Task.Delay(80);int moves=Moves(folder);
      Check("fresh shared frames drive planner",moves>0);
      await Task.Delay(150);Check("duplicate shared observation never repeats correction",Moves(folder)==moves);
      int beforeStaleHolds=Holds(folder);await Task.Delay(650);
      Check("aged duplicate enters explicit stale hold",Get<Label>(form,"trackingStatus").Text.Contains("frame stale")&&Holds(folder)>beforeStaleHolds,Get<Label>(form,"trackingStatus").Text);
      int holds=Holds(folder);await Task.Delay(220);Check("stale hold does not flood commands",Holds(folder)==holds);
      for(int i=0;i<4;i++){Publish(form,DateTime.UtcNow);await Task.Delay(80);}
      Check("fresh frames resume after stale hold",Moves(folder)>moves);
     });
     await Scenario(root,"native-speed-route",async(form,tracking,folder)=>{
      for(int i=0;i<7;i++){Publish(form,DateTime.UtcNow);await Task.Delay(80);}
      var speeds=Commands(folder).Where(r=>r.NativePanSpeed.HasValue).ToArray();Check("actual Link loop routes native velocity without finite pose targets",speeds.Any(r=>r.NativePanSpeed>0)&&speeds.All(r=>r.Target==null));
      int count=speeds.Length;await Task.Delay(180);Check("duplicate frames do not renew native velocity",Commands(folder).Count(r=>r.NativePanSpeed.HasValue)==count);
     },(form,folder)=>Get<CheckBox>(form,"nativeSpeed").Checked=true);
     await Scenario(root,"future-frame-recovery",async(form,tracking,folder)=>{
      await Until(()=>Holds(folder)>0);
      Publish(form,DateTime.UtcNow.AddMinutes(1));await Task.Delay(180);
      Check("future observation never moves camera",Moves(folder)==0);
      Check("future observation reports stale hold",Get<Label>(form,"trackingStatus").Text.Contains("frame stale"));
      for(int i=0;i<6;i++){Publish(form,DateTime.UtcNow);await Task.Delay(100);}
      Check("valid frames recover after future timestamp",Moves(folder)>0,Get<Label>(form,"trackingStatus").Text);
     });
     await Scenario(root,"missing-observation",async(form,tracking,folder)=>{
      await Until(()=>Holds(folder)>0);int holds=Holds(folder);await Task.Delay(300);
      Check("missing observation holds without command flooding",holds==1&&Holds(folder)==holds&&Moves(folder)==0);
     });
     await Scenario(root,"link-only-test-log",async(form,tracking,folder)=>{
      await Until(()=>Holds(folder)>0);
      for(int i=0;i<5;i++){Publish(form,DateTime.UtcNow);await Task.Delay(80);}
      var diagnostic=Get<RoomMapTest>(form,"roomTest");
      using var stream=new FileStream(diagnostic.LogPath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);using var reader=new StreamReader(stream);var lines=reader.ReadToEnd().Split('\n',StringSplitOptions.RemoveEmptyEntries);using var header=JsonDocument.Parse(lines[0]);
      Check("Link-only diagnostic identifies steering mode",header.RootElement.GetProperty("mode").GetString()=="link-only");
      Check("Link-only diagnostic never forces Acer",header.RootElement.GetProperty("forcedAcerWindowMs").GetArrayLength()==0);
      Check("Link-only diagnostic records actual loop moves",lines.Any(line=>line.Contains("\"Outcome\":\"move\""))&&Moves(folder)>0);
     },(form,folder)=>Set(form,"roomTest",new RoomMapTest(Path.Combine(folder,"diagnostics"),[],linkOnly:true)));
     var logSummary=Directory.GetFiles(Path.Combine(root,"link-only-test-log","diagnostics"),"*.summary.json").Single();
     using(var report=JsonDocument.Parse(File.ReadAllText(logSummary)))Check("Link-only cancellation saves its mode and reason",report.RootElement.GetProperty("mode").GetString()=="link-only"&&report.RootElement.GetProperty("reason").GetString()=="cancelled");
     await Scenario(root,"preview-failure-fallback",async(form,tracking,folder)=>{
      await Until(()=>Holds(folder)>0);Set(form,"linkPreview",null);
      await Until(()=>Moves(folder)>0);
      Check("preview failure continues with standalone tracking",Moves(folder)>0,Get<Label>(form,"trackingStatus").Text);
      Check("fallback creates exactly one inference worker",Directory.GetFiles(folder,"body-*.jsonl").Length==1);
      Check("fallback uses the injected capture worker",Directory.GetFiles(folder,"video-*.jsonl").Length==1);
      Set(form,"linkPreview",tracking);Publish(form,DateTime.UtcNow);await Task.Delay(180);
      var bodyPath=Directory.GetFiles(folder,"body-*.jsonl").Single();int bodyPid=int.Parse(Path.GetFileNameWithoutExtension(bodyPath).Split('-')[1]);bool exited;try{using var worker=Process.GetProcessById(bodyPid);exited=worker.HasExited;}catch(ArgumentException){exited=true;}
      Check("shared-preview return disposes redundant inference",exited);
     },(form,folder)=>{
      File.WriteAllText(Path.Combine(folder,"allow-fallback"),"synthetic frames only");
      var video=Process.Start(new ProcessStartInfo(Environment.ProcessPath!,"--video-worker link"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true})!;
      var feeds=Get<CameraFeeds>(form,"feeds");var direct=typeof(CameraFeeds).GetField("link",Fields)!.GetValue(feeds)!;
      typeof(DirectFrames).GetField("process",Fields)!.SetValue(direct,video);
      typeof(DirectFrames).GetField("changed",Fields)!.SetValue(direct,DateTime.UtcNow);
     });
     string slowFolder=Path.Combine(root,"slow-capture-start");Directory.CreateDirectory(slowFolder);Environment.SetEnvironmentVariable(WorkerFolder,slowFolder);
     File.WriteAllText(Path.Combine(slowFolder,"allow-fallback"),"synthetic frames only");File.WriteAllText(Path.Combine(slowFolder,"slow-video-start"),"delay first frame beyond former seven-second limit");
     var slowVideo=Process.Start(new ProcessStartInfo(Environment.ProcessPath!,"--video-worker link"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true})!;
     int slowPid=slowVideo.Id;await using(var slow=new DirectFrames("link")){
      typeof(DirectFrames).GetField("process",Fields)!.SetValue(slow,slowVideo);typeof(DirectFrames).GetField("changed",Fields)!.SetValue(slow,DateTime.UtcNow);
      using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));var startup=Stopwatch.StartNew();var first=await slow.Read(timeout.Token);
      Check("slow initial capture survives beyond seven seconds",startup.ElapsedMilliseconds>=8000&&first.Bytes.Length==1&&first.ReadAtUtc!=null);
      startup.Restart();var second=await slow.Read(timeout.Token);Check("opened capture keeps normal fast frame delivery",startup.ElapsedMilliseconds<2000&&second.ReadAtUtc>=first.ReadAtUtc);
      using var cancelled=new CancellationTokenSource();cancelled.Cancel();bool rejected=false;try{await slow.Read(cancelled.Token);}catch(OperationCanceledException){rejected=true;}Check("slow-start capture still honours cancellation",rejected);
     }
     bool slowExited;try{using var remaining=Process.GetProcessById(slowPid);slowExited=remaining.HasExited;}catch(ArgumentException){slowExited=true;}Check("slow capture worker released",slowExited);
     string captureFolder=Path.Combine(root,"one-point-capture");Directory.CreateDirectory(captureFolder);Environment.SetEnvironmentVariable(WorkerFolder,captureFolder);
     File.WriteAllText(Path.Combine(captureFolder,"allow-fallback"),"synthetic frames only");File.WriteAllText(Path.Combine(captureFolder,"capture-centred"),"known centred head observations");
     using(var form=new ControllerForm(Path.Combine(captureFolder,"fixture-settings.json"))){
      var saved=Get<Settings>(form,"settings");RoomPoint[] original=[new(.4,.5,-3,5),new(.7,.5,14,5),new(.7,.8,14,-10),new(.4,.8,-3,-10)];
      var oldTen=Enumerable.Range(0,10).Select(i=>new RoomPoint(.1+i*.03,.3,i,i)).ToArray();
      saved.RoomPoints=original;saved.PreviousRoomPoints=oldTen;saved.RoomMapVersion=2;saved.RoomMapSceneId="capture-fixture";saved.SceneHome=new("capture-fixture",DateTime.UtcNow,SceneScanPolicy.Targets.Select(t=>new SceneAnchor(t.X,t.Y,0,0,100,40,.9)).ToArray());saved.GuidedPlacementId=null;saved.PendingSceneScanId=null;saved.CameraPlacementChanged=false;
      Get<CheckBox>(form,"sceneUnmoved").Checked=true;Set(form,"state",Reading());Set(form,"cameraAvailable",true);
      Set(form,"body",new BodyResult([new(20,40,220,270,192,216,.95)],640,360,"synthetic",0));Set(form,"bodyTime",DateTime.UtcNow);using var freshBody=new System.Windows.Forms.Timer{Interval=50};freshBody.Tick+=(_,_)=>Set(form,"bodyTime",DateTime.UtcNow);freshBody.Start();
      var video=Process.Start(new ProcessStartInfo(Environment.ProcessPath!,"--video-worker link"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true})!;
      var feeds=Get<CameraFeeds>(form,"feeds");var direct=typeof(CameraFeeds).GetField("link",Fields)!.GetValue(feeds)!;typeof(DirectFrames).GetField("process",Fields)!.SetValue(direct,video);typeof(DirectFrames).GetField("changed",Fields)!.SetValue(direct,DateTime.UtcNow);
      using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(8));await using var detector=new LocalBodySession(limit.Token,wholeBody:true,torso:true);await using var cameraSession=new LocalCameraSession(limit.Token);
      try{await (Task)typeof(ControllerForm).GetMethod("CentreAndRecord",Fields)!.Invoke(form,[detector,cameraSession,limit.Token,true])!;
       Check("actual point capture saves measured pair",saved.RoomPoints.Length==5&&saved.RoomPoints.Take(4).SequenceEqual(original)&&Math.Abs(saved.RoomPoints[4].X-.3)<1e-6);
       Check("actual point capture preserves original ten",saved.PreviousRoomPoints.SequenceEqual(oldTen));
       Check("native verification uses separate worker",Directory.GetFiles(captureFolder,"native-*.jsonl").Length==1&&Directory.GetFiles(captureFolder,"unexpected-*.jsonl").Length==0);
      }finally{await feeds.StopCapture();}
     }
     completion.SetResult();
    }catch(Exception e){completion.SetException(e);}finally{Application.ExitThread();}
   }));
   Application.Run();
  }){IsBackground=true};thread.SetApartmentState(ApartmentState.STA);thread.Start();
  try{await completion.Task.WaitAsync(TimeSpan.FromSeconds(45));}
  finally{Environment.SetEnvironmentVariable(WorkerFolder,previous);File.WriteAllText(Path.Combine(root,"results.json"),JsonSerializer.Serialize(new{failures,tests=results},new JsonSerializerOptions(){WriteIndented=true}));}
  if(failures>0)Environment.ExitCode=1;
 }
}
