using Link360;
using Link360Desktop;
internal static class NativeSpeedTests {
 public static void Run(){
  void Check(bool ok,string message){if(!ok)throw new Exception(message);}
  Check(Convert.ToHexString(NativeSpeedPacket.Encode(4,0))=="01040001","Native right packet mismatch");
  Check(Convert.ToHexString(NativeSpeedPacket.Encode(-4,0))=="FF040001","Native left packet mismatch");
  Check(Convert.ToHexString(NativeSpeedPacket.Encode(0,4))=="0001FF04","Native up packet mismatch");
  Check(Convert.ToHexString(NativeSpeedPacket.Encode(0,-4))=="00010104","Native down packet mismatch");
  Check(Convert.ToHexString(NativeSpeedPacket.Encode(0,0))=="00010001","Native neutral packet mismatch");
  foreach(int invalid in new[]{11,-11,int.MinValue,int.MaxValue}){bool refused=false;try{NativeSpeedPacket.Encode(invalid,0);}catch(ArgumentOutOfRangeException){refused=true;}Check(refused,"Uncaptured speed admitted");}
  Check(NativeAngleFeedback.DriverUnits(40,-145,145)==4&&NativeAngleFeedback.DriverUnits(-85,-90,100)==-9,"Native angle units/rounding mismatch");bool poseRejected=false;try{NativeAngleFeedback.DriverUnits(1451,-145,145);}catch(ArgumentOutOfRangeException){poseRejected=true;}Check(poseRejected,"Out-of-range native feedback accepted");
  Check(Convert.ToHexString(NativeSpeedPacket.Encode(10,-10))=="010A010A","Native UI maximum encoding mismatch");
  var lease=new NativeSpeedLease();lease.Demand(4,-2,100);Check(lease.Read(399)==(4,-2)&&lease.Read(400)==(0,0)&&lease.Read(99)==(0,0),"Speed lease expiry/clock safety failed");lease.Hold();Check(lease.Read(200)==(0,0),"Explicit hold failed");
  var ramp=new NativeSpeedSlew();int previous=0;for(int time=0;time<=240;time+=20){int next=ramp.Tick(4,time);Check(next>=previous&&next-previous<=1,"Speed ramp skipped a level");previous=next;}Check(previous==4,"Fast catch-up ramp never reached captured speed");Check(ramp.Tick(-4,260)==0&&ramp.Tick(-4,280)==0&&ramp.Tick(-4,320)==-1,"Speed reversal failed neutral dwell");Check(ramp.Tick(0,330)==0,"Neutral stop was delayed by smoothing");var highRamp=new NativeSpeedSlew();for(int ms=0;ms<=600;ms+=60)highRamp.Tick(10,ms);Check(highRamp.Tick(4,620,4)==4,"Acer handoff retained a previous high Link speed");
  var planner=new NativeSpeedPlanner();var at=new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc);
  BodyResult Body(double x,double y=.5)=>new([new(60,10,220,165,x*320,y*180,.95)],320,180,"fixture",0,AnchorKind:"head");
  int quiet=0;for(int i=0;i<200;i++){var d=planner.Plan(Body(.5+.04*Math.Sin(i*.9)),at.AddMilliseconds(i*33),at.AddMilliseconds(i*33));if(d is {Pan:not 0}||d is {Tilt:not 0})quiet++;}Check(quiet==0,"Stationary noise enabled native speed");
  Check(NativeSpeedPlanner.Level(.05)==1&&NativeSpeedPlanner.Level(.1)==2&&NativeSpeedPlanner.Level(.2)==5&&NativeSpeedPlanner.Level(.3)==8&&NativeSpeedPlanner.Level(.45)==10,"Near-centre precision / far catch-up curve failed");
  planner.Reset();planner.Plan(Body(.8),at,at);var fast=planner.Plan(Body(.8),at.AddMilliseconds(50),at.AddMilliseconds(50));Check(fast?.Pan==8&&fast.Tilt==0,"Fast displacement did not request catch-up speed");Check(planner.Plan(Body(.8),at.AddMilliseconds(50),at.AddMilliseconds(75))==null,"Duplicate frame renewed speed lease");Check(planner.Plan(Body(.8),at.AddMilliseconds(50),at.AddMilliseconds(400)) is {Pan:0,Tilt:0,Valid:false},"Stale frame did not stop native speed");
  Check(planner.Plan(Body(.8) with{People=[]},at.AddMilliseconds(500),at.AddMilliseconds(500)) is {Pan:0,Tilt:0,Valid:false},"Lost target did not stop native speed");
  Console.WriteLine("PASS: captured native speed packets, bounded lease, ramp/reversal/neutral stop, quiet hold, catch-up demand and stale/duplicate/lost-frame safety");
 }
}
