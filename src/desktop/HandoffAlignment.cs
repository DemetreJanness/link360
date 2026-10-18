using Link360;
namespace Link360Desktop;

// A transient continuity reference, learned only while both views and the
// driver agree on steady framing. Original calibration coordinates never change.
internal sealed class HandoffAlignment {
 readonly Queue<RoomSyncObservation> samples=new();Pose? pose;
 public RoomAlignment? Reference {get;private set;}
 public void Clear(){samples.Clear();pose=null;Reference=null;}
 public void BreakAcquisition(){samples.Clear();pose=null;}
 public void Observe(RoomMap map,BodyResult room,DateTime roomAt,BodyResult link,DateTime linkAt,CameraReading state,DateTime now){
  var pan=state.Controls.Single(c=>c.Name=="Pan");var tilt=state.Controls.Single(c=>c.Name=="Tilt");var zoom=state.Controls.Single(c=>c.Name=="Zoom");var current=new Pose(pan.Value,tilt.Value,zoom.Value);
  if(room.People.Length!=1||!LinkLock.HasUsableTorso(link)||room.Error!=null||room.Width<=0||room.Height<=0||roomAt>now||linkAt>now||now-roomAt>TimeSpan.FromMilliseconds(300)||now-linkAt>TimeSpan.FromMilliseconds(300)||Math.Abs((roomAt-linkAt).TotalMilliseconds)>120){BreakAcquisition();return;}
  var target=link.People[0];
  // Do not learn a compensation while the motor is catching up or from a
  // different zoom. Fresh driver snapshots are mandatory in the live path.
  var snapshotAge=now-DateTimeOffset.FromUnixTimeMilliseconds(state.SnapshotUtcMilliseconds).UtcDateTime;
  if(Math.Abs(target.CenterX/link.Width-.5)>.045||Math.Abs(target.CenterY/link.Height-.5)>.045||state.CommandedPan!=pan.Value||state.CommandedTilt!=tilt.Value||state.SnapshotUtcMilliseconds<=0||snapshotAge<TimeSpan.Zero||snapshotAge>TimeSpan.FromMilliseconds(200)){BreakAcquisition();return;}
  if(pose!=null&&pose!=current)BreakAcquisition();pose=current;
  if(samples.Count>0&&(roomAt<=samples.Last().RoomAt||linkAt<=samples.Last().LinkAt))return;
  samples.Enqueue(new(room,roomAt,link,linkAt));while(samples.Count>5)samples.Dequeue();
  if(samples.Count<5||now-samples.First().RoomAt<TimeSpan.FromMilliseconds(160))return;
  try{Reference=RoomAlignment.Sync(map,samples.ToArray(),state,now);}catch(IOException){BreakAcquisition();}
 }
}
