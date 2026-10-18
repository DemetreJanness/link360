using Link360;
using Link360Desktop;
internal static class HandoffAlignmentTests {
 public static void Run(){
  void Check(bool valid,string message){if(!valid)throw new Exception(message);}
  RoomPoint[] saved=[new(.2,.3,-12,-4),new(.8,.3,12,-4),new(.2,.7,-12,4),new(.8,.7,12,4)];var map=RoomMap.Fit(saved);var now=DateTime.UtcNow;
  var state=new CameraReading{Controls=[new(){Name="Pan",Min=-145,Max=145,Step=1,Capabilities=2,Value=2},new(){Name="Tilt",Min=-90,Max=100,Step=1,Capabilities=2,Value=-3},new(){Name="Zoom",Min=100,Max=400,Step=1,Capabilities=2,Value=100}],CommandedPan=2,CommandedTilt=-3};
  var body=new BodyResult([new(90,10,140,160,160,90,.9)],320,180,"fixture",0,AnchorKind:"upper-body");
  var continuity=new HandoffAlignment();
  void Frame(int index,BodyResult? observed=null){var at=now.AddMilliseconds(index*50);state.SnapshotUtcMilliseconds=new DateTimeOffset(at).ToUnixTimeMilliseconds();continuity.Observe(map,body,at,observed??body,at,state,at);}
  for(int i=0;i<5;i++)Frame(i);Check(continuity.Reference==new RoomAlignment(2,-3),"Steady framing did not establish a bounded continuity reference");
  var original=map.Target(.5,.5,state,false)!;Check(original.Pan==0&&original.Tilt==0,"Continuity changed the saved map");
  Check(continuity.Reference!.Apply(original,state)==new Pose(2,-3,100),"Unmoved subject jumped on Acer handoff");
  var moved=map.Target(.6,.5,state,false)!;Check(continuity.Reference.Apply(moved,state)!.Pan>2,"Continuity suppressed real room movement");
  continuity.BreakAcquisition();Check(continuity.Reference!=null,"Brief Link loss erased the verified framing");
  continuity.Clear();for(int i=0;i<7;i++)Frame(i,body with{People=[body.People[0] with{CenterY=120}]});Check(continuity.Reference==null,"Off-centre Link learned a false reference");
  continuity.Clear();state.CommandedPan=7;for(int i=0;i<7;i++)Frame(i);Check(continuity.Reference==null,"Moving motor learned a reference");state.CommandedPan=2;
  continuity.Clear();for(int i=0;i<7;i++)Frame(0);Check(continuity.Reference==null,"Duplicate frames learned a reference");
  continuity.Clear();state.Controls[0].Value=11;state.CommandedPan=11;for(int i=0;i<7;i++)Frame(i);Check(continuity.Reference==null,"Excessive map correction became a continuity reference");
  Console.WriteLine("PASS: steady-framing handoff continuity, real-motion response, unchanged map, moving/off-centre/duplicate/excessive-offset rejection");
 }
}
