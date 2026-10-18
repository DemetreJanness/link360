using Link360;
using Link360Desktop;
using OpenCvSharp;
internal static class SceneTests {
 static void Check(bool condition,string reason){if(!condition)throw new Exception(reason);}
 static void Reject(Action action,string reason){bool rejected=false;try{action();}catch(IOException){rejected=true;}Check(rejected,reason);}
 internal static Mat Texture(){var image=new Mat(360,640,MatType.CV_8UC3,new Scalar(40,40,40));var random=new Random(8301);for(int i=0;i<350;i++){var at=new Point(random.Next(20,620),random.Next(20,340));var colour=new Scalar(random.Next(60,255),random.Next(60,255),random.Next(60,255));Cv2.Circle(image,at,random.Next(3,10),colour,2);if(i%8==0)Cv2.PutText(image,i.ToString(),at,HersheyFonts.HersheySimplex,.4,colour,1);}return image;}
 public static void Run(){
  var devices=new[]{new VideoDevice(0,"Insta360 Link","link-a"),new VideoDevice(1,"Acer FHD User Facing","room-a")};
  var reordered=new[]{devices[1] with{Index=0},devices[0] with{Index=2},new VideoDevice(1,"Other webcam","other")};
  Check(CameraInventory.Changed(devices,reordered,true)==null,"Unrelated device notification interrupted cameras");
  Check(CameraInventory.Changed(devices,[devices[0]],false)==null,"Unused Acer interrupted Link-only tracking");
  Check(CameraInventory.Changed(devices,[devices[0]],true)=="Acer FHD User Facing","Active Acer disconnect was ignored");
  Check(CameraInventory.Changed(devices,[devices[1]],false)=="Insta360 Link","Link disconnect was ignored");
  Check(CameraInventory.Changed(devices,[devices[0] with{Path="link-b"},devices[1]],true)=="Insta360 Link","Changed camera identity was ignored");
  Check(CameraInventory.Changed(devices,[..devices,devices[0] with{Path="duplicate"}],true)=="Insta360 Link","Ambiguous Link identity was ignored");
  Check(CaptureDetail.For("link",false)==new CaptureDetail(320,180,80),"Tracking capture contract changed");
  Check(CaptureDetail.For("link",true)==new CaptureDetail(640,360,92)&&CaptureDetail.For("room",true)==new CaptureDetail(640,360,92),"Scene capture lost fixed-feature detail");
  using var room=Texture();using var link=new Mat();using var transform=Mat.Eye(2,3,MatType.CV_64F).ToMat();transform.Set(0,2,-64d);transform.Set(1,2,18d);Cv2.WarpAffine(room,link,transform,room.Size());
  var r=new BodyResult([],640,360,"fixture",0);var roomBytes=room.ToBytes(".png");var linkBytes=link.ToBytes(".png");var match=SceneRegistration.Match(roomBytes,linkBytes,r,r);
  Check(Math.Abs(match.X-.6)<.012&&Math.Abs(match.Y-.45)<.012,"Scene registration failed a known view offset");
  var detailed=SceneRegistration.Match(roomBytes,linkBytes,r,r,siftOnly:true);Check(Math.Abs(detailed.X-.6)<.012&&Math.Abs(detailed.Y-.45)<.012,"SIFT registration failed a known view offset");
  Reject(()=>SceneRegistration.Match(roomBytes,linkBytes,r,r with{People=[new(20,20,100,200,70,100,.9)]}),"Scene accepted a visible person");
  var masked=r with{Objects=[new(0,0,640,360,.9,"chair",56)]};Reject(()=>SceneRegistration.Match(roomBytes,linkBytes,masked,masked),"Movable object texture became a room anchor");
  using var blank=new Mat(360,640,MatType.CV_8UC3,Scalar.Black);Reject(()=>SceneRegistration.Match(blank.ToBytes(".png"),linkBytes,r,r),"Featureless scene accepted");
  SceneRegistration.Stable(roomBytes,roomBytes,r,r);using var moving=room.Clone();Cv2.Rectangle(moving,new Rect(180,80,220,180),Scalar.White,-1);var movingBytes=moving.ToBytes(".png");Reject(()=>SceneRegistration.Stable(roomBytes,movingBytes,r,r),"Moving background accepted");
  var cup=r with{Objects=[new(180,80,220,180,.8,"cup",41)]};SceneRegistration.Stable(roomBytes,movingBytes,cup,cup);
  var now=DateTime.UtcNow;SceneScanPolicy.Fresh(new([],now,now),now,now.AddSeconds(-1));Reject(()=>SceneScanPolicy.Fresh(new([],now.AddSeconds(-3),now),now,now.AddSeconds(-4)),"Old scan frame accepted");Reject(()=>SceneScanPolicy.Fresh(new([],null,now),now,now.AddSeconds(-4)),"Unstamped scan frame accepted");
  var state=new CameraReading{Controls=[new(){Name="Pan",Min=-145,Max=145,Step=1,Capabilities=2,Value=0},new(){Name="Tilt",Min=-90,Max=100,Step=1,Capabilities=2,Value=0},new(){Name="Zoom",Min=100,Max=400,Step=1,Capabilities=2,Value=100}]};
  var next=SceneScanPolicy.Next(new(.2,.2,30,.9),.5,.5,state,new(0,0,100),false,false);Check(next.Pan==4&&next.Tilt==-4&&next.Zoom==100,"Scene motion directions or bounds failed");Reject(()=>SceneScanPolicy.Next(new(.2,.2,30,.9),.5,.5,state,new(-60,0,100),false,false),"Scan travel envelope bypassed");
  var anchors=SceneScanPolicy.Targets.Select(t=>new SceneAnchor(t.X,t.Y,0,0,100,30,.9)).ToArray();SceneScanPolicy.ValidateAnchors(anchors);Reject(()=>SceneScanPolicy.ValidateAnchors(anchors[..4]),"Partial scene marked complete");
  var limited=new[]{anchors[0],new SceneAnchor(.38,.36,-8,7,100,40,.9,BoundaryLimited:true),new SceneAnchor(.68,.35,12,8,100,45,.9,BoundaryLimited:true),new SceneAnchor(.67,.70,12,-10,100,35,.85,BoundaryLimited:true),new SceneAnchor(.32,.68,-12,-8,100,38,.9,BoundaryLimited:true)};
  SceneScanPolicy.ValidateAnchors(limited);
  Check(SceneScanPolicy.BoundaryCandidate(new(.38,.36,40,.9),1,anchors[0]),"Supported upper-left boundary rejected");
  Check(!SceneScanPolicy.BoundaryCandidate(new(.7,.3,40,.9),1,anchors[0]),"Wrong quadrant became a measured boundary");
  Check(!SceneScanPolicy.BoundaryCandidate(new(.48,.48,40,.9),1,anchors[0]),"Negligible travel became a boundary");
  Reject(()=>SceneScanPolicy.ValidateAnchors([anchors[0],limited[1],limited[1],limited[3],limited[4]]),"Duplicated scene boundary accepted");
  var privateAnchor=anchors[0] with{RoomObjects=[new(1,2,20,30,.8,"cup",41)]};
  var settingsJson=System.Text.Json.JsonSerializer.Serialize(new Settings{SceneHome=new("fixture",now,[privateAnchor])});Check(!settingsJson.Contains("cup")&&!settingsJson.Contains("RoomObjects"),"Object inventory leaked into project settings");
  var reportJson=System.Text.Json.JsonSerializer.Serialize(new{Objects=privateAnchor.RoomObjects});Check(reportJson.Contains("cup"),"Local report lost inventory evidence");
  var settings=new Settings{RoomPoints=[new(.2,.2,1,2)]};SceneScanPolicy.RequireScene(settings,false,false);Reject(()=>SceneScanPolicy.RequireScene(settings,false,true),"New point collection bypassed scene setup");settings.CameraPlacementChanged=true;Reject(()=>SceneScanPolicy.RequireScene(settings,true,false),"Moved cameras reused stale map");settings.CameraPlacementChanged=false;settings.SceneHome=new("scene",now,anchors);Reject(()=>SceneScanPolicy.RequireScene(settings,false,true),"Unconfirmed placement accepted");Reject(()=>SceneScanPolicy.RequireScene(settings,true,false),"Old map accepted after new scene");settings.RoomMapSceneId="scene";SceneScanPolicy.RequireScene(settings,true,false);Check(settings.RoomPoints.Length==1,"Scene gates changed saved points");
  // Guided collection is a separate placement, never a fabricated scene scan.
  var oldPoints=settings.RoomPoints.ToArray();var guided=new Settings{GuidedPlacementId="guided-new",RoomMapSceneId="old",RoomPoints=oldPoints};
  SceneScanPolicy.RequireScene(guided,true,true);
  Reject(()=>SceneScanPolicy.RequireScene(guided,true,false),"Old map reused for guided placement");
  Reject(()=>SceneScanPolicy.RequireScene(guided,false,true),"Unconfirmed guided placement accepted");
  guided.CameraPlacementChanged=true;Reject(()=>SceneScanPolicy.RequireScene(guided,true,true),"Moved guided placement collected points");Reject(()=>SceneScanPolicy.RequireScene(guided,true,false),"Moved guided placement tracked");
  guided.CameraPlacementChanged=false;guided.RoomMapSceneId="guided-new";SceneScanPolicy.RequireScene(guided,true,false);
  Check(guided.SceneHome==null&&guided.RoomPoints.SequenceEqual(oldPoints),"Guided policy fabricated anchors or changed old points");
  var guidedJson=System.Text.Json.JsonSerializer.Serialize(guided);var reloaded=System.Text.Json.JsonSerializer.Deserialize<Settings>(guidedJson)!;SceneScanPolicy.RequireScene(reloaded,true,false);
  Reject(()=>SceneScanPolicy.RequireScene(reloaded,false,false),"Reload silently confirmed guided mounts");
  reloaded.GuidedPlacementId=" ";Reject(()=>SceneScanPolicy.RequireScene(reloaded,true,true),"Blank guided placement accepted");
  reloaded.GuidedPlacementId="guided-new";reloaded.SceneHome=new("scene",now,anchors);Reject(()=>SceneScanPolicy.RequireScene(reloaded,true,true),"Conflicting scene and guided placements accepted");
  Console.WriteLine("PASS: guided placement isolation, old-map rejection, mount changes, explicit confirmation and settings round trip");
  using var voice=new CalibrationAudio();voice.ValidateSceneAssets();Check(ObjectLabels.Name(41)=="cup"&&ObjectLabels.Name(56)=="chair"&&ObjectLabels.Name(79)=="toothbrush","Object labels do not match the pinned model");
  Console.WriteLine("PASS: scene registration, movable-object masking, motion rejection, freshness, bounded PTZ, five-anchor completion, placement gates, camera identity changes, capture detail and local voice assets");
 }
}
