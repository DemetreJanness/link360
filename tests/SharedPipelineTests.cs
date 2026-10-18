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

 public static bool TryWorker(string[] args){
  string? folder=Environment.GetEnvironmentVariable(WorkerFolder);
  if(folder==null||!args.Any(a=>a is "--continuous-camera-session" or "--body-worker" or "--worker" or "--video-worker"))return false;
  Directory.CreateDirectory(folder);bool camera=args.Contains("--continuous-camera-session");
  bool video=args.Contains("--video-worker"),body=args.Contains("--body-worker"),allowed=File.Exists(Path.Combine(folder,"allow-fallback"));
  string kind=camera?"camera":allowed&&video?"video":allowed&&body?"body":"unexpected";
  string path=Path.Combine(folder,$"{kind}-{Environment.ProcessId}.jsonl");File.WriteAllText(path,JsonSerializer.Serialize(new{started=true,args})+"\n");
  int pan=0,tilt=0;long sequence=0;string? line;
  while((line=Console.ReadLine())!=null){
   if(allowed&&video){Thread.Sleep(60);Console.WriteLine(JsonSerializer.Serialize(new VideoPacket(Convert.ToBase64String([1]),++sequence,ReadAtUtcMilliseconds:DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())));continue;}
   if(allowed&&body){Console.WriteLine(JsonSerializer.Serialize(Body(),Wire));continue;}
   if(!camera){Console.WriteLine("{\"Error\":\"Unexpected duplicate worker in shared-pipeline fixture\"}");continue;}
   var request=JsonSerializer.Deserialize<WorkerRequest>(line,Wire)!;
   File.AppendAllText(path,JsonSerializer.Serialize(new{at=DateTime.UtcNow,request})+"\n");
   if(!request.Hold&&request.Target is {} target){pan=target.Pan;tilt=target.Tilt;}
   Console.WriteLine(JsonSerializer.Serialize(Reading(pan,tilt),Wire));
  }
  return true;
 }
 static WorkerRequest[] Commands(string folder){var commands=new List<WorkerRequest>();foreach(var path in Directory.GetFiles(folder,"camera-*.jsonl")){try{using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);using var reader=new StreamReader(stream);string? line;while((line=reader.ReadLine())!=null){try{using var json=JsonDocument.Parse(line);if(json.RootElement.TryGetProperty("request",out var request))commands.Add(request.Deserialize<WorkerRequest>(Wire)!);}catch(JsonException){/* Worker may still be appending the final line; next poll retries. */}}}catch(IOException){/* Short writer-open race; next bounded poll retries. */}}return commands.ToArray();}
 static int Moves(string folder)=>Commands(folder).Count(r=>r.Target!=null&&!r.Hold);
 static int Holds(string folder)=>Commands(folder).Count(r=>r.Hold);
 static async Task Until(Func<bool> condition,int timeout=2000){var clock=Stopwatch.StartNew();while(!condition()&&clock.ElapsedMilliseconds<timeout)await Task.Delay(25);}
 static async Task Scenario(string root,string name,Func<ControllerForm,CancellationTokenSource,string,Task> run,Action<ControllerForm,string>? setup=null){
  string folder=Path.Combine(root,name);Directory.CreateDirectory(folder);Environment.SetEnvironmentVariable(WorkerFolder,folder);
  using var form=new ControllerForm();using var preview=new CancellationTokenSource();using var tracking=new CancellationTokenSource();
  Get<CameraFeeds>(form,"feeds").AllowObsFallback=false;Set(form,"cameraAvailable",true);Set(form,"state",Reading());Set(form,"trackingOrigin",new Pose(0,0,100));Set(form,"linkPreview",preview);Set(form,"tracking",tracking);
  setup?.Invoke(form,folder);Task? loop=null;
  try{loop=(Task)typeof(ControllerForm).GetMethod("Track",Fields)!.Invoke(form,[tracking,Get<RoomMapTest?>(form,"roomTest")])!;await run(form,tracking,folder);}
  finally{tracking.Cancel();if(loop!=null)await loop.WaitAsync(TimeSpan.FromSeconds(4));await Get<CameraFeeds>(form,"feeds").DisposeAsync();}
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
     completion.SetResult();
    }catch(Exception e){completion.SetException(e);}finally{Application.ExitThread();}
   }));
   Application.Run();
  }){IsBackground=true};thread.SetApartmentState(ApartmentState.STA);thread.Start();
  try{await completion.Task.WaitAsync(TimeSpan.FromSeconds(30));}
  finally{Environment.SetEnvironmentVariable(WorkerFolder,previous);File.WriteAllText(Path.Combine(root,"results.json"),JsonSerializer.Serialize(new{failures,tests=results},new JsonSerializerOptions(){WriteIndented=true}));}
  if(failures>0)Environment.ExitCode=1;
 }
}
