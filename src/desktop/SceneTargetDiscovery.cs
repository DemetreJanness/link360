using OpenCvSharp;
namespace Link360Desktop;

internal record SceneTarget(double X,double Y,string Name);

// Input is the just-verified current view. Destinations inside its fixed-feature
// support are only candidates: each still needs independent optical measurement.
internal static class SceneTargetDiscovery {
 public static readonly SceneTarget Centre=new(.5,.5,"centre");
 public const int MaximumPoints=25;
 public static SceneTarget[] Discover(SceneMatch current,Action<object>? evidence=null){
  var points=(current.RoomFeatures??[]).Where(p=>float.IsFinite(p.X+p.Y)&&p.X>=0&&p.X<=1&&p.Y>=0&&p.Y<=1)
   .GroupBy(p=>(Math.Round(p.X*500),Math.Round(p.Y*500))).Select(g=>g.First()).ToArray();
  if(points.Length<16||current.Inliers<16||!double.IsFinite(current.InlierRatio)||current.InlierRatio<.6||current.InlierRatio>1||!double.IsFinite(current.X+current.Y)||Math.Abs(current.X-.5)>=.065||Math.Abs(current.Y-.5)>=.065)
   throw new IOException("Fresh centre features are insufficient for target discovery; no previous targets reused.");
  var hull=Cv2.ConvexHull(points.Select(p=>new Point2f(p.X,p.Y)));
  if(Cv2.PointPolygonTest(hull,new((float)current.X,(float)current.Y),false)<0)
   throw new IOException("Fresh fixed features do not surround the centre; no previous targets reused.");
  var candidates=new List<(double X,double Y,int Support)>();
  foreach(var seed in points){
   var nearby=points.Where(p=>Math.Pow(p.X-seed.X,2)+Math.Pow(p.Y-seed.Y,2)<=.075*.075).ToArray();
   if(nearby.Length<3)continue;
   double x=nearby.Average(p=>(double)p.X),y=nearby.Average(p=>(double)p.Y);
   double dx=x-current.X,dy=y-current.Y;
   if(dx*dx+dy*dy<.10*.10||Math.Abs(dx)>.36||Math.Abs(dy)>.32||Cv2.PointPolygonTest(hull,new((float)x,(float)y),true)<.015)continue;
   candidates.Add((x,y,nearby.Length));
  }
  var result=new List<SceneTarget>{Centre};
  while(result.Count<MaximumPoints&&candidates.Count>0){
   var ranked=candidates.Select(c=>(c,Distance:result.Select((t,i)=>(X:i==0?current.X:t.X,Y:i==0?current.Y:t.Y)).Min(t=>Math.Sqrt(Math.Pow(t.X-c.X,2)+Math.Pow(t.Y-c.Y,2)))))
    .Where(c=>c.Distance>=.10).OrderByDescending(c=>c.Distance+Math.Min(c.c.Support,20)*.002).ThenBy(c=>c.c.X).ThenBy(c=>c.c.Y).ToArray();
   if(ranked.Length==0)break;
   var next=ranked[0].c;result.Add(new(next.X,next.Y,$"point {result.Count}"));candidates.Remove(next);
  }
  evidence?.Invoke(new{type="target-discovery",featureCount=points.Length,remainingCandidates=candidates.Count,centreX=current.X,centreY=current.Y,proposed=result.ToArray()});
  Validate(result,new(current.X,current.Y,0,0,100,current.Inliers,current.InlierRatio));
  return result.ToArray();
 }
 public static void Validate(IReadOnlyList<SceneTarget> targets,SceneAnchor home){
  if(!double.IsFinite(home.X+home.Y)||home.X<0||home.X>1||home.Y<0||home.Y>1)throw new IOException("Scene centre is invalid.");
  if(targets.Count<4||targets.Count>MaximumPoints)throw new IOException("Need at least four fresh supported locations, including the centre; previous points will not be reused.");
  if(!double.IsFinite(targets[0].X+targets[0].Y)||Math.Abs(targets[0].X-.5)>1e-6||Math.Abs(targets[0].Y-.5)>1e-6)
   throw new IOException("Scene target centre is invalid.");
  for(int i=0;i<targets.Count;i++){
   var t=targets[i];
   if(!double.IsFinite(t.X+t.Y)||t.X<0||t.X>1||t.Y<0||t.Y>1||targets.Take(i).Any(p=>Math.Pow(t.X-p.X,2)+Math.Pow(t.Y-p.Y,2)<.07*.07))
    throw new IOException("Scene targets are invalid or too closely repeated.");
  }
  if(targets.Max(t=>t.X)-targets.Min(t=>t.X)<.2||targets.Max(t=>t.Y)-targets.Min(t=>t.Y)<.2)
   throw new IOException("Fresh targets cover too little of the shared view; add fixed detail or improve overlap.");
  var hull=Cv2.ConvexHull(targets.Skip(1).Select(t=>new Point2f((float)t.X,(float)t.Y)));
  if(Cv2.ContourArea(hull)<.02||Cv2.PointPolygonTest(hull,new((float)home.X,(float)home.Y),false)<0)
   throw new IOException("Fresh targets do not surround the centre; add fixed detail around the shared view.");
 }
}