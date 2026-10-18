using System.Diagnostics;
using System.Text.Json;
using Link360;
using Link360Desktop;
using OpenCvSharp;
internal static class NativeSpeedProbe {
 public static async Task Run(string exe,string report,bool feedbackOnly=false,bool higher=false){
  var rows=new List<object>();using var ct=new CancellationTokenSource(TimeSpan.FromSeconds(higher?100:55));var engine=new CameraEngine(exe);var original=await engine.Execute(new(),ct.Token);int V(CameraReading r,string n)=>r.Controls.Single(c=>c.Name==n).Value;var home=new Pose(V(original,"Pan"),V(original,"Tilt"),V(original,"Zoom"));
  await using var video=new DirectFrames("link",executable:exe);
  async Task<Mat> Frame(){FeedFrame? f=null;for(int i=0;i<4;i++)f=await video.Read(ct.Token);return Cv2.ImDecode(f!.Bytes,ImreadModes.Grayscale);}
  async Task<CameraReading> ReadSpeed(){using var p=Process.Start(new ProcessStartInfo(exe,"--native-speed-read"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true})!;string text=await p.StandardOutput.ReadToEndAsync(ct.Token);await p.WaitForExitAsync(ct.Token);var r=JsonSerializer.Deserialize<CameraReading>(text,Link360Desktop.Program.Json)!;if(p.ExitCode!=0||r.Error!=null)throw new Exception(r.Error??"Speed read failed");return r;}
  try{
   using(var warm=await Frame()){}await Task.Delay(1500,ct.Token);using(var warm=await Frame()){}
   original=await engine.Execute(new(),ct.Token);var live=await ReadSpeed();
   home=new Pose((int)Math.Round(live.NativePanTenths!.Value/10.0),(int)Math.Round(live.NativeTiltTenths!.Value/10.0),V(original,"Zoom"));
   if(home.Tilt < -60){await engine.Execute(new(Control:"Tilt",Value:0),ct.Token);await Task.Delay(1600,ct.Token);live=await ReadSpeed();home=home with{Tilt=(int)Math.Round(live.NativeTiltTenths!.Value/10.0)};}
   rows.Add(new{name="awake-start",home,driverPan=V(original,"Pan"),driverTilt=V(original,"Tilt"),rawPan=live.NativePanTenths,rawTilt=live.NativeTiltTenths});
   var imageDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LINK360-Community","diagnostics","speed-probe-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(imageDir);using(var picture=await Frame())Cv2.ImWrite(Path.Combine(imageDir,"before.png"),picture);
   await engine.Execute(new(Control:"Pan",Value:home.Pan),ct.Token);await Task.Delay(300,ct.Token);await engine.Execute(new(Control:"Tilt",Value:home.Tilt),ct.Token);await Task.Delay(450,ct.Token);using(var flushed=await Frame()){}
   var basePose=await ReadSpeed();int pd=home.Pan+4<=original.Controls.Single(c=>c.Name=="Pan").Max?4:-4,td=home.Tilt+3<=original.Controls.Single(c=>c.Name=="Tilt").Max?3:-3;
   await engine.Execute(new(Control:"Pan",Value:home.Pan+pd),ct.Token);await Task.Delay(350,ct.Token);await engine.Execute(new(Control:"Tilt",Value:home.Tilt+td),ct.Token);await Task.Delay(450,ct.Token);using(var flushed=await Frame()){}
   using(var picture=await Frame())Cv2.ImWrite(Path.Combine(imageDir,"after.png"),picture);var changed=await ReadSpeed();rows.Add(new{name="pose-units",panDelta=pd,tiltDelta=td,rawPanDelta=changed.NativePanTenths-basePose.NativePanTenths,rawTiltDelta=changed.NativeTiltTenths-basePose.NativeTiltTenths,basePan=basePose.NativePanTenths,baseTilt=basePose.NativeTiltTenths,afterPan=changed.NativePanTenths,afterTilt=changed.NativeTiltTenths,imageDir});
   if(Math.Abs((changed.NativePanTenths-basePose.NativePanTenths??99999)-pd*10)>2||Math.Abs((changed.NativeTiltTenths-basePose.NativeTiltTenths??99999)-td*10)>2)throw new Exception("Native pose units did not match Windows angle steps");
   await engine.Execute(new(Control:"Pan",Value:home.Pan),ct.Token);await Task.Delay(300,ct.Token);await engine.Execute(new(Control:"Tilt",Value:home.Tilt),ct.Token);await Task.Delay(300,ct.Token);
   if(feedbackOnly){File.WriteAllText(report,JsonSerializer.Serialize(new{feedbackPassed=true,rows},Link360Desktop.Program.Json));Console.WriteLine(JsonSerializer.Serialize(new{feedbackPassed=true,rows},Link360Desktop.Program.Json));return;}
   var pulses=higher?new[]{(Name:"pan-baseline",Pan:4,Tilt:0),(Name:"pan-six",Pan:6,Tilt:0),(Name:"pan-ten",Pan:10,Tilt:0),(Name:"pan-ten-negative",Pan:-10,Tilt:0),(Name:"tilt-six",Pan:0,Tilt:6),(Name:"tilt-ten-up",Pan:0,Tilt:10),(Name:"tilt-ten-down",Pan:0,Tilt:-10)}:new[]{(Name:"pan-slow",Pan:1,Tilt:0),(Name:"pan-fast",Pan:4,Tilt:0),(Name:"tilt-up",Pan:0,Tilt:4),(Name:"tilt-down",Pan:0,Tilt:-4)};
   foreach(var pulse in pulses){
    using var before=await Frame();Cv2.ImWrite(Path.Combine(imageDir,pulse.Name+"-before.png"),before);CameraReading after;
    await using(var session=new LocalCameraSession(ct.Token,exe,continuous:true,limitedTravel:true,nativeSpeed:true)){
     var start=await engine.Execute(new(DurationMs:0),ct.Token,session);var clock=Stopwatch.StartNew();var trajectory=new List<object>();int maxPan=0,maxTilt=0;
     while(clock.ElapsedMilliseconds<(higher?900:420)){var observed=await engine.Execute(new(NativePanSpeed:pulse.Pan,NativeTiltSpeed:pulse.Tilt),ct.Token,session);maxPan=Math.Max(maxPan,Math.Abs(observed.NativePanSpeed??0));maxTilt=Math.Max(maxTilt,Math.Abs(observed.NativeTiltSpeed??0));trajectory.Add(new{ms=clock.ElapsedMilliseconds,pan=observed.NativePanTenths,tilt=observed.NativeTiltTenths,panSpeed=observed.NativePanSpeed,tiltSpeed=observed.NativeTiltSpeed});await Task.Delay(35,ct.Token);}
     await engine.Execute(new(Hold:true),ct.Token,session);await Task.Delay(150,ct.Token);after=await engine.Execute(new(DurationMs:0),ct.Token,session);
     var neutral=await ReadSpeed();if(neutral.NativePanSpeed!=0||neutral.NativeTiltSpeed!=0)throw new Exception("Pulse did not stop");
     using var picture=await Frame();Cv2.ImWrite(Path.Combine(imageDir,pulse.Name+"-after.png"),picture);var optical=VideoMotionReview.Measure(before,picture);
     rows.Add(new{pulse.Name,pulse.Pan,pulse.Tilt,elapsedMs=clock.ElapsedMilliseconds,maxPan,maxTilt,trajectory,panChange=V(after,"Pan")-V(start,"Pan"),tiltChange=V(after,"Tilt")-V(start,"Tilt"),rawPanChange=after.NativePanTenths-start.NativePanTenths,rawTiltChange=after.NativeTiltTenths-start.NativeTiltTenths,imageDir,optical,neutral=true});
    }
    await engine.Execute(new(Control:"Pan",Value:home.Pan),ct.Token);await Task.Delay(300,ct.Token);await engine.Execute(new(Control:"Tilt",Value:home.Tilt),ct.Token);await Task.Delay(200,ct.Token);
   }
   // Kill the issuing process without killing its independent neutral child.
   using var worker=Process.Start(new ProcessStartInfo(exe,"--speed-camera-session --limited-tracking"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true})!;
   async Task Send(object q){await worker.StandardInput.WriteLineAsync(JsonSerializer.Serialize(q));string? line=await worker.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(6));var r=JsonSerializer.Deserialize<CameraReading>(line!,Link360Desktop.Program.Json)!;if(r.Error!=null)throw new Exception(r.Error);}
   try{await Send(new{NativePanSpeed=1,NativeTiltSpeed=0});await Task.Delay(90,ct.Token);await Send(new{NativePanSpeed=1,NativeTiltSpeed=0});worker.Kill(false);await worker.WaitForExitAsync(ct.Token);await Task.Delay(900,ct.Token);var stopped=await ReadSpeed();if(stopped.NativePanSpeed!=0||stopped.NativeTiltSpeed!=0)throw new Exception("Independent guardian failed worker-kill neutral");rows.Add(new{name="worker-kill",neutral=true});}
   finally{if(!worker.HasExited)worker.StandardInput.Close();}
   File.WriteAllText(report,JsonSerializer.Serialize(new{passed=true,rows},Link360Desktop.Program.Json));Console.WriteLine(JsonSerializer.Serialize(new{passed=true,rows},Link360Desktop.Program.Json));
  }catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{passed=false,error=e.Message,rows},Link360Desktop.Program.Json));throw;}
  finally{using var restore=new CancellationTokenSource(TimeSpan.FromSeconds(10));var stopped=await ReadSpeed();if(stopped.NativePanSpeed!=0||stopped.NativeTiltSpeed!=0)throw new Exception("Home restore refused while speed remains nonzero");await engine.Execute(new(Control:"Pan",Value:home.Pan),restore.Token);await engine.Execute(new(Control:"Tilt",Value:home.Tilt),restore.Token);}
 }
}
