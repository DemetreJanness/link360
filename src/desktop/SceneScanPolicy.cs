using Link360;
namespace Link360Desktop;

internal static class SceneScanPolicy {
 // Legacy saved references only. New scans discover their own targets.
 public static readonly (double X,double Y,string Name)[] Targets=[(.5,.5,"centre"),(.15,.2,"upper left"),(.85,.2,"upper right"),(.85,.8,"lower right"),(.15,.8,"lower left")];
 public static string? PlacementId(Settings settings)=>settings.GuidedPlacementId??settings.SceneHome?.Id;
 public static void RequireScene(Settings settings,bool confirmed,bool collecting){
  if(settings.PendingSceneScanId!=null)throw new IOException("The new scene scan is incomplete. Previous points are archived and will not be reused.");
  if(settings.SceneHome!=null){ValidateAnchors(settings.SceneHome.Anchors,settings.SceneHome.Targets);if(string.IsNullOrWhiteSpace(settings.SceneHome.Id))throw new IOException("Scene reference is invalid; run setup again.");}
  if(settings.GuidedPlacementId!=null&&(string.IsNullOrWhiteSpace(settings.GuidedPlacementId)||settings.SceneHome!=null))throw new IOException("Calibration placement is invalid; start setup again.");
  var placement=PlacementId(settings);
  if(settings.CameraPlacementChanged||placement!=null&&!confirmed||collecting&&placement==null)throw new IOException("Start room scene setup or guided calibration, then confirm the camera mounts have not moved.");
  if(!collecting&&placement!=null&&settings.RoomMapSceneId!=placement)throw new IOException("Collect a new room map for this placement. Previous points are retained.");
 }
 public static void Fresh(FeedFrame frame,DateTime now,DateTime after){
  if(frame.ReadAtUtc is not DateTime read||read<=after||read>now.AddMilliseconds(100)||now-read>TimeSpan.FromMilliseconds(900))throw new IOException("Waiting for fresh direct-camera frames; no scene movement sent.");
 }
 // A driver-space starting view, not a measured room centre or a saved anchor.
 // Existing Link standard-control units: pan/tilt zero and widest zoom 100.
 public static Pose NeutralStart(CameraReading state,BodyResult room,BodyResult link){
  if(state.Error!=null||state.NativeTrackingEnabled!=false)throw new IOException("Built-in tracking must be confirmed off before initial framing.");
  if(room.Error!=null||link.Error!=null||room.Width<=0||room.Height<=0||link.Width<=0||link.Height<=0||room.People.Length!=0||link.People.Length!=0)
   throw new IOException("Leave both camera views empty before initial framing.");
  int Axis(string name,int target,int maxTravel){
   var controls=state.Controls.Where(c=>c.Name==name).ToArray();
   if(controls.Length!=1)throw new IOException("Initial framing needs one verified "+name+" control.");
   var c=controls[0];
   if(c.RangeHResult!=0||c.GetHResult!=0||c.Step<=0||c.Min>c.Max||(c.Capabilities&2)==0||c.Flags!=2||c.Value<c.Min||c.Value>c.Max||target<c.Min||target>c.Max||((long)target-c.Min)%c.Step!=0)
    throw new IOException("Initial framing is unavailable for the reported "+name+" range, step or flags.");
   if(Math.Abs((long)target-c.Value)>maxTravel)throw new IOException("Initial framing exceeds the scan travel limit; face the Link toward the room and retry.");
   return target;
  }
  return new(Axis("Pan",0,60),Axis("Tilt",0,40),Axis("Zoom",100,300));
 }
 public static Pose Next(SceneMatch match,double x,double y,CameraReading state,Pose origin,bool reversePan,bool reverseTilt){
  double ex=x-match.X,ey=y-match.Y;
  if(!double.IsFinite(ex+ey)||x<0||x>1||y<0||y>1)throw new IOException("Invalid scene target.");
  int Axis(string name,double error,int sign){var c=state.Controls.Single(c=>c.Name==name);if(c.Step<=0||c.RangeHResult!=0||c.GetHResult!=0||(c.Capabilities&2)==0)throw new IOException("PTZ scene control unavailable.");int delta=Math.Abs(error)<.065?0:Math.Max(c.Step,(int)Math.Clamp(Math.Round(Math.Abs(error)*20),1,4)/c.Step*c.Step)*Math.Sign(error)*sign;return Math.Clamp(c.Value+delta,c.Min,c.Max);}
  var next=new Pose(Axis("Pan",ex,reversePan?-1:1),Axis("Tilt",ey,reverseTilt?1:-1),state.Controls.Single(c=>c.Name=="Zoom").Value);
  if(Math.Abs(next.Pan-origin.Pan)>60||Math.Abs(next.Tilt-origin.Tilt)>40)throw new IOException("Scan reached its travel limit. More overlap between cameras is needed.");
  if(next.Pan==state.Controls.Single(c=>c.Name=="Pan").Value&&next.Tilt==state.Controls.Single(c=>c.Name=="Tilt").Value)throw new IOException("Boundary target is outside the reachable camera overlap.");
  return next;
 }
 public static void ValidateAnchors(IReadOnlyList<SceneAnchor> anchors,IReadOnlyList<SceneTarget>? targets=null){
  if(anchors.Count<4||anchors.Count>25)throw new IOException("Need 4–25 freshly verified scene points before saving.");
  if(targets==null&&anchors.Count!=Targets.Length||targets!=null&&anchors.Count!=targets.Count)throw new IOException("Every saved scene point needs its own measurement.");
  targets??=Targets.Select(t=>new SceneTarget(t.X,t.Y,t.Name)).ToArray();SceneTargetDiscovery.Validate(targets,anchors[0]);
  for(int i=0;i<anchors.Count;i++){var a=anchors[i];var t=targets[i];if(!double.IsFinite(a.X+a.Y+a.InlierRatio)||a.X<0||a.X>1||a.Y<0||a.Y>1||a.Inliers<16||a.InlierRatio<.6||a.InlierRatio>1)throw new IOException("A scene boundary anchor did not pass verification.");
   if(a.BoundaryLimited){if(!BoundaryCandidate(new(a.X,a.Y,a.Inliers,a.InlierRatio),i,anchors[0],targets))throw new IOException("Limited scene boundary has insufficient measured coverage.");}
   else if(Math.Abs(a.X-t.X)>=.065||Math.Abs(a.Y-t.Y)>=.065)throw new IOException("A scene boundary anchor did not pass verification.");
  }
  if(anchors.Where((a,i)=>anchors.Take(i).Any(p=>Math.Pow(a.X-p.X,2)+Math.Pow(a.Y-p.Y,2)<.06*.06)).Any())throw new IOException("Measured scene points are too closely repeated.");
  if(anchors.Max(a=>a.X)-anchors.Min(a=>a.X)<.2||anchors.Max(a=>a.Y)-anchors.Min(a=>a.Y)<.2)throw new IOException("Shared scene coverage is too small; no usable reference saved.");
  var hull=OpenCvSharp.Cv2.ConvexHull(anchors.Skip(1).Select(a=>new OpenCvSharp.Point2f((float)a.X,(float)a.Y)));
  if(OpenCvSharp.Cv2.ContourArea(hull)<.02||OpenCvSharp.Cv2.PointPolygonTest(hull,new((float)anchors[0].X,(float)anchors[0].Y),false)<0)throw new IOException("Measured boundaries do not surround the scene centre.");
 }
 public static bool BoundaryCandidate(SceneMatch match,int index,SceneAnchor home,IReadOnlyList<SceneTarget>? targets=null){
  if(index<=0||index>=(targets?.Count??Targets.Length)||!double.IsFinite(match.X+match.Y))return false;
  var t=targets?[index]??new SceneTarget(Targets[index].X,Targets[index].Y,Targets[index].Name);double dx=match.X-home.X,dy=match.Y-home.Y;
  double tx=t.X-home.X,ty=t.Y-home.Y,length=Math.Sqrt(tx*tx+ty*ty);
  if(length<.1)return false;
  return (dx*tx+dy*ty)/length>=.10&&Math.Abs(dx*ty-dy*tx)/length<=.065&&Math.Sqrt(dx*dx+dy*dy)>=.12
   &&match.X>=Math.Min(t.X,home.X)-.065&&match.X<=Math.Max(t.X,home.X)+.065
   &&match.Y>=Math.Min(t.Y,home.Y)-.065&&match.Y<=Math.Max(t.Y,home.Y)+.065;
 }
}
