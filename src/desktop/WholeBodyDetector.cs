using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
namespace Link360Desktop;

// Person/chair YOLOX decoding follows OpenCV Zoo (Apache-2.0). Chairs are
// scene context only, never tracking targets. No appearance identity is generated.
internal sealed class LocalWholeBodyDetector : IBodyDetector {
 readonly InferenceSession session;readonly string input,provider;
 // Each detector belongs to one serial worker; reuse its large input buffers.
 readonly float[] values=new float[3*640*640];readonly DenseTensor<float> blob;
 byte[] pixels=Array.Empty<byte>();int previousWidth,previousHeight;
 static readonly (int X,int Y,int Stride)[] anchors=Anchors();
 static (int,int,int)[] Anchors(){var a=new List<(int,int,int)>();foreach(int stride in new[]{8,16,32})for(int y=0;y<640/stride;y++)for(int x=0;x<640/stride;x++)a.Add((x,y,stride));return a.ToArray();}
 public LocalWholeBodyDetector(bool gpu) {
  blob=new DenseTensor<float>(values,new[]{1,3,640,640});
  OrtEnv.Instance().DisableTelemetryEvents();string root=Path.Combine(AppContext.BaseDirectory,"models"),file=Path.Combine(root,"object_detection_yolox_2022nov.onnx");
  using var metadata=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"whole-body-provenance.json")));
  if(!Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).Equals(metadata.RootElement.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase))throw new IOException("Whole-body model checksum mismatch.");
  using var options=new SessionOptions{EnableMemoryPattern=false,ExecutionMode=ExecutionMode.ORT_SEQUENTIAL,IntraOpNumThreads=1,InterOpNumThreads=1,GraphOptimizationLevel=GraphOptimizationLevel.ORT_ENABLE_ALL};
  if(gpu){var a=GpuInventory.List();var selected=a.FirstOrDefault(a=>a.Vendor==0x10de)??a.First();options.AppendExecutionProvider_DML(selected.Index);provider="DirectML · "+selected.Name;}else provider="CPU";
  session=new InferenceSession(file,options);input=session.InputMetadata.Keys.Single();if(!session.InputMetadata[input].Dimensions.SequenceEqual(new[]{1,3,640,640}))throw new IOException("Unexpected whole-body input contract.");
 }
 public BodyResult Detect(byte[] bytes)=>Detect(bytes,RoomImagePreparation.Raw);
 internal BodyResult Detect(byte[] bytes,RoomImagePreparation preparation) {
  using var image=Cv2.ImDecode(bytes,ImreadModes.Color);if(image.Empty()||image.Width>1920||image.Height>1080)throw new IOException("Invalid room image.");
  RoomImageEnhancement.Apply(image,preparation);
  double ratio=640.0/Math.Max(image.Width,image.Height);int w=(int)(image.Width*ratio),h=(int)(image.Height*ratio);using var resized=new Mat();Cv2.Resize(image,resized,new OpenCvSharp.Size(w,h));
  if(pixels.Length!=w*h*3)pixels=new byte[w*h*3];Marshal.Copy(resized.Data,pixels,0,pixels.Length);
  if(w!=previousWidth||h!=previousHeight){Array.Fill(values,114f);previousWidth=w;previousHeight=h;}
  for(int y=0;y<h;y++)for(int x=0;x<w;x++){int src=(y*w+x)*3,dst=y*640+x;values[dst]=pixels[src];values[640*640+dst]=pixels[src+1];values[2*640*640+dst]=pixels[src+2];}
  var clock=System.Diagnostics.Stopwatch.StartNew();using var output=session.Run(new[]{NamedOnnxValue.CreateFromTensor(input,blob)});clock.Stop();var tensor=output.Single().AsTensor<float>();
  if(!tensor.Dimensions.SequenceEqual(new[]{1,8400,85}))throw new IOException("Unexpected whole-body output contract.");var raw=tensor is DenseTensor<float> dense?dense.Buffer.Span:tensor.ToArray().AsSpan();var boxes=new List<PersonBox>();var chairs=new List<PersonBox>();
  var objects=new Dictionary<int,List<PersonBox>>();
  for(int i=0;i<anchors.Length;i++) {
   int offset=i*85;if(raw[offset+4]<Math.Min(.4,TrackingConfidence.Minimum))continue;int cls=0;for(int k=1;k<80;k++)if(raw[offset+5+k]>raw[offset+5+cls])cls=k;
   double score=raw[offset+4]*raw[offset+5+cls];if(score<(cls==0?TrackingConfidence.Minimum:.5)||!double.IsFinite(score))continue;
   var a=anchors[i];double cx=(raw[offset]+a.X)*a.Stride/ratio,cy=(raw[offset+1]+a.Y)*a.Stride/ratio;
   double bw=Math.Exp(Math.Clamp(raw[offset+2],-10,10))*a.Stride/ratio,bh=Math.Exp(Math.Clamp(raw[offset+3],-10,10))*a.Stride/ratio;
   double left=Math.Clamp(cx-bw/2,0,image.Width),top=Math.Clamp(cy-bh/2,0,image.Height),right=Math.Clamp(cx+bw/2,0,image.Width),bottom=Math.Clamp(cy+bh/2,0,image.Height);
   if(!double.IsFinite(left+top+right+bottom)||right-left<(cls==0?16:8)||bottom-top<(cls==0?24:8))continue;
   double hipX=(left+right)/2,hipY=top+0.58*(bottom-top),radius=Math.Max((right-left)/2,Math.Max(hipY-top,bottom-hipY))*1.15;
   var detected=new PersonBox(left,top,right-left,bottom-top,hipX,top+0.4*(bottom-top),score,hipX,hipY,radius,0);
   if(cls==0)boxes.Add(detected);else{if(!objects.TryGetValue(cls,out var list))objects[cls]=list=[];list.Add(detected);if(cls==56)chairs.Add(detected);}
  }
  var kept=new List<PersonBox>();foreach(var p in boxes.OrderByDescending(p=>p.Confidence))if(!kept.Any(q=>Overlap(p,q)>0.45))kept.Add(p);
  var keptChairs=new List<PersonBox>();foreach(var p in chairs.OrderByDescending(p=>p.Confidence))if(!keptChairs.Any(q=>Overlap(p,q)>.45))keptChairs.Add(p);
  var inventory=new List<SceneObject>();foreach(var entry in objects){var selected=new List<PersonBox>();foreach(var p in entry.Value.OrderByDescending(p=>p.Confidence))if(!selected.Any(q=>Overlap(p,q)>.45))selected.Add(p);string label=ObjectLabels.Name(entry.Key);inventory.AddRange(selected.Select(p=>new SceneObject(p.X,p.Y,p.Width,p.Height,p.Confidence,label,entry.Key)));}
  return new(kept.ToArray(),image.Width,image.Height,provider,clock.ElapsedMilliseconds,Chairs:keptChairs.Select(p=>new SceneObject(p.X,p.Y,p.Width,p.Height,p.Confidence,"chair",56)).ToArray(),Objects:inventory.ToArray());
 }
 static double Overlap(PersonBox p,PersonBox q){double area=Math.Max(0,Math.Min(p.X+p.Width,q.X+q.Width)-Math.Max(p.X,q.X))*Math.Max(0,Math.Min(p.Y+p.Height,q.Y+q.Height)-Math.Max(p.Y,q.Y));return area/(p.Width*p.Height+q.Width*q.Height-area);}
 public void Dispose()=>session.Dispose();
}
