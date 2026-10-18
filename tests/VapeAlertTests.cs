using Link360Desktop;
using OpenCvSharp;
internal static class VapeAlertTests {
 static void Check(bool ok,string why){if(!ok)throw new Exception("Vape alert: "+why);}
 public static void Run(){
  using var d=new VapeDetector();using var quiet=new Mat(360,640,MatType.CV_8UC3,new Scalar(30,30,30));var at=DateTime.UtcNow;
  for(int i=0;i<12;i++)Check(!d.Observe(quiet,at.AddMilliseconds(i*200)).Candidate,"stationary scene must hold");
  using var cloud=quiet.Clone();Cv2.Ellipse(cloud,new Point(340,145),new Size(70,35),0,0,360,new Scalar(160,160,160),-1);VapeSignal? signal=null;for(int i=0;i<3;i++)signal=d.Observe(cloud,at.AddMilliseconds(2400+i*200));Check(signal!.Candidate,"sustained pale local change produces experimental cue");
  using var exposure=new Mat(360,640,MatType.CV_8UC3,new Scalar(140,140,140));Check(!d.Observe(exposure,at.AddMilliseconds(3000)).Candidate,"whole-scene exposure change must hold");Check(!d.Observe(cloud,at.AddSeconds(8)).Candidate,"stale gap resets consecutive evidence");
  bool rejected=false;try{d.Learn(quiet,new Rect(10,10,20,30));}catch(IOException){rejected=true;}Check(rejected,"flat reference rejected");
  using var patterned=quiet.Clone();Cv2.Rectangle(patterned,new Rect(150,100,24,46),new Scalar(80,190,240),-1);Cv2.Line(patterned,new Point(153,105),new Point(170,140),new Scalar(230,20,90),3);d.Learn(patterned,new Rect(148,98,28,50));var matched=d.Observe(patterned,at.AddSeconds(8.2));Check(matched.TemplateScore>.95&&matched.VapeBox!=null,"actual textured reference match");
  var seq=new VapeEventPolicy();seq.Reset();Check(!seq.Observe(at,true,true,true,true,.08,.08),"pre-exhale handling cannot fire during baseline");for(int i=0;i<14;i++)Check(!seq.Observe(at.AddSeconds(1+i*.2),true,true,false,false,0,0),"baseline never fires");Check(!seq.Observe(at.AddSeconds(4),true,true,true,true,.08,.08),"near-mouth handling cannot fire");Check(!seq.Observe(at.AddSeconds(4.2),true,true,false,true,.08,.08),"initial removal motion cannot fire");Check(seq.Observe(at.AddSeconds(5),true,true,false,true,.08,.08),"post-removal sustained cue can fire");Check(!seq.Observe(at.AddSeconds(5.2),true,true,false,true,.08,.08),"event requires a fresh interaction sequence");var gate=new VapeAlertGate();Check(gate.Try(at,TimeSpan.FromSeconds(20)),"first event");Check(!gate.Try(at.AddSeconds(19),TimeSpan.FromSeconds(20)),"cooldown suppresses spam");Check(gate.Try(at.AddSeconds(20),TimeSpan.FromSeconds(20)),"cooldown expires");
  Console.WriteLine("PASS: local vape lab stationary hold, plume cue, exposure/stale rejection, reference matching and cooldown");
 }
}
