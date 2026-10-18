using System.Text.Json;
using Link360;
using Link360Desktop;
internal static class RecoveryMapExtensionTests {
 static void Check(bool c,string s){if(!c)throw new Exception(s);}
 static void Reject(Action a,string s){try{a();}catch(IOException){return;}throw new Exception(s);}
 public static void Run(){
  RoomPoint[] before=[new(.4,.5,-3,5),new(.7,.5,14,5),new(.7,.8,14,-10),new(.4,.8,-3,-10)];
  var original=before.ToArray();var added=new RoomPoint(.2,.5,-17,5);
  var next=RecoveryMapExtension.Append(before,added);
  Check(before.SequenceEqual(original)&&next.Length==5&&next.Take(4).SequenceEqual(before)&&next[4]==added,"Coverage extension edited original pairs");
  var state=new CameraReading{Controls=[new(){Name="Pan",Min=-145,Max=145,Step=1,Capabilities=2},new(){Name="Tilt",Min=-90,Max=100,Step=1,Capabilities=2},new(){Name="Zoom",Min=100,Max=400,Step=1,Capabilities=2,Value=100}]};
  Check(RoomMap.Fit(before).Target(.35,.6,state,false)==null&&RoomMap.Fit(next).Target(.35,.6,state,false)!=null,"New measured point did not cover the known type of gap");
  Check(RoomMap.Fit(next).Target(.05,.1,state,false)==null,"Extension extrapolated outside measured area");
  Reject(()=>RecoveryMapExtension.Append(before,before[0]),"Duplicate capture accepted");
  Reject(()=>RecoveryMapExtension.Append(before,added with{X=double.NaN}),"Invalid capture accepted");
  Reject(()=>RecoveryMapExtension.Append(before,added with{Pan=-140}),"Conflicting extreme capture accepted");
  Reject(()=>RecoveryMapExtension.Append(Enumerable.Range(0,25).Select(i=>before[i%4]).ToArray(),added),"Capacity exceeded");
  var oldTen=Enumerable.Range(0,10).Select(i=>new RoomPoint(.1+i*.04,.4,i,i)).ToArray();
  var settings=new Settings{RoomPoints=before,PreviousRoomPoints=oldTen,RoomMapVersion=2,RoomMapSceneId="scene",SceneHome=new("scene",DateTime.UtcNow,[])};
  string snapshot=JsonSerializer.Serialize(settings);Reject(()=>RecoveryMapExtension.Save(settings,added,()=>throw new IOException("disk full")),"Save failure accepted");
  Check(JsonSerializer.Serialize(settings)==snapshot,"Failed save mutated settings");
  settings.CameraPlacementChanged=true;Reject(()=>RecoveryMapExtension.Save(settings,added,()=>{}),"Moved mounts reused map");settings.CameraPlacementChanged=false;
  settings.PendingSceneScanId="new";Reject(()=>RecoveryMapExtension.Save(settings,added,()=>{}),"Incomplete scan reused map");settings.PendingSceneScanId=null;
  settings.RoomMapSceneId="different";Reject(()=>RecoveryMapExtension.Save(settings,added,()=>{}),"Different placement reused map");settings.RoomMapSceneId="scene";
  string? saved=null;RecoveryMapExtension.Save(settings,added,()=>saved=JsonSerializer.Serialize(settings));
  var loaded=JsonSerializer.Deserialize<Settings>(saved!)!;
  Check(loaded.RoomPoints.SequenceEqual(next)&&loaded.PreviousRoomPoints.SequenceEqual(oldTen)&&loaded.RoomMapVersion==3&&loaded.RoomMapSceneId=="scene"&&loaded.SceneHome?.Id=="scene","Coverage save/reload lost map source or archives");
  Console.WriteLine("PASS: same-placement coverage extension expands measured hull, preserves pairs and old ten points, rejects invalid captures and rolls back failed saves");
 }
}
