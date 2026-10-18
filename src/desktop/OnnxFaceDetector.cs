using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
namespace Link360Desktop;

// Decoder adapted from OpenCV 4.13 FaceDetectorYN (Apache-2.0).
// See docs/licenses/OpenCV-LICENSE.txt and models/YuNet-LICENSE.txt.
// The GPU option is for the offline benchmark; CPU is faster for this small
// model under the measured two-camera load. Each serial worker owns a session.
internal sealed class LocalOnnxFaceDetector : IDisposable {
 readonly InferenceSession session;
 readonly float[] values=new float[3*320*192];
 readonly byte[] pixels=new byte[320*180*3];
 readonly DenseTensor<float> tensor;
 public LocalOnnxFaceDetector(string? model=null,bool useGpu=false){
  model??=Path.Combine(AppContext.BaseDirectory,"models","face_detection_yunet_link360_320x192.onnx");
  if(!Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(model))).Equals("7C588045DC2C5FADFCA1E52A97097F1BDF188C9C5D2370746C089FB0907F4F2B",StringComparison.Ordinal))throw new IOException("Head model checksum mismatch.");
  OrtEnv.Instance().DisableTelemetryEvents();
  using var options=new SessionOptions{EnableMemoryPattern=false,ExecutionMode=ExecutionMode.ORT_SEQUENTIAL,IntraOpNumThreads=1,InterOpNumThreads=1,GraphOptimizationLevel=GraphOptimizationLevel.ORT_ENABLE_ALL};
  if(useGpu)options.AppendExecutionProvider_DML(GpuInventory.List().First(a=>a.Vendor==0x10de).Index);
  session=new(model,options);
  if(!session.InputMetadata["input"].Dimensions.SequenceEqual(new[]{1,3,192,320}))throw new IOException("Unexpected head model shape.");
  tensor=new(values,new[]{1,3,192,320});
 }
 public FaceResult Detect(byte[] bytes){
  using var image=Cv2.ImDecode(bytes,ImreadModes.Color);
  if(image.Empty()||image.Width!=320||image.Height!=180)throw new IOException("Expected a 320x180 tracking image.");
  Marshal.Copy(image.Data,pixels,0,pixels.Length);
  // BGR, planar float32, no normalization. Unwritten lower 12 rows remain
  // zero padding, matching FaceDetectorYN's divisor-32 input contract.
  for(int i=0;i<320*180;i++){values[i]=pixels[i*3];values[320*192+i]=pixels[i*3+1];values[2*320*192+i]=pixels[i*3+2];}
  using var output=session.Run(new[]{NamedOnnxValue.CreateFromTensor("input",tensor)});
  var arrays=output.ToDictionary(x=>x.Name,x=>x.AsTensor<float>().ToArray());var candidates=new List<FaceBox>();
  foreach(int stride in new[]{8,16,32}){
   int cols=320/stride,rows=192/stride;var cls=arrays[$"cls_{stride}"];var obj=arrays[$"obj_{stride}"];var box=arrays[$"bbox_{stride}"];
   if(cls.Length!=rows*cols||obj.Length!=cls.Length||box.Length!=cls.Length*4)throw new IOException("Unexpected head model outputs.");
   for(int i=0;i<cls.Length;i++){
    double score=Math.Sqrt(Math.Clamp(cls[i],0,1)*Math.Clamp(obj[i],0,1));if(!double.IsFinite(score)||score<.85)continue;
    double cx=(i%cols+box[i*4])*stride,cy=(i/cols+box[i*4+1])*stride,w=Math.Exp(box[i*4+2])*stride,h=Math.Exp(box[i*4+3])*stride;
    if(!double.IsFinite(cx+cy+w+h)||w<=0||h<=0)continue;
    candidates.Add(new(cx-w/2,cy-h/2,w,h,score));
   }
  }
  var kept=new List<FaceBox>();
  foreach(var face in candidates.OrderByDescending(x=>x.Confidence).Take(100))if(!kept.Any(x=>Overlap(x,face)>.3))kept.Add(face);
  return new(kept.ToArray(),320,180);
 }
 static double Overlap(FaceBox a,FaceBox b){
  int ax=(int)a.X,ay=(int)a.Y,aw=(int)a.Width,ah=(int)a.Height,bx=(int)b.X,by=(int)b.Y,bw=(int)b.Width,bh=(int)b.Height;
  double area=Math.Max(0,Math.Min(ax+aw,bx+bw)-Math.Max(ax,bx))*Math.Max(0,Math.Min(ay+ah,by+bh)-Math.Max(ay,by));
  return area/Math.Max(1,aw*ah+bw*bh-area);
 }
 public void Dispose()=>session.Dispose();
}
