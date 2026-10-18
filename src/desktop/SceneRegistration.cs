using OpenCvSharp;
using OpenCvSharp.Features2D;
namespace Link360Desktop;

internal record SceneAnchor(double X,double Y,int Pan,int Tilt,int Zoom,int Inliers,double InlierRatio,
 [property:System.Text.Json.Serialization.JsonIgnore] SceneObject[]? RoomObjects=null,
 [property:System.Text.Json.Serialization.JsonIgnore] SceneObject[]? LinkObjects=null,bool BoundaryLimited=false);
internal record SceneReference(string Id,DateTime CapturedUtc,SceneAnchor[] Anchors);
internal record SceneMatch(double X,double Y,int Inliers,double InlierRatio,PointF[]? RoomFeatures=null,PointF[]? LinkFeatures=null);

// A planar background registration, not depth reconstruction or a person map.
internal static class SceneRegistration {
 public static SceneMatch Match(byte[] roomBytes,byte[] linkBytes,BodyResult room,BodyResult link,Action<object>? evidence=null,bool siftOnly=false){
  using var a=Cv2.ImDecode(linkBytes,ImreadModes.Grayscale);using var b=Cv2.ImDecode(roomBytes,ImreadModes.Grayscale);
  if(a.Empty()||b.Empty()||room.Error!=null||link.Error!=null||room.People.Length!=0||link.People.Length!=0)throw new IOException("Leave both camera views empty before scanning.");
  using var ma=Mask(a,link);using var mb=Mask(b,room);
  if(!siftOnly){
   using var orb=ORB.Create(1800);
   try{return Align(a,b,ma,mb,orb,NormTypes.Hamming,"ORB",evidence);}
   catch(IOException e){evidence?.Invoke(new{type="matcher-fallback",from="ORB",to="SIFT",reason=e.Message});}
  }
  // More expensive scale/rotation descriptors are confined to scene setup.
  // The same geometric acceptance checks apply to both matchers.
  using var sift=SIFT.Create(1800);
  return Align(a,b,ma,mb,sift,NormTypes.L2,"SIFT",evidence);
 }
 static SceneMatch Align(Mat a,Mat b,Mat ma,Mat mb,Feature2D detector,NormTypes norm,string algorithm,Action<object>? evidence){
  using var da=new Mat();using var db=new Mat();
  detector.DetectAndCompute(a,ma,out var ka,da);detector.DetectAndCompute(b,mb,out var kb,db);
  evidence?.Invoke(new{type="features",algorithm,roomFeatures=kb.Length,linkFeatures=ka.Length,roomUnmasked=Cv2.CountNonZero(mb)/(double)b.Total(),linkUnmasked=Cv2.CountNonZero(ma)/(double)a.Total()});
  if(da.Empty()||db.Empty())throw new IOException("Not enough fixed room detail; improve lighting or camera overlap.");
  using var matcher=new BFMatcher(norm);var pairs=matcher.KnnMatch(da,db,2).Where(p=>p.Length==2&&p[0].Distance<p[1].Distance*.7).Select(p=>p[0]).GroupBy(p=>p.TrainIdx).Select(g=>g.OrderBy(p=>p.Distance).First()).GroupBy(p=>(Math.Round(ka[p.QueryIdx].Pt.X/2),Math.Round(ka[p.QueryIdx].Pt.Y/2))).Select(g=>g.OrderBy(p=>p.Distance).First()).GroupBy(p=>(Math.Round(kb[p.TrainIdx].Pt.X/2),Math.Round(kb[p.TrainIdx].Pt.Y/2))).Select(g=>g.OrderBy(p=>p.Distance).First()).ToArray();
  if(pairs.Length<20)throw new IOException($"Only {pairs.Length} fixed-feature matches; need 20. Improve lighting or camera overlap.");
  var from=pairs.Select(p=>ka[p.QueryIdx].Pt).ToArray();var to=pairs.Select(p=>kb[p.TrainIdx].Pt).ToArray();
  using var source=InputArray.Create(from);using var destination=InputArray.Create(to);using var mask=new Mat();using var h=Cv2.FindHomography(source,destination,HomographyMethods.Ransac,3,mask);
  if(h.Empty())throw new IOException("Room views could not be aligned.");int inliers=Cv2.CountNonZero(mask);double ratio=inliers/(double)pairs.Length;
  if(inliers<16||ratio<.6)throw new IOException($"Room alignment is ambiguous: {inliers}/{pairs.Length} consistent matches ({ratio:P0}); need 16 and 60%.");
  var accepted=Enumerable.Range(0,pairs.Length).Where(i=>mask.At<byte>(i)!=0).Select(i=>to[i]).ToArray();
  if(accepted.Max(p=>p.X)-accepted.Min(p=>p.X)<b.Width*.18||accepted.Max(p=>p.Y)-accepted.Min(p=>p.Y)<b.Height*.15)throw new IOException("Matches are too concentrated; need fixed features across the scene.");
  var center=Cv2.PerspectiveTransform(new[]{new Point2f(a.Width/2f,a.Height/2f)},h)[0];
  if(!float.IsFinite(center.X+center.Y)||center.X<0||center.X>=b.Width||center.Y<0||center.Y>=b.Height)throw new IOException("Link centre lies outside the Acer scene.");
  var acceptedSource=Enumerable.Range(0,pairs.Length).Where(i=>mask.At<byte>(i)!=0).Select(i=>from[i]).ToArray();
  double hullDistance=Cv2.PointPolygonTest(Cv2.ConvexHull(acceptedSource),new Point2f(a.Width/2f,a.Height/2f),true)/Math.Min(a.Width,a.Height);
  evidence?.Invoke(new{type="fit-quality",algorithm,pairs=pairs.Length,inliers,ratio,centerX=center.X/b.Width,centerY=center.Y/b.Height,hullDistance,roomSpreadX=(accepted.Max(p=>p.X)-accepted.Min(p=>p.X))/b.Width,roomSpreadY=(accepted.Max(p=>p.Y)-accepted.Min(p=>p.Y))/b.Height});
  if(hullDistance<0)throw new IOException($"Fixed features do not surround the Link centre ({-hullDistance:P0} outside support); alignment would extrapolate.");
  var neighbours=Cv2.PerspectiveTransform(new[]{new Point2f(a.Width/2f+10,a.Height/2f),new Point2f(a.Width/2f,a.Height/2f+10)},h);var u=neighbours[0]-center;var v=neighbours[1]-center;
  double determinant=u.X*v.Y-u.Y*v.X;if(determinant<2||determinant>2500)throw new IOException("Scene alignment has implausible scale or orientation.");
  return new(center.X/b.Width,center.Y/b.Height,inliers,ratio,accepted.Select(p=>new PointF(p.X/b.Width,p.Y/b.Height)).ToArray(),acceptedSource.Select(p=>new PointF(p.X/a.Width,p.Y/a.Height)).ToArray());
 }
 public static void Stable(byte[] before,byte[] after,BodyResult a,BodyResult b){
  using var x=Cv2.ImDecode(before,ImreadModes.Grayscale);using var y=Cv2.ImDecode(after,ImreadModes.Grayscale);if(x.Size()!=y.Size()||x.Empty())throw new IOException("Scene image dimensions changed.");
  using var mask=Mask(x,a);using var other=Mask(y,b);Cv2.BitwiseAnd(mask,other,mask);if(Cv2.CountNonZero(mask)<x.Total()*.15)throw new IOException("Too little fixed background remains after excluding objects.");
  using var blurX=new Mat();using var blurY=new Mat();Cv2.GaussianBlur(x,blurX,new OpenCvSharp.Size(5,5),0);Cv2.GaussianBlur(y,blurY,new OpenCvSharp.Size(5,5),0);using var diff=new Mat();Cv2.Absdiff(blurX,blurY,diff);Cv2.Threshold(diff,diff,20,255,ThresholdTypes.Binary);Cv2.BitwiseAnd(diff,mask,diff);
  if(Cv2.CountNonZero(diff)/(double)Cv2.CountNonZero(mask)>.04)throw new IOException("Background is still moving or exposure is changing. Wait for a steady room and retry.");
 }
 static Mat Mask(Mat image,BodyResult observations){
  if(observations.Width!=image.Width||observations.Height!=image.Height)throw new IOException("Scene detection dimensions disagree with camera frame.");
  var mask=new Mat(image.Size(),MatType.CV_8UC1,Scalar.White);
  foreach(var chair in (observations.Objects??[]).Concat(observations.Chairs??[])){int x=Math.Clamp((int)chair.X-16,0,image.Width),y=Math.Clamp((int)chair.Y-16,0,image.Height),right=Math.Clamp((int)(chair.X+chair.Width)+16,0,image.Width),bottom=Math.Clamp((int)(chair.Y+chair.Height)+16,0,image.Height);if(right>x&&bottom>y)Cv2.Rectangle(mask,new Rect(x,y,right-x,bottom-y),Scalar.Black,-1);}
  return mask;
 }
}
