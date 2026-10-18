using Link360;
using Link360Desktop;
internal static class SceneStartTests {
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static void Reject(Action action,string message){try{action();}catch(IOException){return;}throw new Exception(message);}
 static CameraReading State()=>new(){NativeTrackingEnabled=false,Controls=[new(){Name="Pan",Min=-145,Max=145,Step=1,Capabilities=2,Flags=2,Value=27},new(){Name="Tilt",Min=-90,Max=100,Step=1,Capabilities=2,Flags=2,Value=-24},new(){Name="Zoom",Min=100,Max=400,Step=1,Capabilities=2,Flags=2,Value=160}]};
 public static void Run(){
  var empty=new BodyResult([],640,360,"fixture",0);var state=State();
  Check(SceneScanPolicy.NeutralStart(state,empty,empty)==new Pose(0,0,100),"Off-axis start did not request a neutral wide view");
  Check(state.Controls[0].Value==27&&state.Controls[1].Value==-24,"Start planning changed readback");
  var person=empty with{People=[new(10,10,100,200,50,70,.9)]};
  Reject(()=>SceneScanPolicy.NeutralStart(state,person,empty),"Initial framing accepted a room person");
  Reject(()=>SceneScanPolicy.NeutralStart(state,empty,person),"Initial framing accepted a Link person");
  Reject(()=>SceneScanPolicy.NeutralStart(state,empty with{Error="detector failed"},empty),"Initial framing ignored failed inference");
  Reject(()=>SceneScanPolicy.NeutralStart(state,empty with{Width=0},empty),"Initial framing accepted invalid inference dimensions");
  state=State();state.NativeTrackingEnabled=true;Reject(()=>SceneScanPolicy.NeutralStart(state,empty,empty),"Native tracking competed with homing");
  state.NativeTrackingEnabled=null;Reject(()=>SceneScanPolicy.NeutralStart(state,empty,empty),"Unknown native state allowed homing");
  state=State();state.Controls[0].Value=61;Reject(()=>SceneScanPolicy.NeutralStart(state,empty,empty),"Initial pan exceeded travel limit");
  state=State();state.Controls[1].Value=-41;Reject(()=>SceneScanPolicy.NeutralStart(state,empty,empty),"Initial tilt exceeded travel limit");
  state=State();state.Controls[0].Flags=1;Reject(()=>SceneScanPolicy.NeutralStart(state,empty,empty),"Automatic control flags allowed a start move");
  state=State();state.Controls[0].Step=2;Reject(()=>SceneScanPolicy.NeutralStart(state,empty,empty),"Off-grid zero target was sent");
  state=State();state.Controls[0].GetHResult=-1;Reject(()=>SceneScanPolicy.NeutralStart(state,empty,empty),"Failed readback allowed homing");
  state=State();state.Controls[2].Min=101;Reject(()=>SceneScanPolicy.NeutralStart(state,empty,empty),"Unknown zoom units allowed homing");
  state=State();state.Controls.Add(state.Controls[0]);Reject(()=>SceneScanPolicy.NeutralStart(state,empty,empty),"Ambiguous controls allowed homing");
  Console.WriteLine("PASS: bounded neutral scene start, empty-view/native-state gates, driver ranges/steps/flags, failed readback and ambiguity rejection");
 }
}
