#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Link360 {
 public class MotionPoint {
  public int AtMs,Pan,Tilt,Zoom;
 }
 public class MotionAxis {
  public int Start,Target,Min,Max,Step;
 }
 public class TrackingMotion {public int Pan,Tilt,Zoom,DurationMs=150;public bool Hold;public double SpeedScale=1;public int? RelativePan,RelativeTilt,NativePanSpeed,NativeTiltSpeed;}
 public static class MotionPlanner {
  public static List<MotionPoint> Plan(MotionAxis pan,MotionAxis tilt,MotionAxis zoom,int requestedMs) {
   var axes=new[]{pan,tilt,zoom};
   foreach(var a in axes) {
    if(a.Step<=0||a.Min>a.Max||a.Start<a.Min||a.Start>a.Max||a.Target<a.Min||a.Target>a.Max||((long)a.Target-a.Min)%a.Step!=0)
     throw new ArgumentException("Motion outside current camera range/step");
   }
   if(requestedMs<75||requestedMs>6000)throw new ArgumentException("Transition must be 75–6000 ms");
   // Limits are command-trajectory bounds in driver units, not calibrated motor speeds.
   double minimum=Math.Max(Math.Abs((long)pan.Target-pan.Start)*1500.0/80,
      Math.Max(Math.Abs((long)tilt.Target-tilt.Start)*1500.0/60,Math.Abs((long)zoom.Target-zoom.Start)*1500.0/150));
   int duration=Math.Max(requestedMs,(int)Math.Ceiling(minimum));
   if(duration>6000)throw new ArgumentException("Transition exceeds bounded movement duration");
   // Short tracking corrections use finer cadence; long preset transitions
   // keep their original 20Hz trajectory and all velocity/quantization bounds.
   int frames=(int)Math.Ceiling(duration/(requestedMs<=150?25.0:50.0));
   var points=new List<MotionPoint>();
   for(int i=0;i<=frames;i++) {
    double t=(double)i/frames,ease=t*t*(3-2*t);
    int Quantize(MotionAxis a) {
     if(i==0)return a.Start;if(i==frames)return a.Target;
     double v=a.Start+(a.Target-a.Start)*ease;
     long ticks=(long)Math.Round((v-a.Min)/a.Step,MidpointRounding.AwayFromZero);
     return (int)Math.Max(a.Min,Math.Min(a.Max,a.Min+ticks*a.Step));
    }
    points.Add(new MotionPoint{AtMs=(int)Math.Round(duration*t),Pan=Quantize(pan),Tilt=Quantize(tilt),Zoom=Quantize(zoom)});
   }
   return points;
  }
 }
 public static partial class CameraProbe {
  public static List<CameraReading> MoveSmooth(int pan,int tilt,int zoom,int durationMs) {
   RequireOneCamera();
   return Inspect(false,null,0,3,(controls,reading)=>{
    MoveBound(controls,reading,pan,tilt,zoom,durationMs);
   });
  }
  static void MoveBound(IAMCameraControl controls,CameraReading reading,int pan,int tilt,int zoom,int durationMs,Dictionary<int,ControlReading> ranges=null) {
    MotionAxis Read(int property,int target) {
     int min,max,step,def,caps,value,flags;
     if(ranges!=null){var c=ranges[property];min=c.Min;max=c.Max;step=c.Step;def=c.Default;caps=c.Capabilities;}
     else Marshal.ThrowExceptionForHR(controls.GetRange(property,out min,out max,out step,out def,out caps));
     Marshal.ThrowExceptionForHR(controls.Get(property,out value,out flags));
     if((caps&2)==0)throw new Exception("Manual motion is unavailable");
     return new MotionAxis{Start=value,Target=target,Min=min,Max=max,Step=step};
    }
    var points=MotionPlanner.Plan(Read(0,pan),Read(1,tilt),Read(3,zoom),durationMs);
    reading.Operation="EasedPreset";
    reading.MotionDurationMs=points[points.Count-1].AtMs;
    var clock=Stopwatch.StartNew();
    var previous=points[0];
    for(int i=1;i<points.Count;i++) {
     var p=points[i];
     int wait=p.AtMs-(int)clock.ElapsedMilliseconds;
     if(wait>0)Thread.Sleep(wait);
     // Never catch up by dumping stale intermediate commands into the driver.
     while(i+1<points.Count && points[i+1].AtMs<=clock.ElapsedMilliseconds){p=points[++i];}
     if(p.Pan!=previous.Pan){Marshal.ThrowExceptionForHR(controls.Set(0,p.Pan,2));reading.MotionCommands++;}
     if(p.Tilt!=previous.Tilt){Marshal.ThrowExceptionForHR(controls.Set(1,p.Tilt,2));reading.MotionCommands++;}
     if(p.Zoom!=previous.Zoom){Marshal.ThrowExceptionForHR(controls.Set(3,p.Zoom,2));reading.MotionCommands++;}
     previous=p;
    }
    reading.MotionElapsedMs=(int)clock.ElapsedMilliseconds;
    int value,flags;
    foreach(var pair in new[]{Tuple.Create(0,pan),Tuple.Create(1,tilt),Tuple.Create(3,zoom)}) {
     Marshal.ThrowExceptionForHR(controls.Get(pair.Item1,out value,out flags));
     if(value!=pair.Item2)throw new Exception("Motion endpoint differs from driver readback");
     if(ranges!=null){ranges[pair.Item1].Value=value;ranges[pair.Item1].Flags=flags;ranges[pair.Item1].GetHResult=0;}
    }
  }
  // Keep one verified COM binding for a cancellable process lifetime. Any
  // disconnect or manual override kills that process; no session is resumed.
  public static void ServeTracking(Func<TrackingMotion> receive,Action<CameraReading> respond) {
   RequireOneCamera();
   var result=Inspect(false,null,0,3,(controls,reading)=>{
    var ranges=new Dictionary<int,ControlReading>();
    foreach(var pair in new[]{Tuple.Create(0,"Pan"),Tuple.Create(1,"Tilt"),Tuple.Create(3,"Zoom")}) {
     var c=new ControlReading{Name=pair.Item2};c.RangeHResult=controls.GetRange(pair.Item1,out c.Min,out c.Max,out c.Step,out c.Default,out c.Capabilities);Marshal.ThrowExceptionForHR(c.RangeHResult);ranges[pair.Item1]=c;
    }
    while(true) {
     var request=receive();if(request==null)break;
     var current=new CameraReading{Name=reading.Name};
     try {
      if(request.DurationMs!=0)MoveBound(controls,current,request.Pan,request.Tilt,request.Zoom,request.DurationMs,ranges);
      foreach(var pair in new[]{Tuple.Create(0,"Pan"),Tuple.Create(1,"Tilt"),Tuple.Create(3,"Zoom")}) {
       var c=ranges[pair.Item1];
       if(request.DurationMs==0)c.GetHResult=controls.Get(pair.Item1,out c.Value,out c.Flags);current.Controls.Add(c);
       Marshal.ThrowExceptionForHR(c.RangeHResult);Marshal.ThrowExceptionForHR(c.GetHResult);
      }
     }catch(Exception e){current.Error=e.Message;current.ErrorKind="command_failed";}
     respond(current);if(current.Error!=null)break;
    }
   });
   if(result.Count!=1||result[0].Error!=null)throw new Exception(result.Count==1?result[0].Error:"Expected exactly one camera");
  }
 }
}
