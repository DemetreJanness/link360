using System.Text.Json;
using Link360;
using Link360Desktop;

if(args.Contains("--gpu-head-benchmark")){GpuHeadBenchmark.Run(args[1],args[2],!args.Contains("--cpu"));return;}
if(args.Contains("--vision-model-info")){using var options=new Microsoft.ML.OnnxRuntime.SessionOptions();Microsoft.ML.OnnxRuntime.OrtEnv.Instance().DisableTelemetryEvents();using var session=new Microsoft.ML.OnnxRuntime.InferenceSession(Path.Combine(AppContext.BaseDirectory,"models","face_detection_yunet_2023mar.onnx"),options);Console.WriteLine(JsonSerializer.Serialize(new{inputs=session.InputMetadata.Select(x=>new{x.Key,x.Value.Dimensions,x.Value.SymbolicDimensions}),outputs=session.OutputMetadata.Select(x=>new{x.Key,x.Value.Dimensions,x.Value.SymbolicDimensions})}));return;}
if(args.Contains("--scene-startup-probe")){await SceneStartupProbe.Run(args.Last());return;}
if(args.Contains("--scene-match-probe")){await SceneMatchProbe.Run(args.Last());return;}
if(args.Contains("--review-paired-video")){VideoMotionReview.Run(args[1],args[2]);return;}
if(args.Contains("--replay-head-framing")){HeadFramingTests.Replay(args.Last());return;}
if(args.Contains("--guide-render")){GuideRender.Run(args.Last());return;}
if(args.Contains("--scene-render")){SceneRender.Run(args.Last());return;}

if(args.Contains("--replay-video")){RecordingReplay.Run(args.Last(),args.Contains("--pose-anchors"),args.Contains("--profile"));return;}
if(args.Contains("--room-quality")){RoomQualityReplay.Run(args.Last());return;}
if(args.Contains("--map-audit")){RoomMapAudit.Run(args.Last());return;}
if(args.Contains("--pose-series")){PoseReview.Analyze(args.Last());return;}
if(args.Contains("--pose-review")){PoseReview.Run(args[1],double.Parse(args[2],System.Globalization.CultureInfo.InvariantCulture),args[3],!args.Contains("--cpu"),args.Contains("--native-roi"),args.Contains("--expanded-roi"));return;}
if(args.Contains("--test-video")){using var frame=new OpenCvSharp.Mat(180,320,OpenCvSharp.MatType.CV_8UC3,new OpenCvSharp.Scalar(40,90,130));var bytes=frame.ToBytes(".jpg");var packet=new FeedFrame(bytes,DateTime.UtcNow,DateTime.UtcNow);var pose=new Pose(0,0,100);using(var video=new RoomTestVideo(args.Last())){for(int i=0;i<15;i++){video.Push(packet,packet,new(i*100,"move",1,.8,.5,.5,30,10,1,.1,.05,30,pose,pose,pose,2,Anchor:"pose"));Thread.Sleep(100);}if(video.Error!=null)throw new Exception(video.Error);}using var cap=new OpenCvSharp.VideoCapture(args.Last());if(!cap.IsOpened()||cap.FrameCount<8||cap.FrameWidth!=960||cap.FrameHeight!=360)throw new Exception("Diagnostic test video did not finalize valid frames");Console.WriteLine($"PASS: local test video finalized {cap.FrameCount} paired overlay frames; no camera used");return;}

static void Assert(bool condition,string text){if(!condition)throw new Exception(text);}
SceneTests.Run();
VideoMotionReview.Verify();
HandoffAlignmentTests.Run();
HeadFramingTests.Run();
FeedbackControlTests.Run();
var guidePoints=new[]{new RoomPoint(.2,.2,0,0),new RoomPoint(.8,.2,0,0),new RoomPoint(.8,.8,0,0),new RoomPoint(.2,.8,0,0)};
var guideCopy=guidePoints.ToArray();var collectionGuide=RoomCollectionGuide.Create(guidePoints);
Assert(collectionGuide.Next is {} gap&&Math.Abs(gap.X-.5)<.02&&Math.Abs(gap.Y-.5)<.02,"Collection guide missed the largest measured gap");
Assert(guidePoints.SequenceEqual(guideCopy),"Collection guide changed saved measurements");
Assert(RoomCollectionGuide.Create([new(.2,.2,0,0),new(.4,.4,0,0),new(.8,.8,0,0)]).Next==null,"Collinear measurements produced misleading coverage");
Assert(RoomCollectionGuide.Create(Enumerable.Range(0,25).Select(i=>guidePoints[i%4]).ToArray()).Next==null,"Collection guide exceeded 25-point capture limit");
var triangleGuide=RoomCollectionGuide.Create([new(.2,.2,0,0),new(.8,.2,0,0),new(.2,.8,0,0)]);
Assert(triangleGuide.Next is {} triangleGap&&triangleGap.X>=.2-1e-6&&triangleGap.Y>=.2-1e-6&&triangleGap.X+triangleGap.Y<=1+1e-6,"Collection guide suggested outside measured hull");
Console.WriteLine("PASS: collection guide finds gaps, respects coverage and capacity, preserves measurements");
var weakTorso=new BodyResult([new(100,40,200,280,200,150,.42)],640,360,"fixture",0,AnchorKind:"pose");
Assert(!LinkLock.HasUsableTorso(weakTorso),"Default score threshold changed");
TrackingConfidence.Minimum=.35;Assert(LinkLock.HasUsableTorso(weakTorso),"Lenient score threshold rejected valid weak torso");
Assert(!LinkLock.HasUsableTorso(weakTorso with{AnchorKind="missing"}),"Lenient score bypassed missing torso guard");
Assert(!LinkLock.HasUsableTorso(weakTorso with{People=[weakTorso.People[0],weakTorso.People[0]]}),"Lenient score bypassed ambiguity guard");
TrackingConfidence.Minimum=double.NaN;Assert(TrackingConfidence.Minimum==.5,"Invalid score threshold was not reset");
Console.WriteLine("PASS: confidence adjustment preserves default, torso and ambiguity guards");
var upperJoints=Enumerable.Range(0,33).Select(_=>new Joint(0,0,0,0)).ToArray();
upperJoints[0]=new(200,80,.9,.9);upperJoints[11]=new(150,140,.9,.9);upperJoints[12]=new(250,140,.9,.9);
var seatedBody=new PersonBox(120,45,160,150,200,100,.8);
var upperAnchor=HeadShoulderAnchor.Select(upperJoints,.9,640,360,seatedBody);
Assert(upperAnchor is {} upperPosition&&upperPosition.Y>80&&upperPosition.Y<140,"Head/shoulders without hips did not yield upper-body anchor");
Assert(LinkLock.HasUsableTorso(new([seatedBody with{CenterX=upperAnchor!.Value.X,CenterY=upperAnchor.Value.Y}],640,720,"fixture",0,AnchorKind:"upper-body")),"Head/shoulders lock retained full torso height requirement");
upperJoints[11]=new(180,55,.9,.9);upperJoints[12]=new(220,55,.9,.9);
Assert(HeadShoulderAnchor.Select(upperJoints,.9,640,360,seatedBody)==null,"Face-like shoulder hallucination accepted");
upperJoints[11]=new(150,140,.9,.9);upperJoints[12]=new(250,140,.2,.2);
Assert(HeadShoulderAnchor.Select(upperJoints,.9,640,360,seatedBody)==null,"Missing shoulder accepted as reliable pair");
Console.WriteLine("PASS: seated head/shoulders lock without hips; false shoulder and missing shoulder rejection");
Assert(MediaFoundationDevices.PhysicalPath("@device:pnp:\\\\?\\usb#vid_test#camera-a#{11111111-1111-1111-1111-111111111111}\\global")==MediaFoundationDevices.PhysicalPath("\\\\?\\usb#vid_test#camera-a#{22222222-2222-2222-2222-222222222222}\\global"),"Capture backend interface categories changed physical camera identity");
Assert(MediaFoundationDevices.PhysicalPath("\\\\?\\usb#vid_test#camera-a#{11111111-1111-1111-1111-111111111111}")!=MediaFoundationDevices.PhysicalPath("\\\\?\\usb#vid_test#camera-b#{11111111-1111-1111-1111-111111111111}"),"Different camera instances collapsed into one identity");
MotionAxis Axis(int start,int target,int min,int max,int step=1)=>new(){Start=start,Target=target,Min=min,Max=max,Step=step};
var points=MotionPlanner.Plan(Axis(7,22,-145,145),Axis(4,14,-90,100),Axis(117,167,100,400),1400);
Assert(points[0].Pan==7&&points[^1].Pan==22&&points[^1].Tilt==14&&points[^1].Zoom==167,"Endpoint mismatch");
Assert(points.Zip(points.Skip(1)).All(p=>p.First.Pan<=p.Second.Pan&&p.First.Tilt<=p.Second.Tilt&&p.First.Zoom<=p.Second.Zoom&&p.First.AtMs<p.Second.AtMs),"Non-monotonic motion");
Assert(points[1].Pan-points[0].Pan<=points[points.Count/2].Pan-points[points.Count/2-1].Pan,"Easing missing");
var stepped=MotionPlanner.Plan(Axis(0,10,-10,10,2),Axis(0,0,-10,10,2),Axis(100,150,100,400,5),1000);
Assert(stepped.All(p=>p.Pan%2==0&&(p.Zoom-100)%5==0),"Step alignment failed");
bool rejected=false;try{MotionPlanner.Plan(Axis(0,146,-145,145),Axis(0,0,-90,100),Axis(100,100,100,400),1400);}catch(ArgumentException){rejected=true;}
Assert(rejected,"Out-of-range motion accepted");
var longMotion=MotionPlanner.Plan(Axis(-145,145,-145,145),Axis(-90,100,-90,100),Axis(100,400,100,400),500);
Assert(longMotion[^1].AtMs>=5437&&longMotion[^1].AtMs<=6000,"Trajectory speed bound failed");
var shortMotion=MotionPlanner.Plan(Axis(0,5,-145,145),Axis(0,5,-90,100),Axis(127,127,100,400),150);
Assert(shortMotion[^1].AtMs==150&&shortMotion[^1].Pan==5&&shortMotion[^1].Tilt==5,"Short tracking trajectory failed");
var fastMotion=MotionPlanner.Plan(Axis(0,3,-145,145),Axis(0,3,-90,100),Axis(127,127,100,400),75);
Assert(fastMotion[^1].AtMs==75&&fastMotion[^1].Pan==3,"Fast tracking trajectory failed");
Assert(fastMotion.Count==4&&fastMotion.Zip(fastMotion.Skip(1)).All(p=>p.Second.Pan-p.First.Pan<=1),"Fast correction cadence skipped adjacent pan steps");
Console.WriteLine("PASS: eased endpoints, monotonicity, quantization, invalid ranges, bounded command rates");
var servo=new ServoAxis(0,-145,145,1,80,320);int lastServo=0;double lastVelocity=0;
for(int i=0;i<20;i++){int next=servo.Tick(30,.025);Assert(next>=lastServo&&next-lastServo<=2,"Continuous servo skipped rate bound");Assert(Math.Abs(servo.Velocity-lastVelocity)<=8.001,"Continuous acceleration unbounded");lastServo=next;lastVelocity=servo.Velocity;}
servo.Hold(lastServo);Assert(servo.Velocity==0,"Servo hold retained velocity");
bool servoBounds=false;try{servo.Tick(146,.025);}catch(ArgumentException){servoBounds=true;}Assert(servoBounds,"Continuous servo accepted out-of-range target");
var reversing=new ServoAxis(0,-145,145,1,80,320);for(int i=0;i<8;i++)reversing.Tick(30,.025);double reversalStart=reversing.Position,priorVelocity=reversing.Velocity;reversing.Tick(-30,.025);Assert(reversing.Position>=reversalStart&&Math.Abs(reversing.Velocity-priorVelocity)<=8.001,"Target reversal snapped against current velocity");
bool nativeGate=false;try{CameraProbe.NativeTracking(true,.5,.5);}catch(InvalidOperationException){nativeGate=true;}Assert(nativeGate,"Unverified native activation escaped the gate");
Assert(RelativeGoal.Resolve(20,5,-145,145,1)==25,"Relative fine correction used stale camera snapshot");
Assert(RelativeGoal.Resolve(144,5,-145,145,1)==145,"Relative correction exceeded range");
bool relativeRejected=false;try{RelativeGoal.Resolve(0,6,-145,145,1);}catch(ArgumentException){relativeRejected=true;}Assert(relativeRejected,"Oversized fine correction accepted");
bool staleDriverRejected=false;try{CameraSnapshotGuard.Validate(new(){SnapshotUtcMilliseconds=1000},1501);}catch(CameraWorkerException){staleDriverRejected=true;}Assert(staleDriverRejected,"Stale continuous driver snapshot accepted");CameraSnapshotGuard.Validate(new(){SnapshotUtcMilliseconds=1000},1100);
Console.WriteLine("PASS: continuous servo speed, acceleration, quantization and hold bounds");

var rpc=new FakeObs();await using var route=new ObsVideoRoute(rpc);
await route.StartShared(default);Assert(rpc.Shared,"Virtual camera not started");
await route.Check(true,default);await route.Check(true,default);
Assert(rpc.Toggles==0,"Fresh frames caused unnecessary recovery");
rpc.Frozen=true;
for(int i=0;i<6;i++)await route.Check(true,default);
Assert(rpc.Toggles==2&&rpc.Active,"Frozen source recovery did not restore active state");
rpc.Active=false;int previous=rpc.Toggles;
await route.Check(true,default);Assert(rpc.Toggles==previous,"Disabled source was reactivated automatically");
rpc.Active=true;rpc.Showing=false;
await route.Check(true,default);Assert(rpc.Toggles==previous,"Hidden source was reactivated");
rpc.Showing=true;rpc.Verified=false;
rejected=false;try{await route.StartShared(default);}catch(IOException){rejected=true;}
Assert(rejected,"Unverified camera source accepted");
Console.WriteLine("PASS: shared route, frozen-frame recovery, disabled/hidden-source respect, hardware selection");
var stalled=new FakeObs{Frozen=true};var now=new DateTime(2026,10,14,0,0,0,DateTimeKind.Utc);
await using var bounded=new ObsVideoRoute(stalled,()=>now);
for(int i=0;i<30;i++){await bounded.Check(true,default);now=now.AddSeconds(12);}
Assert(stalled.Toggles==6&&stalled.Active,"Frozen-video retries were not bounded to three restarts");
var reconnect=new FakeObs();await using var recovering=new ObsVideoRoute(reconnect);
await recovering.Check(false,default);Assert(reconnect.Toggles==0,"Disconnected camera triggered capture commands");
await recovering.Check(true,default);Assert(reconnect.Toggles==2&&reconnect.Active,"Reconnect did not recover capture");
Console.WriteLine("PASS: three-retry limit, no commands while absent, capture recovery after reconnect");
var trackingState=new CameraReading{Controls=new(){new(){Name="Pan",Min=-145,Max=145,Step=1,Capabilities=2,Value=0},new(){Name="Tilt",Min=-90,Max=100,Step=1,Capabilities=2,Value=0},new(){Name="Zoom",Min=100,Max=400,Step=1,Capabilities=2,Value=127}}};
var planner=new TrackingPlanner();
FaceResult Face(double x,double y)=>new([new(x,y,60,60,0.95)],640,360);
Assert(planner.Plan(Face(480,210),trackingState,false,false)==null,"Tracker moved before stable acquisition");
var correction=planner.Plan(Face(480,210),trackingState,false,false)!;
Assert(correction.Pan>0&&correction.Pan<=5&&correction.Tilt<0&&correction.Tilt>=-5&&correction.Zoom==127,"Tracking direction/bounds/zoom preservation failed");
planner.Reset();planner.Plan(Face(480,210),trackingState,true,true);
correction=planner.Plan(Face(480,210),trackingState,true,true)!;
Assert(correction.Pan<0&&correction.Tilt>0,"Reverse directions failed");
Assert(planner.Plan(new([],640,360),trackingState,false,false)==null,"No-face movement");
Assert(planner.Plan(new([new(480,210,60,60,0.95),new(10,10,60,60,0.95)],640,360),trackingState,false,false)==null,"Multiple-face movement");
Assert(planner.Plan(Face(290,150),trackingState,false,false)==null&&planner.Plan(Face(290,150),trackingState,false,false)==null,"Center deadband failed");
Assert(planner.Plan(Face(double.NaN,150),trackingState,false,false)==null,"Invalid detection accepted");
planner.Reset();trackingState.Controls[0].Value=145;planner.Plan(Face(480,150),trackingState,false,false);
Assert(planner.Plan(Face(480,150),trackingState,false,false)==null,"Tracking exceeded hardware range");
Console.WriteLine("PASS: stable face acquisition, centering directions, deadband, no/multiple/invalid face hold, bounded corrections and unchanged zoom");
RoomPoint[] roomPoints=[new(0.2,0.3,-12,-4),new(0.8,0.3,12,-4),new(0.2,0.7,-12,4),new(0.8,0.7,12,4)];
var roomMap=RoomMap.Fit(roomPoints);trackingState.Controls[0].Value=0;
var mapped=roomMap.Target(0.6,0.6,trackingState)!;
Assert(mapped.Pan==4&&mapped.Tilt==2&&mapped.Zoom==127,"Room coordinate mapping failed");
Assert(roomMap.Target(0.95,0.5,trackingState)==null,"Room map extrapolated beyond calibrated area");
Assert(Math.Abs(roomMap.Target(0.8,0.7,trackingState)!.Pan)<=5,"Room mapping correction exceeded limit");
Assert(roomMap.Target(.8,.7,trackingState,bounded:false)!.Pan==12,"Continuous room target depended on stale current pose");
rejected=false;try{RoomMap.Fit([new(0.2,0.5,0,0),new(0.4,0.5,4,0),new(0.6,0.5,8,0),new(0.8,0.5,12,0)]);}catch(IOException){rejected=true;}
Assert(rejected,"Degenerate room map accepted");
rejected=false;try{RoomMap.Fit([..roomPoints,new(0.5,0.5,60,60)]);}catch(IOException){rejected=true;}
Assert(rejected,"Inconsistent room map accepted");
Console.WriteLine("PASS: room coordinate fit, no extrapolation, bounded movement, invalid/degenerate mapping rejection");
Assert(HybridGuidance.Select(1,50,0,50)==GuidanceSource.Room,"Lost Link target did not use room guidance");
Assert(HybridGuidance.Select(1,50,1,50)==GuidanceSource.Link,"Visible Link target did not use fine tracking");
Assert(HybridGuidance.Select(1,50,1,900)==GuidanceSource.Room,"Stale Link target prevented reacquisition");
Assert(HybridGuidance.Select(2,50,1,50)==GuidanceSource.Hold&&HybridGuidance.Select(1,50,2,50)==GuidanceSource.Hold,"Multiple subjects commanded tracking");
Assert(HybridGuidance.Select(0,50,1,50)==GuidanceSource.Link,"Acer loss stopped a reliable Link torso");
Assert(HybridGuidance.Select(1,900,1,50)==GuidanceSource.Link,"Stale Acer blocked fresh Link tracking");
Assert(HybridGuidance.Select(0,50,0,50)==GuidanceSource.Hold,"Both cameras lost subject but still commanded motion");
Assert(!LinkLock.HasUsableTorso(new([new PersonBox(220,140,40,40,240,156,.9)],320,180,"test",0)),"Head fragment at frame edge accepted as torso lock");
Assert(LinkLock.HasUsableTorso(new([new PersonBox(90,20,120,160,150,84,.8)],320,180,"test",0)),"Visible torso rejected");
RoomPoint[] nonlinear=[new(.2,.3,-12,-4),new(.8,.3,12,-4),new(.2,.7,-12,4),new(.8,.7,12,4),new(.5,.5,0,14)];
var mesh=RoomMap.Fit(nonlinear);trackingState.Controls.Single(c=>c.Name=="Tilt").Value=14;
Assert(mesh.Target(.5,.5,trackingState)!.Tilt==14,"Mesh did not reproduce non-linear interior capture");
Assert(mesh.Target(.1,.2,trackingState)==null,"Mesh extrapolated outside captured hull");
Console.WriteLine("PASS: nonlinear captured-point mesh and Link-loss/stale/ambiguous hybrid guidance");
var handoffCheck=new HybridHandoff();var handoffTime=DateTime.UtcNow;
Assert(handoffCheck.Select(1,20,1,20,handoffTime)==GuidanceSource.Hold,"Single Link glimpse acquired tracking");
Assert(handoffCheck.Select(1,20,1,20,handoffTime.AddMilliseconds(60))==GuidanceSource.Hold,"Handoff acquired before stability window");
Assert(handoffCheck.Select(1,20,1,20,handoffTime.AddMilliseconds(150))==GuidanceSource.Link,"Stable Link did not acquire");
Assert(handoffCheck.Select(1,20,0,20,handoffTime.AddMilliseconds(190))==GuidanceSource.Hold,"Transient miss swung camera to map");
Assert(handoffCheck.Select(1,20,0,20,handoffTime.AddMilliseconds(360))==GuidanceSource.Room,"Sustained loss did not reacquire from room");
if(args.Contains("--saved-map")){
 var saved=System.Text.Json.JsonSerializer.Deserialize<Settings>(File.ReadAllText("src/desktop/bin/Release/net9.0-windows/settings.local.json"),Link360Desktop.Program.Json)!;var actualMap=RoomMap.Fit(saved.RoomPoints);
 foreach(var p in saved.RoomPoints){trackingState.Controls.Single(c=>c.Name=="Pan").Value=p.Pan;trackingState.Controls.Single(c=>c.Name=="Tilt").Value=p.Tilt;var t=actualMap.Target(p.X,p.Y,trackingState);Assert(t!=null&&t.Pan==p.Pan&&t.Tilt==p.Tilt,"Saved point was not reproduced by mesh");}
 Console.WriteLine($"PASS: actual {saved.RoomPoints.Length}-point map accepted and every saved PTZ position reproduced");
 trackingState.Controls.Single(c=>c.Name=="Pan").Value=0;
}
var joints=Enumerable.Range(0,39).Select(_=>new Joint(128,128,0.99,0.99)).ToArray();
joints[11]=new(100,100,0.99,0.99);joints[12]=new(140,100,0.99,0.99);joints[13]=new(70,100,0.99,0.99);joints[14]=new(170,100,0.99,0.99);joints[15]=new(40,100,0.99,0.99);joints[16]=new(200,100,0.99,0.99);
Assert(ArmsGesture.IsOut(joints,0.95),"T-pose rejected");
var lowered=(Joint[])joints.Clone();lowered[15]=new(100,170,0.99,0.99);Assert(!ArmsGesture.IsOut(lowered,0.95),"Lowered arm accepted");
Assert(!ArmsGesture.IsOut(joints,0.4),"Low-confidence pose accepted");
var invisible=(Joint[])joints.Clone();invisible[16]=new(200,100,0.2,0.99);Assert(!ArmsGesture.IsOut(invisible,0.95),"Invisible wrist accepted");
var hold=new GestureHold();var startHold=DateTime.UtcNow;
Assert(!hold.Tick(true,0.5,0.5,startHold)&&!hold.Tick(true,0.5,0.5,startHold.AddSeconds(1))&&hold.Tick(true,0.5,0.5,startHold.AddSeconds(2)),"Gesture hold trigger failed");
Assert(!hold.Tick(true,0.5,0.5,startHold.AddSeconds(10)),"Held gesture retriggered");
hold.Tick(false,0.5,0.5,startHold.AddSeconds(11));hold.Tick(false,0.5,0.5,startHold.AddSeconds(12));
Assert(!hold.Tick(true,0.7,0.5,startHold.AddSeconds(13))&&hold.Tick(true,0.7,0.5,startHold.AddSeconds(15)),"Gesture release/rearm failed");
Console.WriteLine("PASS: arms-out pose, lowered/occluded/low-confidence rejection, hold timing and release-to-rearm");
var still=new StillnessCapture();
Assert(!still.Tick(true,.3,.4,startHold)&&!still.Tick(true,.3,.4,startHold.AddSeconds(1))&&still.Tick(true,.3,.4,startHold.AddSeconds(2)),"Stillness capture timing failed");
Assert(!still.Tick(true,.3,.4,startHold.AddSeconds(6)),"Same position recaptured");
Assert(!still.Tick(true,.6,.4,startHold.AddSeconds(7))&&!still.Tick(true,.6,.4,startHold.AddSeconds(8))&&still.Tick(true,.6,.4,startHold.AddSeconds(9)),"New position did not rearm");
var interrupted=new StillnessCapture();interrupted.Tick(true,.4,.4,startHold);interrupted.Tick(false,.4,.4,startHold.AddSeconds(1));Assert(!interrupted.Tick(true,.4,.4,startHold.AddSeconds(2)),"Lost body accumulated hold time");
Console.WriteLine("PASS: stillness capture, new-position rearm and missing-body reset");
var centering=new CenteringPolicy(new Pose(20,12,100));centering.Check(new Pose(-10,12,100),new Pose(-15,12,100));centering.Check(new Pose(-17,6,100),new Pose(-22,6,100));
for(int i=0;i<12;i++)centering.Observe(.5-i*.025,.1);
bool stalledRejected=false;try{for(int i=0;i<8;i++)centering.Observe(.6,.1);}catch(IOException){stalledRejected=true;}Assert(stalledRejected,"Non-improving centering allowed continuous movement");
bool travelRejected=false;try{new CenteringPolicy(new Pose(0,0,100)).Check(new Pose(60,0,100),new Pose(65,0,100));}catch(IOException){travelRejected=true;}Assert(travelRejected,"Centering travel bound exceeded");
Console.WriteLine("PASS: room-width centering travel and stalled/wrong-way movement rejection");
var diagnosticDir=Path.Combine(Path.GetFullPath(".rea-tools"),"diagnostic-fixture-"+Guid.NewGuid().ToString("N"));
var diagnosticPoints=new RoomPoint[]{new(.2,.3,-10,-5),new(.8,.3,10,-5),new(.2,.7,-10,5),new(.8,.7,10,5)};
using(var diagnostic=new RoomMapTest(diagnosticDir,diagnosticPoints)){
 var p=new Pose(0,0,100);diagnostic.Record(new(250,"move",1,.8,.5,.5,25,12,1,.1,.05,25,p,new Pose(1,0,100),new Pose(1,0,100),90));
 diagnostic.Record(new(500,"holding",0,null,null,null,20,12,0,null,null,null,p,null,p,null));
 var summaryPath=diagnostic.Finish("manual override");using var summary=System.Text.Json.JsonDocument.Parse(File.ReadAllText(summaryPath));
 Assert(summary.RootElement.GetProperty("samples").GetInt32()==2&&summary.RootElement.GetProperty("roomMissingOrAmbiguous").GetInt32()==1,"Diagnostic sample/hold counts wrong");
 Assert(summary.RootElement.GetProperty("reason").GetString()=="manual override","Interrupted test lost stop reason");
 Assert(File.ReadAllLines(diagnostic.LogPath).Length==4,"Diagnostic start/sample/summary records missing");
}
bool tooMany=false;try{RoomMap.Fit(Enumerable.Range(0,26).Select(i=>diagnosticPoints[i%4]).ToArray());}catch(IOException){tooMany=true;}Assert(tooMany,"Map accepted more than twenty-five points");
RoomMap.Fit(Enumerable.Range(0,25).Select(i=>diagnosticPoints[i%4]).ToArray());
Console.WriteLine("PASS: local numeric test logs, missing-target/interruption summary and 4–25 point limits");
var torsoPlan=new TrackingPlanner();var torso=new BodyResult([new PersonBox(50,40,110,130,105,90,.6)],320,180,"test",0);
Assert(torsoPlan.PlanBody(torso,trackingState,false,false)==null,"Body target moved before stable acquisition");
var torsoTarget=torsoPlan.PlanBody(torso,trackingState,false,false);
Assert(torsoTarget!=null&&torsoTarget.Pan<0&&torsoTarget.Zoom==trackingState.Controls.Single(c=>c.Name=="Zoom").Value,"Off-center body correction/zoom preservation failed");
Assert(torsoPlan.PlanBody(torso with {People=[]},trackingState,false,false)==null,"Missing body commanded motion");
var anchorJoints=Enumerable.Range(0,39).Select(_=>new Joint(0,0,0,0)).ToArray();anchorJoints[11]=new(100,80,.9,.9);anchorJoints[12]=new(160,80,.9,.9);anchorJoints[23]=new(110,180,.9,.9);anchorJoints[24]=new(150,180,.9,.9);
Assert(TorsoAnchor.Select(anchorJoints,.9,320,240)==(130d,120d),"Chest anchor did not follow shoulders/hips");anchorJoints[15]=new(0,0,.9,.9);anchorJoints[16]=new(319,0,.9,.9);
Assert(TorsoAnchor.Select(anchorJoints,.9,320,240)==(130d,120d),"Arm movement displaced torso anchor");anchorJoints[11]=anchorJoints[11] with{Visibility=.2};Assert(TorsoAnchor.Select(anchorJoints,.9,320,240)==null,"Occluded shoulders supplied a torso lock");
var hybridPolicy=new HybridTrackingPlanner(roomMap);var policyTime=DateTime.UtcNow;
var roomBody=new BodyResult([new PersonBox(150,40,200,220,384,216,.9)],640,360,"test",0);
var primary=torso with{AnchorKind="pose"};TrackingDecision policyDecision=hybridPolicy.Evaluate(null,policyTime,primary,policyTime,trackingState,false,false,policyTime);
for(int i=1;i<=7;i++){var at=policyTime.AddMilliseconds(i*40);policyDecision=hybridPolicy.Evaluate(null,at,primary,at,trackingState,false,false,at);}
Assert(policyDecision.Source==GuidanceSource.Link&&policyDecision.RelativePan<0,"Primary tracker failed without Acer");
var lostAt=policyTime.AddMilliseconds(320);hybridPolicy.Evaluate(roomBody,lostAt,null,lostAt,trackingState,false,false,lostAt);
var recoveryAt=policyTime.AddMilliseconds(520);policyDecision=hybridPolicy.Evaluate(roomBody,recoveryAt,null,recoveryAt,trackingState,false,false,recoveryAt);
Assert(policyDecision.Source==GuidanceSource.Room&&policyDecision.Target!=null&&policyDecision.RelativePan==null,"Loss did not produce an absolute mapped recovery target");
var outsideAt=policyTime.AddMilliseconds(560);policyDecision=hybridPolicy.Evaluate(roomBody with{People=[roomBody.People[0] with{CenterX=640*.95}]},outsideAt,null,outsideAt,trackingState,false,false,outsideAt);
Assert(policyDecision.Hold&&policyDecision.Target==null,"Filtered coordinates extrapolated a raw out-of-map detection");
var duplicatePolicy=new HybridTrackingPlanner(roomMap);var duplicated=duplicatePolicy.Evaluate(null,policyTime,primary,policyTime,trackingState,false,false,policyTime);for(int i=1;i<=5;i++)duplicated=duplicatePolicy.Evaluate(null,policyTime,primary,policyTime,trackingState,false,false,policyTime.AddMilliseconds(i*40));Assert(duplicated.Source==GuidanceSource.Hold,"Duplicate Link frame qualified a torso lock");
Console.WriteLine("PASS: shoulder/hip torso anchors, primary tracking without Acer, absolute recovery and no extrapolation");
var syncNow=DateTime.UtcNow;var syncBody=new BodyResult([new PersonBox(90,0,140,180,160,90,.9)],320,180,"test",0){AnchorKind="pose"};
var syncOriginal=trackingState.Controls.Select(c=>c.Value).ToArray();var predicted=roomMap.Target(.5,.5,trackingState,bounded:false)!;
trackingState.Controls.Single(c=>c.Name=="Pan").Value=predicted.Pan+2;trackingState.Controls.Single(c=>c.Name=="Tilt").Value=predicted.Tilt-3;
var syncSamples=Enumerable.Range(0,5).Select(i=>new RoomSyncObservation(syncBody,syncNow.AddMilliseconds(-600+120*i),syncBody,syncNow.AddMilliseconds(-600+120*i))).ToArray();
var aligned=RoomAlignment.Sync(roomMap,syncSamples,trackingState,syncNow);Assert(aligned==new RoomAlignment(2,-3)&&aligned.Apply(predicted,trackingState)==(predicted with{Pan=predicted.Pan+2,Tilt=predicted.Tilt-3}),"Sync did not reproduce the centred subject position");
Assert(roomMap.Target(.5,.5,trackingState,bounded:false)==predicted,"Sync changed original map coordinates");
bool SyncRejected(RoomSyncObservation[] observations){try{RoomAlignment.Sync(roomMap,observations,trackingState,syncNow);return false;}catch(IOException){return true;}}
Assert(SyncRejected(syncSamples.Select(o=>o with{RoomAt=o.RoomAt.AddSeconds(-3)}).ToArray()),"Sync accepted stale frames");
Assert(SyncRejected(syncSamples.Select(o=>o with{RoomAt=syncSamples[0].RoomAt}).ToArray()),"Sync accepted duplicate room frames");
var offCentre=syncBody with{People=[syncBody.People[0] with{CenterX=200}]};Assert(SyncRejected(syncSamples.Select(o=>o with{Link=offCentre}).ToArray()),"Sync accepted an off-centre torso");
trackingState.Controls.Single(c=>c.Name=="Pan").Value=predicted.Pan+11;Assert(SyncRejected(syncSamples),"Sync accepted excessive alignment correction");
Assert(new RoomAlignment(-10,0).Apply(predicted with{Pan=trackingState.Controls.Single(c=>c.Name=="Pan").Min},trackingState)==null,"Sync bypassed camera range");
var falsePose=Enumerable.Range(0,39).Select(_=>new Joint(0,0,0,0)).ToArray();falsePose[11]=new(160,78,.99,.99);falsePose[12]=new(193,76,.99,.99);falsePose[23]=new(170,110,.99,.99);falsePose[24]=new(186,108,.99,.99);
Assert(TorsoAnchor.Select(falsePose,.77,320,180,new PersonBox(126,51,162,126,207,102,.87))==null,"Confident head-sized pose accepted as a torso");
for(int i=0;i<syncOriginal.Length;i++)trackingState.Controls[i].Value=syncOriginal[i];
Console.WriteLine("PASS: session alignment, unchanged room map, stale/duplicate/off-centre/excessive offset rejection and range bounds");
var quietPlanner=new TrackingPlanner();DateTime noiseStart=DateTime.UtcNow;int noiseCommands=0;
BodyResult Measured(double x)=>new([new PersonBox(100,20,120,160,x*320,90,.9)],320,180,"test",0);
for(int i=0;i<300;i++)if(quietPlanner.PlanBody(Measured(.5+.045*Math.Sin(i*.9)),trackingState,false,false,noiseStart.AddSeconds(i/30.0))!=null)noiseCommands++;
Assert(noiseCommands==0,"Quiet detection noise commanded camera movement");
quietPlanner.Reset();for(int i=0;i<15;i++)quietPlanner.PlanBody(Measured(.5),trackingState,false,false,noiseStart.AddSeconds(i/30.0));int fastResponse=-1;
for(int i=0;i<6;i++)if(quietPlanner.PlanBody(Measured(.64),trackingState,false,false,noiseStart.AddSeconds((15+i)/30.0))!=null&&fastResponse<0)fastResponse=i;
Assert(fastResponse>=0&&fastResponse<=2&&quietPlanner.SpeedScale>.15,"Fast motion was delayed by quiet smoothing");
Console.WriteLine($"PASS: zero stationary-noise corrections; fast movement responds within {(fastResponse+1)*33}ms in planner fixture");
if(args.Contains("--live-engine")) {
 var engine=new CameraEngine(Path.GetFullPath("src/desktop/bin/Release/net9.0-windows/LINK360.exe"));
 var start=await engine.Execute(new(),default);
 int Get(CameraReading r,string name)=>r.Controls.Single(c=>c.Name==name).Value;
 var pose=new Pose(Get(start,"Pan"),Get(start,"Tilt"),Get(start,"Zoom"));
 var target=pose with{Zoom=pose.Zoom<=380?pose.Zoom+20:pose.Zoom-20};
 try {
  using(var trackingCancel=new CancellationTokenSource()) {
   await using var session=new LocalCameraSession(trackingCancel.Token,Path.GetFullPath("src/desktop/bin/Release/net9.0-windows/LINK360.exe"));
   var warm=await engine.Execute(new(),default,session);var clock=System.Diagnostics.Stopwatch.StartNew();
   var small=pose with{Pan=pose.Pan<=140?pose.Pan+3:pose.Pan-3};
   await engine.Execute(new(Target:small,DurationMs:75),default,session);
   Console.WriteLine($"PASS: reusable camera worker, 75ms correction roundtrip {clock.ElapsedMilliseconds}ms");
   trackingCancel.Cancel();bool blocked=false;
   try{await engine.Execute(new(),trackingCancel.Token,session);}catch(OperationCanceledException){blocked=true;}
   Assert(blocked,"Cancelled tracking session accepted another request");
  }
  await engine.Execute(new(Target:pose,DurationMs:500),default);
  using var cancel=new CancellationTokenSource(500);
  bool stopped=false;
  try{await engine.Execute(new(Target:target,DurationMs:3000),cancel.Token);}catch(OperationCanceledException){stopped=true;}
  Assert(stopped,"Engine cancellation did not stop the worker");
  var after=await engine.Execute(new(),default);
  Assert(Get(after,"Zoom")!=target.Zoom,"Cancelled worker reached target");
  Console.WriteLine("PASS: live CameraEngine cancellation, worker cleanup, subsequent read");
 }finally{await engine.Execute(new(Target:pose,DurationMs:1400),default);}
}

sealed class FakeObs : IObsRpc {
 public bool Shared,Active=true,Showing=true,Frozen,Verified=true;
 public int Toggles,Counter;
 public Task<JsonElement> Call(string type,object? data,CancellationToken ct) {
  ct.ThrowIfCancellationRequested();
  object result=type switch {
   "GetInputSettings"=>new{inputKind="dshow_input",inputSettings=new{active=Active,video_device_id=Verified?"vid_2e1a&pid_4c01":"other"}},
   "GetSourceActive"=>new{videoShowing=Showing,videoActive=true},
   "GetVirtualCamStatus"=>new{outputActive=Shared},
   "GetSourceScreenshot"=>new{imageData="data:image/png;base64,"+Convert.ToBase64String(BitConverter.GetBytes(Frozen?99:++Counter))},
   "StartVirtualCam"=>Start(),
   "PressInputPropertiesButton"=>Toggle(),
   _=>throw new Exception(type)
  };
  return Task.FromResult(JsonSerializer.SerializeToElement(result));
 }
 object Start(){Shared=true;return new{};}
 object Toggle(){Active=!Active;Toggles++;return new{};}
 public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
}
