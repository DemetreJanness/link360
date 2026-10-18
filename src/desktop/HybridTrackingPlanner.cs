using Link360;
namespace Link360Desktop;
internal record TrackingDecision(Pose? Target,bool Hold,GuidanceSource Source,string Outcome,string Status,double? RoomX=null,double? RoomY=null,double SpeedScale=.15,int? RelativePan=null,int? RelativeTilt=null);
// Camera-independent policy. A reliable Link torso can continue without Acer;
// Acer supplies calibrated absolute goals when the Link cannot lock a torso.
internal sealed class HybridTrackingPlanner(RoomMap map,RoomAlignment? alignment=null) {
 readonly TrackingPlanner fine=new();readonly AdaptiveAim roomAim=new();readonly HybridHandoff handoff=new();
 readonly HandoffAlignment continuity=new();int? referenceZoom;
 public RoomAlignment? RecoveryAlignment=>continuity.Reference;
 DateTime lastRoom=DateTime.MinValue,lastLink=DateTime.MinValue,lastRoomDemand=DateTime.MinValue;
 (double X,double Y)? previousRoom,filteredRoom;int stable;
 public TrackingDecision Evaluate(BodyResult? room,DateTime roomAt,BodyResult? link,DateTime linkAt,CameraReading state,bool reversePan,bool reverseTilt,DateTime now){
  double roomAge=(now-roomAt).TotalMilliseconds;double? linkAge=link==null?null:(now-linkAt).TotalMilliseconds;
  bool roomValid=room!=null&&room.Error==null&&room.Width>0&&room.Height>0&&room.People.Length==1&&roomAge>=0&&roomAge<600;
  double? x=null,y=null;
  if(roomValid){var p=room!.People[0];x=p.CenterX/room.Width;y=p.CenterY/room.Height;roomValid=double.IsFinite(x.Value+y.Value+p.Confidence)&&p.Confidence>=TrackingConfidence.Minimum&&p.Confidence<=1&&x>=0&&x<=1&&y>=0&&y<=1;}
  if(roomValid&&roomAt!=lastRoom){stable=previousRoom is {} prev&&Math.Abs(x!.Value-prev.X)<.2&&Math.Abs(y!.Value-prev.Y)<.2?stable+1:1;if(stable==1)continuity.Clear();previousRoom=(x!.Value,y!.Value);filteredRoom=roomAim.Update(x.Value,y.Value,roomAt);lastRoom=roomAt;}
  if(!roomValid){stable=0;previousRoom=null;filteredRoom=null;roomAim.Reset();continuity.Clear();}
  int zoom=state.Controls.Single(c=>c.Name=="Zoom").Value;if(referenceZoom!=zoom){continuity.Clear();referenceZoom=zoom;}
  int roomCount=room?.People.Length??0;if(roomCount==1&&!roomValid)roomCount=0;
  var source=handoff.Select(roomCount,roomAge,LinkLock.GuidanceCount(link),linkAge,now);
  if(roomCount>1||link?.People.Length>1)continuity.Clear();
  if(source==GuidanceSource.Link&&roomValid&&link!=null)continuity.Observe(map,room!,roomAt,link,linkAt,state,now);else continuity.BreakAcquisition();
  if(source==GuidanceSource.Hold){fine.Reset();string status=roomCount>1||link?.People.Length>1?"Multiple people · holding camera":"Waiting for a stable torso · holding camera";return new(null,true,source,"handoff-hold",status,x,y);}
  int Value(string name)=>state.Controls.Single(c=>c.Name==name).Value;
  if(source==GuidanceSource.Link){if(linkAt==lastLink)return new(null,false,source,"fine-wait","Link subject locked · waiting for a new frame",x,y);lastLink=linkAt;var target=fine.PlanBody(link!,state,reversePan,reverseTilt,linkAt);return new(target,target==null,source,target==null?"fine-hold":"fine-centering",target==null?"Subject framed · steady camera":"Link subject locked · following movement",x,y,fine.SpeedScale,target==null?null:target.Pan-Value("Pan"),target==null?null:target.Tilt-Value("Tilt"));}
  fine.Reset();
  if(!roomValid||stable<2||filteredRoom==null)return new(null,true,source,"acquiring","Acer acquiring torso · holding camera",x,y);
  if(roomAt==lastRoomDemand)return new(null,false,source,"room-wait","Acer guidance · waiting for a new frame",x,y);
  lastRoomDemand=roomAt;
  if(map.Target(x!.Value,y!.Value,state,bounded:false)==null)return new(null,true,source,"outside-map","Acer target outside mapped area · holding camera",x,y);
  var coarse=map.Target(filteredRoom.Value.X,filteredRoom.Value.Y,state,bounded:false);
  if(coarse==null)return new(null,true,source,"outside-map","Acer target outside mapped area · holding camera",x,y);
  coarse=(continuity.Reference??alignment??new()).Apply(coarse,state);
  if(coarse==null)return new(null,true,source,"alignment-range","Aligned target outside camera range · holding camera",x,y);
  double speed=Math.Clamp(Math.Max(Math.Abs(coarse.Pan-Value("Pan")),Math.Abs(coarse.Tilt-Value("Tilt")))/5.0,.15,1);
  return new(coarse,false,source,"room-reacquiring",continuity.Reference==null?"Acer torso guides the Link · reacquiring":"Acer follows movement · keeping the last steady framing",x,y,speed);
 }
}
