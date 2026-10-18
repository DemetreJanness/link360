using Link360Desktop;
using System.Text.Json;

internal static class SceneMatchProbe {
 public static async Task Run(string app){
  using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(50));var ct=deadline.Token;
  await using var room=new DirectFrames("room",true,app);await using var link=new DirectFrames("link",true,app);
  using var detector=new LocalWholeBodyDetector(true);
  await room.Read(ct);await link.Read(ct);await Task.Delay(1500,ct);
  for(int i=0;i<6;i++){
   var r=await room.Read(ct);var l=await link.Read(ct);TrackingConfidence.Minimum=.35;
   var rd=detector.Detect(r.Bytes);var ld=detector.Detect(l.Bytes);var evidence=new List<object>();string? error=null;SceneMatch? match=null;
   try{match=SceneRegistration.Match(r.Bytes,l.Bytes,rd,ld,evidence.Add);}catch(Exception e){error=e.Message;}
   Console.WriteLine(JsonSerializer.Serialize(new{sample=i,roomPeople=rd.People.Length,linkPeople=ld.People.Length,roomObjects=rd.Objects?.Select(o=>o.Kind),linkObjects=ld.Objects?.Select(o=>o.Kind),quality=evidence,match=match==null?null:new{match.X,match.Y,match.Inliers,match.InlierRatio},error}));
   await Task.Delay(500,ct);
  }
 }
}
