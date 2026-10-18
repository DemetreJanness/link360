using Link360;
namespace Link360Desktop;
// Single visible person is an acquisition hint, not a head lock or identity.
// Both cameras must be unambiguous. Never infer tilt from this coarse box.
internal static class BodyReacquisition {
 public static BodyResult? Horizontal(BodyResult? link){
  if(link==null||link.Error!=null||link.Width<=0||link.Height<=0||link.People.Length!=1)return null;
  var p=link.People[0];double x=p.X+p.Width*.5,y=p.Y+p.Height*.4;
  if(!double.IsFinite(p.X+p.Y+p.Width+p.Height+p.Confidence)||p.Confidence<Math.Max(.55,TrackingConfidence.Minimum)||p.Confidence>1||p.Width/link.Width<.12||p.Height/link.Height<.30||p.X<0||p.Y<0||p.X+p.Width>link.Width+1||p.Y+p.Height>link.Height+1)return null;
  return link with{People=[p with{CenterX=x,CenterY=y}],AnchorKind="box"};
 }
}
internal sealed class FreshCoverageSession {
 readonly List<RoomPoint> points=[];readonly List<(double X,double Y)> stable=[];
 DateTime lastRoom,lastLink,started;Pose? lastPose;
 public string Id {get;private set;}=Guid.NewGuid().ToString("N");
 public static FreshCoverageSession Restore(string id,string json){
  if(!Guid.TryParseExact(id,"N",out _))throw new IOException("Invalid debug generation");
  using var doc=System.Text.Json.JsonDocument.Parse(json);var root=doc.RootElement;
  if(root.GetProperty("Id").GetString()!=id||root.GetProperty("consumed").GetBoolean()||root.GetProperty("savedMapReused").GetBoolean())throw new IOException("Debug generation is not resumable");
  var pairs=System.Text.Json.JsonSerializer.Deserialize<RoomPoint[]>(root.GetProperty("points").GetRawText())??[];
  if(pairs.Length>25||pairs.Any(p=>!double.IsFinite(p.X+p.Y)||p.X<0||p.X>1||p.Y<0||p.Y>1))throw new IOException("Invalid debug captures");
  var session=new FreshCoverageSession(){Id=id};session.points.AddRange(pairs);try{session.Map=RoomMap.Fit(pairs);}catch(IOException){if(root.GetProperty("ready").GetBoolean())throw;}
  session.Status=$"Resuming {pairs.Length} fresh points from this untested generation.";return session;
 }
 public RoomPoint[] Points=>points.ToArray();public RoomMap? Map {get;private set;}
 public bool Consumed {get;set;} public string Status {get;private set;}="Start in view; move across the room and pause when framed.";
 public double Progress {get;private set;}
 void Reset(string reason){stable.Clear();started=default;lastPose=null;Progress=0;Status=reason;}
 public bool Observe(BodyResult? room,DateTime roomAt,BodyResult? link,DateTime linkAt,CameraReading state,DateTime now){
  if(Consumed)return false;
  if(room==null||room.Error!=null||room.Width<=0||room.Height<=0||room.People.Length!=1||!LinkLock.HasUsableTorso(link)||link!.AnchorKind is not ("head" or "upper-body")){Reset("Need one clear person in Acer and a head/shoulder lock in Link. Face the Link.");return false;}
  if(roomAt>now||linkAt>now||now-roomAt>TimeSpan.FromMilliseconds(300)||now-linkAt>TimeSpan.FromMilliseconds(300)||Math.Abs((roomAt-linkAt).TotalMilliseconds)>150){Reset("Waiting for fresh, paired camera frames.");return false;}
  var p=room.People[0];double x=p.CenterX/room.Width,y=p.CenterY/room.Height;
  if(!double.IsFinite(x+y+p.Confidence)||x<0||x>1||y<0||y>1||p.Confidence<TrackingConfidence.Minimum||p.Confidence>1){Reset("Acer target uncertain; capture held.");return false;}
  var q=link.People[0];if(Math.Abs(q.CenterX/link.Width-.5)>.065||Math.Abs(q.CenterY/link.Height-.5)>.065){Reset("Link is framing you; pause until the target is green.");return false;}
  if(state.Error!=null||state.SnapshotUtcMilliseconds<=0||now-DateTimeOffset.FromUnixTimeMilliseconds(state.SnapshotUtcMilliseconds).UtcDateTime is var age&&(age<TimeSpan.Zero||age>TimeSpan.FromMilliseconds(250))||state.NativePanSpeed!=0||state.NativeTiltSpeed!=0){Reset("Waiting for the motor to stop and fresh angle readback.");return false;}
  int V(string name){var c=state.Controls.Single(c=>c.Name==name);if(c.GetHResult!=0||c.RangeHResult!=0||c.Step<=0||(c.Capabilities&2)==0||c.Value<c.Min||c.Value>c.Max)throw new IOException("Fresh calibration camera state invalid");return c.Value;}
  var pose=new Pose(V("Pan"),V("Tilt"),V("Zoom"));
  if(points.Count>=25){Reset("25 fresh points collected. Run the recovery test.");return false;}
  if(points.Any(a=>Math.Pow(a.X-x,2)+Math.Pow(a.Y-y,2)<.04*.04)){Reset($"{points.Count} fresh points. Move farther toward a room edge, or nearer/farther.");return false;}
  if(lastPose!=pose||stable.Count>0&&(Math.Abs(stable[0].X-x)>.02||Math.Abs(stable[0].Y-y)>.02)){Reset("Hold your new position while the cameras settle.");lastPose=pose;}
  if(roomAt<=lastRoom||linkAt<=lastLink)return false;lastRoom=roomAt;lastLink=linkAt;
  if(started==default)started=now;lastPose=pose;stable.Add((x,y));Progress=Math.Clamp((now-started).TotalMilliseconds/1200,0,1);Status=$"Hold still · {Progress:P0} · {points.Count} fresh points";
  if(Progress<1||stable.Count<6)return false;
  var measured=new RoomPoint(stable.Average(a=>a.X),stable.Average(a=>a.Y),pose.Pan,pose.Tilt);var next=points.Append(measured).ToArray();RoomMap? map=null;
  try{map=RoomMap.Fit(next);}catch(IOException e){if(next.Length>=4&&e.Message.Contains("outlier")){Reset("Capture rejected: angle conflicts with other fresh points. Retry here.");return false;}}
  points.Add(measured);Map=map;Reset(map==null?$"{points.Count} captured. Need left/right AND near/far spread for a usable map.":$"{points.Count} fresh points · map ready. Add edges or run the recovery test.");return true;
 }
}
