#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
namespace Link360 {
 public class ServoAxis {
  public double Position,Velocity;readonly double speed,acceleration;readonly int min,max,step;
  public ServoAxis(int value,int min,int max,int step,double speed,double acceleration){if(step<=0||min>max||value<min||value>max)throw new ArgumentException("Invalid servo range");Position=value;this.min=min;this.max=max;this.step=step;this.speed=speed;this.acceleration=acceleration;}
  public int Tick(int target,double seconds,double speedScale=1){if(target<min||target>max)throw new ArgumentException("Servo target outside driver range");if(!double.IsFinite(speedScale)||speedScale<.15||speedScale>1)throw new ArgumentException("Invalid tracking speed scale");seconds=Math.Clamp(seconds,0,.05);double error=target-Position,desired=Math.Sign(error)*Math.Min(speed*speedScale,Math.Sqrt(2*acceleration*Math.Abs(error)));Velocity+=Math.Clamp(desired-Velocity,-acceleration*seconds,acceleration*seconds);double movement=Velocity*seconds;if(Math.Sign(movement)==Math.Sign(error)&&Math.Abs(movement)>Math.Abs(error)){Position=target;Velocity=0;}else Position=Math.Clamp(Position+movement,min,max);return (int)Math.Clamp(min+Math.Round((Position-min)/step)*step,min,max);}
  public void Hold(int position){Position=position;Velocity=0;}
 }
 public static class RelativeGoal {
  public static int Resolve(int commanded,int delta,int min,int max,int step){if(step<=0||Math.Abs((long)delta)>5||delta%step!=0)throw new ArgumentException("Relative tracking correction outside bounds");return Math.Clamp(commanded+delta,min,max);}
 }
 public static partial class CameraProbe {
  public static void ServeContinuous(Func<TrackingMotion> receive,Action<CameraReading> respond,bool limitedTravel=false){
   RequireOneCamera();var result=Inspect(false,null,0,3,(controls,reading)=>{
    var cache=new Dictionary<int,ControlReading>();
    foreach(var pair in new[]{Tuple.Create(0,"Pan"),Tuple.Create(1,"Tilt"),Tuple.Create(3,"Zoom")}){var c=new ControlReading{Name=pair.Item2};c.RangeHResult=controls.GetRange(pair.Item1,out c.Min,out c.Max,out c.Step,out c.Default,out c.Capabilities);c.GetHResult=controls.Get(pair.Item1,out c.Value,out c.Flags);Marshal.ThrowExceptionForHR(c.RangeHResult);Marshal.ThrowExceptionForHR(c.GetHResult);if((c.Capabilities&2)==0)throw new Exception("Manual tracking unavailable");cache[pair.Item1]=c;}
    foreach(var c in cache.Values)if(c.Step<=0||c.Min>c.Max||c.Value<c.Min||c.Value>c.Max)throw new Exception("Invalid tracking control range");
    int panSpan=20/cache[0].Step*cache[0].Step,tiltSpan=15/cache[1].Step*cache[1].Step;
    int panMin=limitedTravel?Math.Max(cache[0].Min,cache[0].Value-panSpan):cache[0].Min,panMax=limitedTravel?Math.Min(cache[0].Max,cache[0].Value+panSpan):cache[0].Max;
    int tiltMin=limitedTravel?Math.Max(cache[1].Min,cache[1].Value-tiltSpan):cache[1].Min,tiltMax=limitedTravel?Math.Min(cache[1].Max,cache[1].Value+tiltSpan):cache[1].Max;
    object gate=new();TrackingMotion pending=null;CameraReading snapshot=null;bool ended=false;Exception inputError=null;long inputTime=0;int commandedPan=cache[0].Value,commandedTilt=cache[1].Value;
    CameraReading Copy(){var copy=new CameraReading{Name=reading.Name,Operation="ContinuousTrackingSnapshot",SnapshotUtcMilliseconds=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()};foreach(var c in cache.Values)copy.Controls.Add(new ControlReading{Name=c.Name,Min=c.Min,Max=c.Max,Step=c.Step,Default=c.Default,Capabilities=c.Capabilities,Value=c.Value,Flags=c.Flags,GetHResult=c.GetHResult,RangeHResult=c.RangeHResult});return copy;}
    snapshot=Copy();var clock=Stopwatch.StartNew();
    var input=new Thread(()=>{try{while(true){var request=receive();if(request==null)break;CameraReading reply;lock(gate){if(request.Hold)pending=null;else if(request.DurationMs!=0){if(request.RelativePan.HasValue!=request.RelativeTilt.HasValue)throw new Exception("Both relative tracking axes required");if(request.RelativePan.HasValue){request.Pan=RelativeGoal.Resolve(commandedPan,request.RelativePan.Value,panMin,panMax,cache[0].Step);request.Tilt=RelativeGoal.Resolve(commandedTilt,request.RelativeTilt.Value,tiltMin,tiltMax,cache[1].Step);}foreach(var pair in new[]{Tuple.Create(0,request.Pan),Tuple.Create(1,request.Tilt),Tuple.Create(3,request.Zoom)}){var c=snapshot.Controls.Find(c=>c.Name==cache[pair.Item1].Name);if(pair.Item2<c.Min||pair.Item2>c.Max||((long)pair.Item2-c.Min)%c.Step!=0)throw new Exception("Continuous target outside driver range/step");}if(request.Pan<panMin||request.Pan>panMax||request.Tilt<tiltMin||request.Tilt>tiltMax)throw new Exception("Tracking travel guard reached; reframe before enabling again");if(request.Zoom!=snapshot.Controls.Find(c=>c.Name=="Zoom").Value)throw new Exception("Change framing before continuous tracking; zoom remains fixed");pending=request;inputTime=clock.ElapsedMilliseconds;}reply=new CameraReading{Name=snapshot.Name,Operation=snapshot.Operation,Controls=snapshot.Controls,SnapshotUtcMilliseconds=snapshot.SnapshotUtcMilliseconds,CommandedPan=commandedPan,CommandedTilt=commandedTilt,ResolvedTargetPan=pending?.Pan,ResolvedTargetTilt=pending?.Tilt};}respond(reply);}}catch(Exception e){lock(gate)inputError=e;}finally{lock(gate)ended=true;}}){IsBackground=true};input.Start();
    var pan=cache[0];var tilt=cache[1];var p=new ServoAxis(pan.Value,pan.Min,pan.Max,pan.Step,80,320);var t=new ServoAxis(tilt.Value,tilt.Min,tilt.Max,tilt.Step,60,240);int sentPan=pan.Value,sentTilt=tilt.Value;long tick=clock.ElapsedMilliseconds,lastRead=tick;
    while(true){TrackingMotion target;bool exit;lock(gate){if(inputError!=null)throw inputError;exit=ended;if(clock.ElapsedMilliseconds-inputTime>350)pending=null;target=pending;}if(exit)break;
     long now=clock.ElapsedMilliseconds;double dt=(now-tick)/1000.0;tick=now;
     if(target==null){p.Hold(sentPan);t.Hold(sentTilt);}else{int nextPan=p.Tick(target.Pan,dt,target.SpeedScale),nextTilt=t.Tick(target.Tilt,dt,target.SpeedScale);lock(gate){if(nextPan!=sentPan){Marshal.ThrowExceptionForHR(controls.Set(0,nextPan,2));sentPan=commandedPan=nextPan;}if(nextTilt!=sentTilt){Marshal.ThrowExceptionForHR(controls.Set(1,nextTilt,2));sentTilt=commandedTilt=nextTilt;}}}
     if(now-lastRead>=100){foreach(var id in new[]{0,1,3}){var c=cache[id];c.GetHResult=controls.Get(id,out c.Value,out c.Flags);Marshal.ThrowExceptionForHR(c.GetHResult);}lock(gate)snapshot=Copy();lastRead=clock.ElapsedMilliseconds;}
     int wait=25-(int)(clock.ElapsedMilliseconds-now);if(wait>0)Thread.Sleep(wait);
    }
   });if(result.Count!=1||result[0].Error!=null)throw new Exception(result.Count==1?result[0].Error:"Expected one camera");
  }
 }
}
