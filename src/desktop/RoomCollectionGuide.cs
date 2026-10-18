namespace Link360Desktop;

// Image-space sampling guidance only; these locations are never room measurements.
internal sealed record RoomCollectionGuide(PointF[] Boundary, PointF? Next) {
 public static RoomCollectionGuide Create(RoomPoint[] measurements) {
  var p=measurements.Where(p=>double.IsFinite(p.X)&&double.IsFinite(p.Y)&&p.X>=0&&p.X<=1&&p.Y>=0&&p.Y<=1)
   .Select(p=>new PointF((float)p.X,(float)p.Y)).Distinct().OrderBy(p=>p.X).ThenBy(p=>p.Y).ToArray();
  if(p.Length<3)return new([],null);
  static double Cross(PointF a,PointF b,PointF c)=>(b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);
  var lower=new List<PointF>();var upper=new List<PointF>();
  foreach(var q in p){while(lower.Count>=2&&Cross(lower[^2],lower[^1],q)<=0)lower.RemoveAt(lower.Count-1);lower.Add(q);}
  foreach(var q in p.Reverse()){while(upper.Count>=2&&Cross(upper[^2],upper[^1],q)<=0)upper.RemoveAt(upper.Count-1);upper.Add(q);}
  var hull=lower.SkipLast(1).Concat(upper.SkipLast(1)).ToArray();
  if(hull.Length<3)return new([],null);
  if(measurements.Length>=25)return new(hull,null);
  PointF? best=null;double bestDistance=.07*.07;
  // Stay within measured coverage: the image alone cannot identify walkable floor
  // or guarantee that the Link can see a suggested position.
  for(int y=1;y<80;y++)for(int x=1;x<80;x++){
   var q=new PointF(x/80f,y/80f);
   if(Enumerable.Range(0,hull.Length).Any(i=>Cross(hull[i],hull[(i+1)%hull.Length],q)<-1e-8))continue;
   double distance=p.Min(a=>(double)(a.X-q.X)*(a.X-q.X)+(double)(a.Y-q.Y)*(a.Y-q.Y));
   if(distance>bestDistance){bestDistance=distance;best=q;}
  }
  return new(hull,best);
 }
}
