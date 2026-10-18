using OpenCvSharp;
using System.Text.Json;

// Offline diagnostics only. Reads an explicitly supplied local paired test clip;
// never opens cameras or sends controls. Image motion is not motor encoder data.
internal static class VideoMotionReview {
 internal record Motion(double Dx,double Dy,int Inliers,double Ratio);
 internal record Sample(double Seconds,Motion? Room,Motion? Link);
 internal static Motion? Measure(Mat before,Mat after){
  using var mask=new Mat(before.Size(),MatType.CV_8UC1,Scalar.White);
  // Ignore timestamp/framing overlays and the central subject region. Remaining
  // foreground motion can still contaminate this estimate; inspect the contact sheet.
  Cv2.Rectangle(mask,new Rect(0,0,before.Width,28),Scalar.Black,-1);
  Cv2.Rectangle(mask,new Rect(before.Width/4,28,before.Width/2,before.Height-28),Scalar.Black,-1);
  var points=Cv2.GoodFeaturesToTrack(before,300,.015,7,mask,3,false,.04);
  if(points.Length<20)return null;
  var next=points.ToArray();Cv2.CalcOpticalFlowPyrLK(before,after,points,ref next,out byte[] status,out float[] error);
  var good=Enumerable.Range(0,points.Length).Where(i=>status[i]!=0&&error[i]<25&&next[i].X>=0&&next[i].X<after.Width&&next[i].Y>=0&&next[i].Y<after.Height).ToArray();
  if(good.Length<20)return null;
  using var source=InputArray.Create(good.Select(i=>points[i]).ToArray());using var destination=InputArray.Create(good.Select(i=>next[i]).ToArray());using var inlierMask=new Mat();
  using var fit=Cv2.EstimateAffinePartial2D(source,destination,inlierMask,RobustEstimationAlgorithms.RANSAC,1.5);
  if(fit==null||fit.Empty())return null;int count=Cv2.CountNonZero(inlierMask);double ratio=count/(double)good.Length;
  if(count<16||ratio<.65)return null;
  double x=before.Width*.5,y=before.Height*.5;
  return new(fit.At<double>(0,0)*x+fit.At<double>(0,1)*y+fit.At<double>(0,2)-x,fit.At<double>(1,0)*x+fit.At<double>(1,1)*y+fit.At<double>(1,2)-y,count,ratio);
 }
 public static void Verify(){
  using var texture=SceneTests.Texture();using var before=new Mat();Cv2.CvtColor(texture,before,ColorConversionCodes.BGR2GRAY);using var after=new Mat();using var warp=Mat.Eye(2,3,MatType.CV_64F).ToMat();warp.Set(0,2,3d);warp.Set(1,2,-2d);Cv2.WarpAffine(before,after,warp,before.Size());var measured=Measure(before,after);
  if(measured==null||Math.Abs(measured.Dx-3)>.15||Math.Abs(measured.Dy+2)>.15)throw new Exception("Offline image-motion check failed known translation");
  var still=Measure(before,before);if(still==null||Math.Abs(still.Dx)+Math.Abs(still.Dy)>.02)throw new Exception("Offline image-motion check invented stationary movement");
  using var blank=new Mat(before.Size(),MatType.CV_8UC1,Scalar.Black);if(Measure(blank,blank)!=null)throw new Exception("Offline image-motion check accepted featureless frames");
  Console.WriteLine("PASS: offline image-motion review measures known translation, stillness and missing texture");
 }
 public static void Run(string path,string outputDirectory){
  Cv2.SetNumThreads(1);using var capture=new VideoCapture(path);if(!capture.IsOpened()||capture.FrameWidth!=960||capture.FrameHeight!=360)throw new IOException("Expected a local 960x360 paired test clip");
  double duration=Math.Min(62,capture.FrameCount/capture.Fps);var rows=new List<Sample>();using var frame=new Mat();using var priorRoom=new Mat();using var priorLink=new Mat();
  using var sheet=new Mat(720,960,MatType.CV_8UC3,new Scalar(16,20,26));double[] contactTimes=[1,5,10,15,25,35,40,55];int contact=0;
  for(double time=0;time<duration;time+=.1){
   capture.Set(VideoCaptureProperties.PosMsec,time*1000);if(!capture.Read(frame))break;
   using var roomRoi=new Mat(frame,new Rect(0,0,480,270));using var linkRoi=new Mat(frame,new Rect(480,0,480,270));using var room=new Mat();using var link=new Mat();Cv2.CvtColor(roomRoi,room,ColorConversionCodes.BGR2GRAY);Cv2.CvtColor(linkRoi,link,ColorConversionCodes.BGR2GRAY);
   if(!priorRoom.Empty())rows.Add(new(time,Measure(priorRoom,room),Measure(priorLink,link)));room.CopyTo(priorRoom);link.CopyTo(priorLink);
   if(contact<contactTimes.Length&&time>=contactTimes[contact]){using var tile=new Mat();Cv2.Resize(frame,tile,new Size(480,180));using var slot=new Mat(sheet,new Rect((contact%2)*480,(contact/2)*180,480,180));tile.CopyTo(slot);Cv2.PutText(slot,$"{time:F1}s",new Point(210,18),HersheyFonts.HersheySimplex,.5,Scalar.Yellow,1);contact++;}
  }
  object Summarize(double start,double end,bool room){var valid=rows.Where(s=>s.Seconds>=start&&s.Seconds<end).Select(s=>room?s.Room:s.Link).Where(m=>m!=null).Select(m=>m!).ToArray();int px=0,py=0,rx=0,ry=0;foreach(var m in valid){int x=Math.Abs(m.Dx)<.35?0:Math.Sign(m.Dx),y=Math.Abs(m.Dy)<.35?0:Math.Sign(m.Dy);if(x!=0){if(px!=0&&x!=px)rx++;px=x;}if(y!=0){if(py!=0&&y!=py)ry++;py=y;}}var magnitude=valid.Select(m=>Math.Sqrt(m.Dx*m.Dx+m.Dy*m.Dy)).Order().ToArray();return new{start,end,valid=valid.Length,missing=rows.Count(s=>s.Seconds>=start&&s.Seconds<end)-valid.Length,medianPixelsPer100ms=magnitude.Length==0?0:magnitude[magnitude.Length/2],p95PixelsPer100ms=magnitude.Length==0?0:magnitude[(int)(magnitude.Length*.95)],directionReversals=new{x=rx,y=ry},stepsOver3Pixels=valid.Count(m=>Math.Abs(m.Dx)>3||Math.Abs(m.Dy)>3)};}
  var summary=new{video=Path.GetFileName(path),duration,meaning="Dominant peripheral image motion in 100ms samples, not physical motor speed. Paired frames are not synchronized exposures; duplicate frames and foreground objects can bias results. Confirm still/moving phases with the user and inspect frames.",phases=new[]{(0d,15d),(15d,35d),(35d,40d),(40d,62d)}.Select(p=>new{room=Summarize(p.Item1,p.Item2,true),link=Summarize(p.Item1,p.Item2,false)}).ToArray()};
  Directory.CreateDirectory(outputDirectory);var basename=Path.GetFileNameWithoutExtension(path);var options=new JsonSerializerOptions{WriteIndented=true};File.WriteAllText(Path.Combine(outputDirectory,basename+".motion.json"),JsonSerializer.Serialize(new{summary,samples=rows},options));Cv2.ImWrite(Path.Combine(outputDirectory,basename+".review.jpg"),sheet);Console.WriteLine(JsonSerializer.Serialize(summary,options));
 }
}
