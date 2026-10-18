using OpenCvSharp;
namespace Link360Desktop;
internal record VapeSignal(double PlumeFraction,double ChangedFraction,double TemplateScore,Rect? VapeBox,bool Candidate,string Reason);
// Visual cue only: this does not identify chemicals or prove someone is vaping.
internal sealed class VapeDetector:IDisposable {
 Mat? background,reference;DateTime last=DateTime.MinValue;int consecutive;
 public void Reset(){background?.Dispose();background=null;consecutive=0;last=DateTime.MinValue;}
 public void Learn(Mat image,Rect area){if(area.Width<8||area.Height<8||area.X<0||area.Y<0||area.Right>image.Width||area.Bottom>image.Height)throw new IOException("Select a visible vape in the preview first.");using var gray=new Mat();Cv2.CvtColor(image,gray,ColorConversionCodes.BGR2GRAY);using var crop=new Mat(gray,area);Cv2.MeanStdDev(crop,out _,out var sd);if(sd.Val0<8)throw new IOException("Reference has too little detail; select the vape more tightly.");reference?.Dispose();reference=crop.Clone();}
 public VapeSignal Observe(Mat image,DateTime now,double threshold=.018,Rect? face=null){
  using var small=new Mat();Cv2.Resize(image,small,new OpenCvSharp.Size(320,180));using var gray=new Mat();Cv2.CvtColor(small,gray,ColorConversionCodes.BGR2GRAY);Cv2.GaussianBlur(gray,gray,new OpenCvSharp.Size(5,5),0);
  double score=0;Rect? box=null;
  if(reference!=null&&reference.Width<=image.Width&&reference.Height<=image.Height){using var raw=new Mat();Cv2.CvtColor(image,raw,ColorConversionCodes.BGR2GRAY);using var result=new Mat();Cv2.MatchTemplate(raw,reference,result,TemplateMatchModes.CCoeffNormed);Cv2.MinMaxLoc(result,out _,out score,out _,out var p);if(double.IsFinite(score)&&score>=.82)box=new Rect(p.X,p.Y,reference.Width,reference.Height);else if(!double.IsFinite(score))score=0;}
  if(background==null||now-last>TimeSpan.FromSeconds(1)||now<=last){Reset();background=gray.Clone();last=now;return new(0,0,score,box,false,"Learning quiet background");}last=now;
  using var diff=new Mat();using var bright=new Mat();using var changed=new Mat();using var hsv=new Mat();using var pale=new Mat();using var plume=new Mat();Cv2.Absdiff(gray,background,diff);Cv2.Threshold(diff,changed,20,255,ThresholdTypes.Binary);Cv2.Subtract(gray,background,bright);Cv2.Threshold(bright,bright,14,255,ThresholdTypes.Binary);Cv2.CvtColor(small,hsv,ColorConversionCodes.BGR2HSV);Cv2.InRange(hsv,new Scalar(0,0,65),new Scalar(179,75,245),pale);Cv2.BitwiseAnd(bright,pale,plume);
  double motion=Cv2.CountNonZero(changed)/57600.0; // Reject camera motion / whole-scene exposure changes.
  Rect region=new(32,9,256,126);if(face is {} f){int left=Math.Clamp(f.X-f.Width*2,0,319),right=Math.Clamp(f.Right+f.Width*2,1,320),top=Math.Clamp(f.Y,0,179),bottom=Math.Clamp(f.Y+f.Height,1,180);region=new(left,top,Math.Max(1,right-left),Math.Max(1,bottom-top));var clipped=f.Intersect(new Rect(0,0,320,180));if(clipped.Width>0&&clipped.Height>0)Cv2.Rectangle(plume,clipped,Scalar.Black,-1);}using var upper=new Mat(plume,region);double fraction=Cv2.CountNonZero(upper)/(double)(region.Width*region.Height);
  bool quietScene=motion<.32&&Math.Abs(Cv2.Mean(gray).Val0-Cv2.Mean(background).Val0)<18;
  consecutive=quietScene&&fraction>=threshold?consecutive+1:0;
  // Slow background update preserves a short-lived plume; movement reset avoids a stale reference.
  using var mixed=new Mat();Cv2.AddWeighted(background,.94,gray,.06,0,mixed);mixed.CopyTo(background);
  return new(fraction,motion,score,box,consecutive>=3,quietScene?(consecutive>=3?"Possible plume · visual cue only":"Watching for a sustained pale plume"):"Scene motion / exposure change · holding");
 }
 public void Dispose(){background?.Dispose();reference?.Dispose();}
}
internal sealed class VapeAlertGate {
 DateTime next=DateTime.MinValue;
 public bool Try(DateTime now,TimeSpan cooldown){if(now<next)return false;next=now+cooldown;return true;}
}

// A conservative interaction sequence, not a trained vape classifier.
internal sealed class VapeEventPolicy {
 DateTime? quietAt,awayAt;DateTime mouthAt=DateTime.MinValue;bool ready;
 public string Status{get;private set;}="Automatic alerts off";
 public void Reset(){quietAt=awayAt=null;mouthAt=DateTime.MinValue;ready=false;Status="Waiting for quiet baseline";}
 public bool Observe(DateTime now,bool armed,bool face,bool nearMouth,bool plume,double fraction,double motion){
  if(!armed||!face){Reset();Status=armed?"Need one visible face":"Automatic alerts off";return false;}
  if(!ready){if(fraction<.01&&motion<.03){quietAt??=now;if(now-quietAt>=TimeSpan.FromSeconds(2))ready=true;}else quietAt=null;Status="Learning quiet baseline · hold still";return false;}
  if(nearMouth){mouthAt=now;awayAt=null;Status="Vape near mouth · waiting for it to move away";return false;}
  if(now-mouthAt>TimeSpan.FromSeconds(8)){Status="Ready · waiting for vape near mouth";return false;}
  awayAt??=now;if(now-awayAt<TimeSpan.FromMilliseconds(600)){Status="Handling motion · waiting";return false;}
  Status="Watching for plume after vape";if(!plume||motion>=.2)return false;Reset();Status="Possible vape event";return true;
 }
}
