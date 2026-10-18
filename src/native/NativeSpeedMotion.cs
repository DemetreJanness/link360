#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
namespace Link360 {
 // Captured original-Link selector22: [pan direction,magnitude,tilt direction,magnitude].
 // Signed API axes follow standard Windows pan/tilt; tilt wire direction is inverted.
 public static class NativeSpeedPacket {
  public const int Maximum=10; public const int TrackingMaximum=10; public const int RecoveryMaximum=4;
  public static byte[] Encode(int pan,int tilt){if(Math.Abs((long)pan)>Maximum||Math.Abs((long)tilt)>Maximum)throw new ArgumentOutOfRangeException("speed","Native UI-derived magnitude must be between zero and ten");byte Dir(int v)=>v==0?(byte)0:v>0?(byte)1:(byte)255;return new[]{Dir(pan),(byte)Math.Max(1,Math.Abs(pan)),Dir(-tilt),(byte)Math.Max(1,Math.Abs(tilt))};}
 }
 public static class NativeAngleFeedback { public static int DriverUnits(int tenths,int min,int max){if((long)tenths<min*10L||(long)tenths>max*10L)throw new ArgumentOutOfRangeException(nameof(tenths),"Native pose outside verified driver range");return (int)Math.Round(tenths/10.0,MidpointRounding.AwayFromZero);} }
 public sealed class NativeSpeedLease {
  public const int ExpiryMs=300;
  long at;bool armed;int pan,tilt;
  public void Demand(int p,int t,long now){NativeSpeedPacket.Encode(p,t);pan=p;tilt=t;at=now;armed=true;}
  public void Hold(){armed=false;pan=tilt=0;}
  public (int Pan,int Tilt) Read(long now)=>armed&&now>=at&&now-at<ExpiryMs?(pan,tilt):(0,0);
 }
 public sealed class NativeSpeedSlew {
  int value;long changed=-1000;
  public int Tick(int desired,long now,int maximum=NativeSpeedPacket.Maximum){if(maximum<1||maximum>NativeSpeedPacket.Maximum||Math.Abs((long)desired)>maximum)throw new ArgumentOutOfRangeException(nameof(desired));value=Math.Clamp(value,-maximum,maximum);if(desired==0){value=0;changed=now;return 0;}if(value!=0&&Math.Sign(value)!=Math.Sign(desired)){value=0;changed=now;return 0;}if(now-changed>=60&&value!=desired){value+=Math.Sign(desired-value);changed=now;}return value;}
 }
 public static partial class CameraProbe {
  sealed class SpeedChannel:IDisposable {
   IKsControl control;object owned;uint node;
   public SpeedChannel(IAMCameraControl camera){
    var topology=(IKsTopologyInfo)camera;Marshal.ThrowExceptionForHR(topology.NumNodes(out uint count));if(count>64)throw new Exception("Unexpected speed topology");int matches=0;
    try{for(uint i=0;i<count;i++){Marshal.ThrowExceptionForHR(topology.NodeType(i,out Guid type));if(type!=new Guid("941C7AC0-C559-11D0-8A2B-00A0C9255AC1"))continue;object instance=null;
     try{var iid=typeof(IKsControl).GUID;if(topology.CreateNodeInstance(i,ref iid,out instance)<0)continue;var candidate=(IKsControl)instance;var prop=new KsNodeProperty{Set=TrackingExtension,Id=22,Flags=0x10000001,NodeId=i};var bytes=new byte[4];int hr=candidate.KsProperty(ref prop,32,bytes,4,out uint length);if(hr<0||length!=4)continue;matches++;if(matches==1){control=candidate;owned=instance;node=i;instance=null;}}
     finally{Release(instance);}}
     if(matches!=1||control==null)throw new Exception("Original-Link speed capability unavailable/ambiguous");if(NativeMode()!=0)throw new Exception("Turn built-in AI off before native speed control");
     var current=Read(22,4);if(!ValidDirection(current[0])||!ValidDirection(current[2]))throw new Exception("Unrecognized native speed state");
    }catch{Dispose();throw;}
   }
   static bool ValidDirection(byte value)=>value==0||value==1||value==255;
   byte[] Read(uint id,int length){var prop=new KsNodeProperty{Set=TrackingExtension,Id=id,Flags=0x10000001,NodeId=node};var bytes=new byte[length];Marshal.ThrowExceptionForHR(control.KsProperty(ref prop,32,bytes,(uint)length,out uint returned));if(returned!=length)throw new Exception("Unexpected native speed response length");return bytes;}
   public byte[] Current()=>Read(22,4);
   public (int Pan,int Tilt) RawPosition(){var bytes=Read(2,52);if(BitConverter.ToUInt32(bytes,0)!=0)throw new Exception("Native mode changed; pose feedback unavailable");return (BitConverter.ToInt32(bytes,38),BitConverter.ToInt32(bytes,42));}
   public uint NativeMode()=>BitConverter.ToUInt32(Read(2,52),0);
   public void Write(int pan,int tilt){var bytes=NativeSpeedPacket.Encode(pan,tilt);var prop=new KsNodeProperty{Set=TrackingExtension,Id=22,Flags=0x10000002,NodeId=node};Marshal.ThrowExceptionForHR(control.KsProperty(ref prop,32,bytes,4,out _));}
   public void StopVerified(){Write(0,0);var value=Read(22,4);if(value[0]!=0||value[2]!=0)throw new Exception("Native neutral command did not read back");}
   public void Dispose(){Release(owned);owned=null;control=null;}
  }
  static void RequireSpeedDevice(){var devices=VideoDevices().FindAll(d=>d.Name=="Insta360 Link");if(devices.Count!=1||!devices[0].Path.ToLowerInvariant().Contains("vid_2e1a&pid_4c01"))throw new Exception("Speed control requires one original Link VID2E1A/PID4C01");RequireOneCamera();if(Process.GetProcessesByName("Insta360 Link Controller").Length>0)throw new Exception("Close the vendor controller before speed control");}
  public static List<CameraReading> ReadNativeSpeeds(){RequireSpeedDevice();var results=Inspect(false,null,0,3,(camera,reading)=>{using var channel=new SpeedChannel(camera);var bytes=channel.Current();int Decode(byte d,byte v)=>d==0?0:d==1?v:-v;reading.NativeTrackingEnabled=false;reading.NativePanSpeed=Decode(bytes[0],bytes[1]);reading.NativeTiltSpeed=-Decode(bytes[2],bytes[3]);var position=channel.RawPosition();reading.NativePanTenths=position.Pan;reading.NativeTiltTenths=position.Tilt;reading.Operation="NativeSpeedRead";});foreach(var r in results){if(r.Error!=null)continue;foreach(var c in r.Controls){if(c.Name=="Pan")c.Value=NativeAngleFeedback.DriverUnits(r.NativePanTenths.Value,c.Min,c.Max);if(c.Name=="Tilt")c.Value=NativeAngleFeedback.DriverUnits(r.NativeTiltTenths.Value,c.Min,c.Max);}}return results;}
  public static void GuardNativeSpeed(){
   RequireSpeedDevice();var result=Inspect(false,null,0,3,(camera,reading)=>{using var channel=new SpeedChannel(camera);channel.StopVerified();Console.WriteLine("ready");try{while(true){var next=Console.In.ReadLineAsync();if(!next.Wait(450)||next.Result!="beat")break;}}finally{channel.StopVerified();}Console.WriteLine("stopped");});if(result.Count!=1||result[0].Error!=null)throw new Exception(result.Count==1?result[0].Error:"Speed guardian lost camera");
  }
  public static void ServeNativeSpeed(Func<TrackingMotion> receive,Action<CameraReading> respond,bool limitedTravel=false){
   RequireSpeedDevice();var result=Inspect(false,null,0,3,(camera,reading)=>{
    var cache=new Dictionary<int,ControlReading>();foreach(var pair in new[]{Tuple.Create(0,"Pan"),Tuple.Create(1,"Tilt"),Tuple.Create(3,"Zoom")}){var c=new ControlReading{Name=pair.Item2};Marshal.ThrowExceptionForHR(c.RangeHResult=camera.GetRange(pair.Item1,out c.Min,out c.Max,out c.Step,out c.Default,out c.Capabilities));Marshal.ThrowExceptionForHR(c.GetHResult=camera.Get(pair.Item1,out c.Value,out c.Flags));if(c.Step<=0||c.Min>c.Max||c.Value<c.Min||c.Value>c.Max||(c.Capabilities&2)==0)throw new Exception("Invalid speed feedback range");cache[pair.Item1]=c;}
    using var channel=new SpeedChannel(camera);var position=channel.RawPosition();cache[0].Value=NativeAngleFeedback.DriverUnits(position.Pan,cache[0].Min,cache[0].Max);cache[1].Value=NativeAngleFeedback.DriverUnits(position.Tilt,cache[1].Min,cache[1].Max);
    int originPan=cache[0].Value,originTilt=cache[1].Value,zoom=cache[3].Value;
    int panMin=limitedTravel?Math.Max(cache[0].Min,originPan-20):cache[0].Min,panMax=limitedTravel?Math.Min(cache[0].Max,originPan+20):cache[0].Max;
    int tiltMin=limitedTravel?Math.Max(cache[1].Min,originTilt-15):cache[1].Min,tiltMax=limitedTravel?Math.Min(cache[1].Max,originTilt+15):cache[1].Max;
    channel.StopVerified();
    using var guardian=Process.Start(new ProcessStartInfo(Environment.ProcessPath!,"--native-speed-guardian"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true})??throw new Exception("Cannot start independent neutral supervisor");
    var ready=guardian.StandardOutput.ReadLineAsync();if(!ready.Wait(4000)||ready.Result!="ready"){guardian.StandardInput.Close();throw new Exception("Neutral supervisor unavailable; no motion enabled");}
    object gate=new();var queue=new Queue<(TrackingMotion Motion,long At)>();var clock=Stopwatch.StartNew();bool ended=false;Exception inputError=null;
    var input=new Thread(()=>{try{while(true){var request=receive();if(request==null)break;lock(gate){if(queue.Count>=4)throw new Exception("Speed request backlog; stopping");queue.Enqueue((request,clock.ElapsedMilliseconds));}}}catch(Exception e){lock(gate)inputError=e;}finally{lock(gate)ended=true;}}){IsBackground=true};input.Start();
    var lease=new NativeSpeedLease();var pSlew=new NativeSpeedSlew();var tSlew=new NativeSpeedSlew();long lastDemand=-1000,lastRead=-1000,lastNative=0,lastBeat=-1000;TrackingMotion target=null;int sentPan=0,sentTilt=0;
    CameraReading Snapshot()=>new CameraReading{Name=reading.Name,Operation="NativeSpeedTracking",NativeTrackingEnabled=false,NativePanSpeed=sentPan,NativeTiltSpeed=sentTilt,SnapshotUtcMilliseconds=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),CommandedPan=cache[0].Value,CommandedTilt=cache[1].Value,NativePanTenths=position.Pan,NativeTiltTenths=position.Tilt,ResolvedTargetPan=target?.Pan,ResolvedTargetTilt=target?.Tilt,Controls=new List<ControlReading>{Clone(cache[0]),Clone(cache[1]),Clone(cache[3])}};
    guardian.StandardInput.WriteLine("beat");guardian.StandardInput.Flush();lastBeat=clock.ElapsedMilliseconds;
    try{while(true){long now=clock.ElapsedMilliseconds,arrival=now;TrackingMotion request=null;lock(gate){if(inputError!=null)throw inputError;if(ended)break;if(queue.Count>0){var item=queue.Dequeue();request=item.Motion;arrival=item.At;}}
     if(guardian.HasExited)throw new Exception("Neutral supervisor exited; speed control stopped");
     if(now-lastRead>=50||request!=null){foreach(var id in new[]{0,1,3}){var c=cache[id];Marshal.ThrowExceptionForHR(c.GetHResult=camera.Get(id,out c.Value,out c.Flags));if(c.Value<c.Min||c.Value>c.Max)throw new Exception("Speed feedback outside driver range");}position=channel.RawPosition();cache[0].Value=NativeAngleFeedback.DriverUnits(position.Pan,cache[0].Min,cache[0].Max);cache[1].Value=NativeAngleFeedback.DriverUnits(position.Tilt,cache[1].Min,cache[1].Max);lastRead=clock.ElapsedMilliseconds;}
     if(cache[3].Value!=zoom)throw new Exception("Zoom changed during speed session; stopping");
     if(now-lastNative>=400){if(Process.GetProcessesByName("Insta360 Link Controller").Length>0||channel.NativeMode()!=0)throw new Exception("Native controller/AI changed; speed control stopped");lastNative=clock.ElapsedMilliseconds;}
     now=clock.ElapsedMilliseconds;if(guardian.HasExited||now-lastBeat>=350)throw new Exception("Speed loop stalled; supervisor takes over");
     if(request!=null){
      if(request.Hold){lease.Hold();target=null;}
      else if(request.NativePanSpeed.HasValue){if(!request.NativeTiltSpeed.HasValue)throw new Exception("Both speed axes required");lease.Demand(request.NativePanSpeed.Value,request.NativeTiltSpeed.Value,arrival);target=null;lastDemand=arrival;}
      else if(request.DurationMs!=0){
       if(request.RelativePan.HasValue||request.RelativeTilt.HasValue)throw new Exception("Speed target mode requires absolute mapped goals");
       if(request.Pan<panMin||request.Pan>panMax||request.Tilt<tiltMin||request.Tilt>tiltMax||request.Zoom!=zoom)throw new Exception("Speed target outside bounded range");target=request;lastDemand=arrival;lease.Hold();}
     }
     var desired=lease.Read(now);
     if(target!=null){if(now-lastDemand>=NativeSpeedLease.ExpiryMs){target=null;desired=(0,0);}else{int Axis(int error)=>Math.Abs(error)<=1?0:Math.Sign(error)*Math.Min(NativeSpeedPacket.RecoveryMaximum,(int)Math.Ceiling(Math.Abs(error)/4.0));desired=(Axis(target.Pan-cache[0].Value),Axis(target.Tilt-cache[1].Value));}}
     int Guard(int demand,int value,int min,int max)=>demand>0&&value>=max-2||demand<0&&value<=min+2?0:demand;
     desired=(Guard(desired.Item1,cache[0].Value,panMin,panMax),Guard(desired.Item2,cache[1].Value,tiltMin,tiltMax));
     int cap=target!=null?NativeSpeedPacket.RecoveryMaximum:NativeSpeedPacket.Maximum;int pan=pSlew.Tick(desired.Item1,now,cap),tilt=tSlew.Tick(desired.Item2,now,cap);
     if(pan!=sentPan||tilt!=sentTilt){channel.Write(pan,tilt);sentPan=pan;sentTilt=tilt;}
     if(now-lastBeat>=50){guardian.StandardInput.WriteLine("beat");guardian.StandardInput.Flush();lastBeat=clock.ElapsedMilliseconds;}
     if(request!=null)respond(Snapshot());Thread.Sleep(20);
    }}finally{
     try{channel.StopVerified();}finally{guardian.StandardInput.Close();if(!guardian.WaitForExit(1800))throw new Exception("Neutral supervisor did not exit; motion remains stopped by lease");if(guardian.ExitCode!=0)throw new Exception("Neutral supervisor reported a stop failure");}
    }
   });if(result.Count!=1||result[0].Error!=null)throw new Exception(result.Count==1?result[0].Error:"Speed camera unavailable");
  }
  static ControlReading Clone(ControlReading c)=>new ControlReading{Name=c.Name,RangeHResult=c.RangeHResult,GetHResult=c.GetHResult,Min=c.Min,Max=c.Max,Step=c.Step,Default=c.Default,Capabilities=c.Capabilities,Value=c.Value,Flags=c.Flags};
 }
}
