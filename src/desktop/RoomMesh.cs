using Link360;
namespace Link360Desktop;
// Local interpolation of measured room correspondences, not 3D triangulation.
// Delaunay triangles partition the calibrated convex hull; never extrapolate.
internal sealed class RoomMap {
 readonly RoomPoint[] points;readonly Triangle[] triangles;
 readonly record struct Triangle(int A,int B,int C);
 RoomMap(RoomPoint[] points,Triangle[] triangles){this.points=points;this.triangles=triangles;}
 static double Cross(RoomPoint a,RoomPoint b,RoomPoint c)=>(b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);
 public static RoomMap Fit(RoomPoint[] input) {
  if(input.Length<4||input.Length>25||input.Any(p=>!double.IsFinite(p.X+p.Y)||p.X<0||p.X>1||p.Y<0||p.Y>1))throw new IOException("A room map needs 4–25 valid room positions.");
  var unique=new List<RoomPoint>();foreach(var p in input){var same=unique.FirstOrDefault(q=>Math.Abs(q.X-p.X)<1e-6&&Math.Abs(q.Y-p.Y)<1e-6);if(same!=null){if(Math.Abs(same.Pan-p.Pan)>4||Math.Abs(same.Tilt-p.Tilt)>4)throw new IOException("The same room position has conflicting pan/tilt captures.");continue;}unique.Add(p);}
  if(unique.Count<4||unique.Max(p=>p.X)-unique.Min(p=>p.X)<.15||unique.Max(p=>p.Y)-unique.Min(p=>p.Y)<.06)throw new IOException("Calibration needs four spread-out left/right AND near/far positions.");
  foreach(var p in unique){var others=unique.Where(q=>q!=p).ToArray();if(p.Pan<others.Min(q=>q.Pan)-25||p.Pan>others.Max(q=>q.Pan)+25||p.Tilt<others.Min(q=>q.Tilt)-25||p.Tilt>others.Max(q=>q.Tilt)+25)throw new IOException("One mapping point is an extreme pan/tilt outlier; recheck its capture.");}
  int n=unique.Count;var all=unique.Concat(new[]{new RoomPoint(-20,-20,0,0),new RoomPoint(20,-20,0,0),new RoomPoint(0,20,0,0)}).ToArray();var mesh=new List<Triangle>{new(n,n+1,n+2)};
  bool InsideCircle(Triangle t,RoomPoint p){var a=all[t.A];var b=all[t.B];var c=all[t.C];double ax=a.X-p.X,ay=a.Y-p.Y,bx=b.X-p.X,by=b.Y-p.Y,cx=c.X-p.X,cy=c.Y-p.Y;double determinant=(ax*ax+ay*ay)*(bx*cy-cx*by)-(bx*bx+by*by)*(ax*cy-cx*ay)+(cx*cx+cy*cy)*(ax*by-bx*ay);return Cross(a,b,c)>0?determinant>1e-10:determinant< -1e-10;}
  for(int i=0;i<n;i++){var bad=mesh.Where(t=>InsideCircle(t,all[i])).ToArray();var edges=new Dictionary<(int,int),int>();foreach(var t in bad){foreach(var e in new[]{(t.A,t.B),(t.B,t.C),(t.C,t.A)}){var edge=e.Item1<e.Item2?e:(e.Item2,e.Item1);edges[edge]=edges.GetValueOrDefault(edge)+1;}mesh.Remove(t);}foreach(var edge in edges.Where(e=>e.Value==1).Select(e=>e.Key))if(Math.Abs(Cross(all[edge.Item1],all[edge.Item2],all[i]))>1e-10)mesh.Add(new(edge.Item1,edge.Item2,i));}
  var final=mesh.Where(t=>t.A<n&&t.B<n&&t.C<n).ToArray();if(final.Length==0)throw new IOException("Room mapping positions are collinear; add depth variation.");return new(unique.ToArray(),final);
 }
 public Pose? Target(double x,double y,CameraReading state,bool bounded=true) {
  if(!double.IsFinite(x+y))return null;var query=new RoomPoint(x,y,0,0);
  foreach(var t in triangles){var a=points[t.A];var b=points[t.B];var c=points[t.C];double area=Cross(a,b,c),wA=Cross(query,b,c)/area,wB=Cross(a,query,c)/area,wC=1-wA-wB;if(wA< -1e-8||wB< -1e-8||wC< -1e-8)continue;
   int Axis(string name,double desired){var control=state.Controls.Single(c=>c.Name==name);if(control.Step<=0||control.GetHResult!=0||control.RangeHResult!=0||(control.Capabilities&2)==0)throw new IOException("Room tracking control unavailable.");if(desired<control.Min||desired>control.Max)throw new IOException("Room point outside driver range.");double goal=bounded?control.Value+Math.Clamp(desired-control.Value,-5,5):desired;return (int)Math.Clamp(control.Min+Math.Round((goal-control.Min)/control.Step)*control.Step,control.Min,control.Max);}
   return new(Axis("Pan",wA*a.Pan+wB*b.Pan+wC*c.Pan),Axis("Tilt",wA*a.Tilt+wB*b.Tilt+wC*c.Tilt),state.Controls.Single(c=>c.Name=="Zoom").Value);
  }return null;
 }
}
