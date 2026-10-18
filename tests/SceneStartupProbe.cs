using Link360;
using Link360Desktop;
using OpenCvSharp;
using System.Text.Json;

// Explicit read-only hardware investigation. No PTZ writes or saved frames.
internal static class SceneStartupProbe {
 public static async Task Run(string app){
  using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(45));var ct=deadline.Token;
  var engine=new CameraEngine(Path.GetFullPath(app));
  object Pose(CameraReading state)=>new{pan=state.Controls.Single(c=>c.Name=="Pan").Value,tilt=state.Controls.Single(c=>c.Name=="Tilt").Value,zoom=state.Controls.Single(c=>c.Name=="Zoom").Value};
  Console.WriteLine(JsonSerializer.Serialize(new{stage="before-video",pose=Pose(await engine.Execute(new(),ct))}));
  await using var room=new DirectFrames("room",true,app);await using var link=new DirectFrames("link",true,app);
  await room.Read(ct);await link.Read(ct);
  for(int i=0;i<3;i++){
   await Task.Delay(1200,ct);var r=await room.Read(ct);var l=await link.Read(ct);
   using var ri=Cv2.ImDecode(r.Bytes,ImreadModes.Grayscale);using var li=Cv2.ImDecode(l.Bytes,ImreadModes.Grayscale);
   var state=await engine.Execute(new(ReadNativeTracking:true),ct);
   Console.WriteLine(JsonSerializer.Serialize(new{stage="video-running",sample=i,pose=Pose(state),nativeTracking=state.NativeTrackingEnabled,room=new{width=ri.Width,height=ri.Height,meanLuma=Math.Round(Cv2.Mean(ri).Val0,1)},link=new{width=li.Width,height=li.Height,meanLuma=Math.Round(Cv2.Mean(li).Val0,1)}}));
  }
 }
}
