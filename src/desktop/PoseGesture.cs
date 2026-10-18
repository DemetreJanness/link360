using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
namespace Link360Desktop;
internal record Joint(double X,double Y,double Visibility,double Presence);
internal record PoseObservation(bool Arms,long Ms,string Reason,double? TorsoX=null,double? TorsoY=null,string AnchorKind="pose");
internal static class HeadShoulderAnchor {
 public static (double X,double Y)? Select(Joint[] joints,double confidence,int width,int height,PersonBox body){
  if(joints.Length<13||!double.IsFinite(confidence)||confidence<.5||confidence>1||body.Width<=0||body.Height<=0)return null;
  bool Valid(int i)=>joints[i].Visibility>=.55&&joints[i].Presence>=.55&&double.IsFinite(joints[i].X+joints[i].Y+joints[i].Visibility+joints[i].Presence)&&joints[i].X>=0&&joints[i].X<width&&joints[i].Y>=0&&joints[i].Y<height;
  if(!Valid(0)||!Valid(11)||!Valid(12))return null;
  var head=joints[0];var left=joints[11];var right=joints[12];
  double sx=(left.X+right.X)/2,sy=(left.Y+right.Y)/2,span=Math.Sqrt(Math.Pow(left.X-right.X,2)+Math.Pow(left.Y-right.Y,2));
  // Reject face landmarks mislabelled as shoulders; hips are deliberately optional.
  if(span<Math.Max(12,body.Width*.25)||sy<body.Y+body.Height*.18||sy>body.Y+body.Height*.8||head.Y>=sy-span*.15||sy-head.Y>span*1.8||Math.Abs(head.X-sx)>span*.9)return null;
  bool Inside(Joint p)=>p.X>=body.X&&p.X<=body.X+body.Width&&p.Y>=body.Y&&p.Y<=body.Y+body.Height;
  if(!Inside(head)||!Inside(left)||!Inside(right))return null;
  return (head.X*.35+sx*.65,head.Y*.35+sy*.65);
 }
}
internal static class TorsoAnchor {
 public static (double X,double Y)? Select(Joint[] points,double confidence,int width,int height,PersonBox? body=null){
  if(points.Length<25||confidence<.5||confidence>1||!double.IsFinite(confidence))return null;
  bool Valid(int i)=>points[i].Visibility>=.55&&points[i].Presence>=.55&&double.IsFinite(points[i].X+points[i].Y+points[i].Visibility+points[i].Presence)&&points[i].X>=0&&points[i].X<width&&points[i].Y>=0&&points[i].Y<height;
  if(!Valid(11)||!Valid(12))return null;var left=points[11];var right=points[12];double dx=right.X-left.X,dy=right.Y-left.Y,span=Math.Sqrt(dx*dx+dy*dy);if(span<8)return null;
  double x=(left.X+right.X)/2,y=(left.Y+right.Y)/2;
  if(body!=null&&(span<Math.Max(12,body.Width*.25)||y<body.Y+body.Height*.18||y>body.Y+body.Height*.8||left.X<body.X||left.X>body.X+body.Width||right.X<body.X||right.X>body.X+body.Width))return null;
  if(Valid(23)&&Valid(24)){double hipX=(points[23].X+points[24].X)/2,hipY=(points[23].Y+points[24].Y)/2;if(hipY<=y)return null;if(body!=null&&hipY<body.Y+body.Height*.5)return null;x+=.4*(hipX-x);y+=.4*(hipY-y);}
  else {x-=.5*dy*Math.Sign(dx);y+=.5*Math.Abs(dx);}
  return x>=0&&x<width&&y>=0&&y<height?(x,y):null;
 }
}
internal static class ArmsGesture {
 public static bool IsOut(Joint[] points,double confidence) {
  if(points.Length<17||confidence<0.5||!double.IsFinite(confidence))return false;
  foreach(int i in new[]{11,12,13,14,15,16})if(points[i].Visibility<0.55||points[i].Presence<0.55||!double.IsFinite(points[i].X+points[i].Y+points[i].Visibility+points[i].Presence))return false;
  var l=points[11];var r=points[12];double dx=r.X-l.X,dy=r.Y-l.Y,width=Math.Sqrt(dx*dx+dy*dy);if(width<8)return false;
  bool Extended(Joint shoulder,Joint elbow,Joint wrist,int direction) {
   double wx=wrist.X-shoulder.X,wy=wrist.Y-shoulder.Y;
   double reach=(wx*dx+wy*dy)/width*direction;
   double off=Math.Abs(wx*dy-wy*dx)/width,elbowOff=Math.Abs((elbow.X-shoulder.X)*dy-(elbow.Y-shoulder.Y)*dx)/width;
   double Dist(Joint a,Joint b)=>Math.Sqrt(Math.Pow(a.X-b.X,2)+Math.Pow(a.Y-b.Y,2));
   return reach>0.8*width&&off<0.4*width&&elbowOff<0.4*width&&Dist(shoulder,wrist)>0.9*(Dist(shoulder,elbow)+Dist(elbow,wrist));
  }
  return Extended(l,points[13],points[15],-1)&&Extended(r,points[14],points[16],1);
 }
 public static string Reason(Joint[] points,double confidence) {
  if(points.Length<17||confidence<0.5||!double.IsFinite(confidence))return "Pose confidence low · stand fully in view";
  foreach(var pair in new[]{(11,"shoulders"),(12,"shoulders"),(13,"elbows"),(14,"elbows"),(15,"wrists"),(16,"wrists")})if(points[pair.Item1].Visibility<0.55||points[pair.Item1].Presence<0.55)return $"Need both {pair.Item2} clearly in view";
  return IsOut(points,confidence)?"Arms-out recognized":"Raise and straighten both arms at shoulder height";
 }
}
internal sealed class GestureHold {
 DateTime? held,down,lastPositive;bool armed=true;double x,y;
 public double Progress(DateTime now)=>held==null?0:Math.Clamp((now-held.Value).TotalSeconds/2,0,1);
 public bool Tick(bool arms,double cx,double cy,DateTime now) {
  if(!arms){if(armed&&held!=null&&lastPositive!=null&&now-lastPositive.Value<TimeSpan.FromMilliseconds(350))return false;held=null;down??=now;if(now-down.Value>=TimeSpan.FromMilliseconds(800))armed=true;return false;}
  lastPositive=now;
  down=null;if(!armed)return false;
  if(held==null||Math.Abs(cx-x)>0.035||Math.Abs(cy-y)>0.035){held=now;x=cx;y=cy;return false;}
  if(now-held.Value<TimeSpan.FromSeconds(2))return false;
  armed=false;held=null;return true;
 }
}
// ROI normalization follows OpenCV Zoo MPPose (Apache-2.0); retained model
// license/provenance is under models/. Only gesture/torso coordinates leave
// this worker; joints remain in memory and are not stored as identities.
internal sealed class LocalPoseDetector : IDisposable {
 readonly InferenceSession session;readonly string input;readonly string[] outputs;
 readonly byte[] pixels=new byte[256*256*3];readonly float[] values=new float[256*256*3];readonly DenseTensor<float> blob;
 public LocalPoseDetector(bool gpu) {
  blob=new DenseTensor<float>(values,new[]{1,256,256,3});
  OrtEnv.Instance().DisableTelemetryEvents();string root=Path.Combine(AppContext.BaseDirectory,"models"),file=Path.Combine(root,"pose_estimation_mediapipe_2023mar.onnx");
  using var metadata=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"pose-provenance.json")));
  if(!Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).Equals(metadata.RootElement.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase))throw new IOException("Pose model checksum mismatch.");
  using var options=new SessionOptions{EnableMemoryPattern=false,ExecutionMode=ExecutionMode.ORT_SEQUENTIAL,IntraOpNumThreads=1,InterOpNumThreads=1};
  if(gpu){var a=GpuInventory.List();options.AppendExecutionProvider_DML((a.FirstOrDefault(a=>a.Vendor==0x10de)??a.First()).Index);}
  session=new InferenceSession(file,options);input=session.InputMetadata.Keys.Single();if(!session.InputMetadata[input].Dimensions.SequenceEqual(new[]{1,256,256,3}))throw new IOException("Unexpected pose input contract.");
  outputs=session.OutputMetadata.Where(o=>o.Value.Dimensions.Aggregate(1,(a,b)=>a*b) is 195 or 1).Select(o=>o.Key).ToArray();if(outputs.Length!=2)throw new IOException("Unexpected pose output contract.");
 }
 public PoseObservation Detect(byte[] bytes,PersonBox person,Action<Joint[],double>? inspect=null) {
  if(person.Radius<8||!double.IsFinite(person.HipX+person.HipY+person.Radius+person.Rotation))return new(false,0,"Body crop unavailable");
  using var image=Cv2.ImDecode(bytes,ImreadModes.Color);using var transform=Cv2.GetRotationMatrix2D(new Point2f((float)person.HipX,(float)person.HipY),person.Rotation,256/(2*person.Radius));
  transform.Set(0,2,transform.At<double>(0,2)+128-person.HipX);transform.Set(1,2,transform.At<double>(1,2)+128-person.HipY);
  using var crop=new Mat();Cv2.WarpAffine(image,crop,transform,new OpenCvSharp.Size(256,256));
  System.Runtime.InteropServices.Marshal.Copy(crop.Data,pixels,0,pixels.Length);
  for(int p=0;p<pixels.Length;p+=3){values[p]=pixels[p+2]/255f;values[p+1]=pixels[p+1]/255f;values[p+2]=pixels[p]/255f;}
  var clock=System.Diagnostics.Stopwatch.StartNew();using var output=session.Run(new[]{NamedOnnxValue.CreateFromTensor(input,blob)},outputs);clock.Stop();var tensors=output.Select(o=>o.AsTensor<float>()).ToArray();
  var raw=tensors.Single(t=>t.Length==195).ToArray();double conf=tensors.Single(t=>t.Length==1).ToArray()[0];
  double Sig(double v)=>1/(1+Math.Exp(-Math.Clamp(v,-100,100)));var joints=new Joint[39];for(int i=0;i<39;i++)joints[i]=new(raw[i*5],raw[i*5+1],Sig(raw[i*5+3]),Sig(raw[i*5+4]));
  using var inverse=new Mat();Cv2.InvertAffineTransform(transform,inverse);var imageJoints=joints.Select(p=>p with{X=inverse.At<double>(0,0)*p.X+inverse.At<double>(0,1)*p.Y+inverse.At<double>(0,2),Y=inverse.At<double>(1,0)*p.X+inverse.At<double>(1,1)*p.Y+inverse.At<double>(1,2)}).ToArray();
  var upper=HeadShoulderAnchor.Select(imageJoints,conf,image.Width,image.Height,person);
  var torso=upper??TorsoAnchor.Select(imageJoints,conf,image.Width,image.Height,person);
  inspect?.Invoke(imageJoints,conf);
  return new(ArmsGesture.IsOut(joints,conf),clock.ElapsedMilliseconds,ArmsGesture.Reason(joints,conf),torso?.X,torso?.Y,upper.HasValue?"upper-body":"pose");
 }
 public void Dispose()=>session.Dispose();
}
