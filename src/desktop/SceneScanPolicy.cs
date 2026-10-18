using Link360;
namespace Link360Desktop;

internal static class SceneScanPolicy {
 public static readonly (double X,double Y,string Name)[] Targets=[(.5,.5,"centre"),(.15,.2,"upper left"),(.85,.2,"upper right"),(.85,.8,"lower right"),(.15,.8,"lower left")];
 public static string? PlacementId(Settings settings)=>settings.GuidedPlacementId??settings.SceneHome?.Id;
 public static void RequireScene(Settings settings,bool confirmed,bool collecting){
  if(settings.SceneHome!=null){ValidateAnchors(settings.SceneHome.Anchors);if(string.IsNullOrWhiteSpace(settings.SceneHome.Id))throw new IOException("Scene reference is invalid; run setup again.");}
  if(settings.GuidedPlacementId!=null&&(string.IsNullOrWhiteSpace(settings.GuidedPlacementId)||settings.SceneHome!=null))throw new IOException("Calibration placement is invalid; start setup again.");
  var placement=PlacementId(settings);
  if(settings.CameraPlacementChanged||placement!=null&&!confirmed||collecting&&placement==null)throw new IOException("Start room scene setup or guided calibration, then confirm the camera mounts have not moved.");
  if(!collecting&&placement!=null&&settings.RoomMapSceneId!=placement)throw new IOException("Collect a new room map for this placement. Previous points are retained.");
 }
 public static void Fresh(FeedFrame frame,DateTime now,DateTime after){
  if(frame.ReadAtUtc is not DateTime read||read<=after||read>now.AddMilliseconds(100)||now-read>TimeSpan.FromMilliseconds(900))throw new IOException("Waiting for fresh direct-camera frames; no scene movement sent.");
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
 public static void ValidateAnchors(IReadOnlyList<SceneAnchor> anchors){
  if(anchors.Count!=Targets.Length)throw new IOException("Scene needs all five measured anchors.");
  for(int i=0;i<anchors.Count;i++){var a=anchors[i];var t=Targets[i];if(!double.IsFinite(a.X+a.Y+a.InlierRatio)||a.X<0||a.X>1||a.Y<0||a.Y>1||a.Inliers<16||a.InlierRatio<.6||a.InlierRatio>1)throw new IOException("A scene boundary anchor did not pass verification.");
   if(a.BoundaryLimited){if(!BoundaryCandidate(new(a.X,a.Y,a.Inliers,a.InlierRatio),i,anchors[0]))throw new IOException("Limited scene boundary has insufficient measured coverage.");}
   else if(Math.Abs(a.X-t.X)>=.065||Math.Abs(a.Y-t.Y)>=.065)throw new IOException("A scene boundary anchor did not pass verification.");
  }
  if(anchors.Max(a=>a.X)-anchors.Min(a=>a.X)<.2||anchors.Max(a=>a.Y)-anchors.Min(a=>a.Y)<.2)throw new IOException("Shared scene coverage is too small; no usable reference saved.");
  var hull=OpenCvSharp.Cv2.ConvexHull(anchors.Skip(1).Select(a=>new OpenCvSharp.Point2f((float)a.X,(float)a.Y)));
  if(OpenCvSharp.Cv2.PointPolygonTest(hull,new((float)anchors[0].X,(float)anchors[0].Y),false)<0)throw new IOException("Measured boundaries do not surround the scene centre.");
 }
 public static bool BoundaryCandidate(SceneMatch match,int index,SceneAnchor home){
  if(index<=0||index>=Targets.Length||!double.IsFinite(match.X+match.Y))return false;
  var t=Targets[index];double dx=match.X-home.X,dy=match.Y-home.Y;
  return dx*Math.Sign(t.X-home.X)>=.06&&dy*Math.Sign(t.Y-home.Y)>=.06&&Math.Sqrt(dx*dx+dy*dy)>=.12
   &&match.X>=Math.Min(t.X,home.X)-.065&&match.X<=Math.Max(t.X,home.X)+.065
   &&match.Y>=Math.Min(t.Y,home.Y)-.065&&match.Y<=Math.Max(t.Y,home.Y)+.065;
 }
}
