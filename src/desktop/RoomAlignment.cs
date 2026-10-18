using Link360;
namespace Link360Desktop;
internal record RoomSyncObservation(BodyResult Room,DateTime RoomAt,BodyResult Link,DateTime LinkAt);
// Session-only boresight correction. Does not change motor home, room points,
// camera extrinsics or depth calibration; original map hull remains mandatory.
internal record RoomAlignment(int Pan=0,int Tilt=0) {
 public Pose? Apply(Pose target,CameraReading state){
  int? Axis(string name,int value,int offset){var c=state.Controls.Single(c=>c.Name==name);long desired=(long)value+offset;if(c.Step<=0||desired<c.Min||desired>c.Max)return null;int quantized=c.Min+(int)Math.Round((desired-c.Min)/(double)c.Step)*c.Step;return quantized>=c.Min&&quantized<=c.Max?quantized:null;}
  var pan=Axis("Pan",target.Pan,Pan);var tilt=Axis("Tilt",target.Tilt,Tilt);return pan.HasValue&&tilt.HasValue?target with{Pan=pan.Value,Tilt=tilt.Value}:null;
 }
 public static RoomAlignment Sync(RoomMap map,IReadOnlyList<RoomSyncObservation> observations,CameraReading state,DateTime now){
  if(observations.Count<5)throw new IOException("Sync needs five fresh stable observations.");
  var predictions=new List<Pose>();double? startX=null,startY=null;DateTime? roomAt=null,linkAt=null;
  foreach(var o in observations){
   double roomAge=(now-o.RoomAt).TotalMilliseconds,linkAge=(now-o.LinkAt).TotalMilliseconds;
   if(roomAge<0||linkAge<0||roomAge>1200||linkAge>1200||o.RoomAt<=roomAt||o.LinkAt<=linkAt||o.Room.Error!=null||o.Room.Width<=0||o.Room.Height<=0||o.Room.People.Length!=1||!LinkLock.HasUsableTorso(o.Link))throw new IOException("Sync needs one fresh torso in both cameras. Keep still and retry.");
   roomAt=o.RoomAt;linkAt=o.LinkAt;var room=o.Room.People[0];var link=o.Link.People[0];double x=room.CenterX/o.Room.Width,y=room.CenterY/o.Room.Height;
   if(room.Confidence<.5||!double.IsFinite(x+y+room.Confidence)||room.Confidence>1||Math.Abs(link.CenterX/o.Link.Width-.5)>.065||Math.Abs(link.CenterY/o.Link.Height-.5)>.065)throw new IOException("Centre your torso in the Link first, then keep still and sync.");
   startX??=x;startY??=y;if(Math.Abs(x-startX.Value)>.025||Math.Abs(y-startY.Value)>.025)throw new IOException("Subject moved during sync; keep still and retry.");
   predictions.Add(map.Target(x,y,state,bounded:false)??throw new IOException("Stand inside the mapped room area to sync."));
  }
  if(now-observations[^1].RoomAt>TimeSpan.FromMilliseconds(300)||now-observations[^1].LinkAt>TimeSpan.FromMilliseconds(300))throw new IOException("Latest sync frames are stale; retry with live previews.");
  int Value(string name){var c=state.Controls.Single(c=>c.Name==name);if(c.GetHResult!=0||c.RangeHResult!=0)throw new IOException("Camera position unavailable.");return c.Value;}
  int pan=Value("Pan")-predictions.Select(p=>p.Pan).Order().ElementAt(predictions.Count/2),tilt=Value("Tilt")-predictions.Select(p=>p.Tilt).Order().ElementAt(predictions.Count/2);
  if(Math.Abs(pan)>10||Math.Abs(tilt)>10)throw new IOException("Alignment differs too much; check camera placement or recalibrate. Saved points are unchanged.");
  var alignment=new RoomAlignment(pan,tilt);if(alignment.Apply(predictions[^1],state)==null)throw new IOException("Alignment exceeds the camera range.");return alignment;
 }
}
