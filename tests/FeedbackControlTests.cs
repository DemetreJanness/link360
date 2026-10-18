using Link360;
using Link360Desktop;
internal static class FeedbackControlTests {
 public static void Run(){
  void Check(bool ok,string why){if(!ok)throw new Exception(why);}
  foreach(int latency in new[]{100,250,350,450})foreach(int frameMs in new[]{50,100}){
   var result=Simulate(latency,frameMs,false);
   Check(result.LateTravel<=2,$"Delayed feedback hunted at {latency}ms/{frameMs}ms: {result}");
   Check(result.FeedbackWaits>0,"Simulation did not exercise delayed-feedback coasting");
   Check(result.FinalError<.075,$"Delayed feedback failed to settle: {result}");
  }
  var motion=Simulate(300,50,true);
  Check(motion.LateTravel<=2&&motion.FinalError<.075,"Moving subject did not settle after delayed feedback: "+motion);
  Console.WriteLine("PASS: closed-loop delayed-image simulation settles without repeated hunting across 100–450ms delay and 10/20 FPS; moving target settles");
 }
 internal record Result(double FinalError,int LateTravel,int FeedbackWaits);
 static Result Simulate(int latency,int frameMs,bool moving){
  var planner=new TrackingPlanner();var servo=new ServoAxis(0,-90,90,1,60,240);var history=new List<(int Time,int Pose)>();int commanded=0,pending=0,lateTravel=0,previous=0,feedbackWaits=0;double scale=.15,error=0;bool hold=true;var epoch=new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc);
  double Subject(int time)=>moving?8+Math.Clamp((time-4000)/2000.0,0,1)*12:8;
  for(int time=0;time<16000;time+=25){
   history.Add((time,commanded));
   if(time%frameMs==0){
    int exposure=Math.Max(0,time-latency);var seen=history.Last(h=>h.Time<=exposure).Pose;
    error=(seen-Subject(exposure))*.021+Math.Sin(time*.017)*.006;
    var state=new CameraReading{CommandedPan=0,CommandedTilt=commanded,Controls=[new(){Name="Pan",Min=-145,Max=145,Step=1,Capabilities=2,Value=0},new(){Name="Tilt",Min=-90,Max=90,Step=1,Capabilities=2,Value=commanded},new(){Name="Zoom",Min=100,Max=400,Step=1,Capabilities=2,Value=100}]};
    var body=new BodyResult([new(60,10,200,165,160,(.5+error)*180,.95)],320,180,"simulation",0,AnchorKind:"head");
    var goal=planner.PlanBody(body,state,false,false,epoch.AddMilliseconds(time));hold=goal==null&&!planner.WaitingForFeedback;if(planner.WaitingForFeedback)feedbackWaits++;
    if(goal!=null){pending=RelativeGoal.Resolve(commanded,goal.Tilt-commanded,-90,90,1);scale=planner.SpeedScale;}
   }
   if(hold)servo.Hold(commanded);else commanded=servo.Tick(pending,.025,scale);
   if(time>=12000)lateTravel+=Math.Abs(commanded-previous);previous=commanded;
  }
  return new(Math.Abs((commanded-Subject(16000))*.021),lateTravel,feedbackWaits);
 }
}
