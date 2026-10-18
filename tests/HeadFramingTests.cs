using Link360Desktop;
using OpenCvSharp;
internal static class HeadFramingTests {
 public static void Run(){
  void Check(bool valid,string message){if(!valid)throw new Exception(message);}
  using(var blank=new Mat(180,320,MatType.CV_8UC3,Scalar.All(0)))Check(LocalFaceDetector.Detect(blank.ToBytes(".jpg")).Faces.Length==0,"Blank image generated a head target");
  using(var wrong=new Mat(60,80,MatType.CV_8UC3,Scalar.All(0))){bool rejected=false;try{LocalFaceDetector.Detect(wrong.ToBytes(".jpg"));}catch(IOException){rejected=true;}Check(rejected,"Wrong-size head image was accepted");}
  var person=new PersonBox(60,20,200,155,160,82,.9);var face=new FaceBox(110,35,50,65,.93);var faces=new FaceResult([face],320,180);
  var aim=HeadFramingAnchor.Select(faces,person);Check(aim is {} a&&a.X==135&&Math.Abs(a.Y-90.25)<.01,"Validated head did not provide consistent framing");
  Check(HeadFramingAnchor.Select(faces with{Faces=[face,face]},person)==null,"Ambiguous heads produced an aim");
  Check(HeadFramingAnchor.Select(faces with{Faces=[face with{Confidence=.7}]},person)==null,"Weak head produced an aim");
  Check(HeadFramingAnchor.Select(faces with{Faces=[face with{X=270}]},person)==null,"Off-body head produced an aim");
  Check(HeadFramingAnchor.Select(faces with{Faces=[face with{Y=110,Height=30}]},person)==null,"Head below shoulders region produced an aim");
  Check(HeadFramingAnchor.Select(faces with{Faces=[face with{X=double.NaN}]},person)==null,"Non-finite head produced an aim");
  Check(LinkLock.HasUsableTorso(new([person with{CenterX=aim!.Value.X,CenterY=aim.Value.Y}],320,180,"fixture",0,AnchorKind:"head")),"Validated head framing failed primary lock");
  var fake=Enumerable.Range(0,39).Select(_=>new Joint(0,0,0,0)).ToArray();fake[11]=new(159,58,.99,.99);fake[12]=new(143,63,.99,.99);fake[23]=new(157,114,.99,.99);fake[24]=new(147,122,.99,.99);
  Check(TorsoAnchor.Select(fake,.9,320,180,new(53,23,214,154,160,85,.9))==null,"Recorded face-as-shoulders geometry escaped the torso fallback guard");
  Console.WriteLine("PASS: geometric head framing, ambiguity/weak/off-body/invalid rejection and recorded face-as-shoulders regression");
 }
 public static void Replay(string path){
  using var capture=new VideoCapture(path);using var detector=new LocalWholeBodyDetector(true);using var pose=new LocalPoseDetector(true);using var pair=new Mat();using var image=new Mat();int frames=0,people=0,heads=0,upper=0;var ms=new List<double>();
  for(double t=0;t<Math.Min(60,capture.FrameCount/capture.Fps);t+=.25){capture.Set(VideoCaptureProperties.PosMsec,t*1000);if(!capture.Read(pair))break;using var tile=new Mat(pair,new Rect(480,0,480,270));Cv2.Resize(tile,image,new Size(320,180));var bytes=image.ToBytes(".jpg");var body=detector.Detect(bytes);frames++;if(body.People.Length!=1)continue;people++;var watch=System.Diagnostics.Stopwatch.StartNew();var head=HeadFramingAnchor.Select(LocalFaceDetector.Detect(bytes),body.People[0]);watch.Stop();if(frames>4)ms.Add(watch.Elapsed.TotalMilliseconds);if(head!=null){heads++;continue;}var fallback=pose.Detect(bytes,body.People[0]);if(fallback.AnchorKind=="upper-body"&&fallback.TorsoX.HasValue)upper++;}
  Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{frames,onePersonFrames=people,headFrames=heads,upperBodyFallbackFrames=upper,noPrimaryAnchor=people-heads-upper,meanHeadDetectionMs=ms.Count==0?0:ms.Average(),meaning="Offline compressed preview replay, not a live motion or anatomical ground-truth test. All processing local; no frames saved."}));
 }
}
