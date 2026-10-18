using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace Link360Desktop;
// Preprocessing/anchor decoding follows OpenCV Zoo MPPersonDet (Apache-2.0).
// Model provenance and its retained license are under models/.
internal record PersonBox(double X,double Y,double Width,double Height,double CenterX,double CenterY,double Confidence,double HipX=0,double HipY=0,double Radius=0,double Rotation=0);
internal record SceneObject(double X,double Y,double Width,double Height,double Confidence,string Kind,int ClassId=-1);
internal record BodyResult(PersonBox[] People,int Width,int Height,string Provider,long InferenceMs,string? Error=null,bool ArmsOut=false,long PoseMs=0,string? PoseReason=null,string AnchorKind="box",SceneObject[]? Chairs=null,SceneObject[]? Objects=null);
internal record GpuAdapter(int Index,string Name,uint Vendor);

internal static class GpuInventory {
 [DllImport("dxgi.dll")] static extern int CreateDXGIFactory1(ref Guid iid,out IntPtr factory);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int EnumAdapter(IntPtr self,uint index,out IntPtr adapter);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetDesc(IntPtr self,out Desc desc);
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct Desc {
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string Description;
  public uint VendorId,DeviceId,SubSysId,Revision;
  public nuint DedicatedVideoMemory,DedicatedSystemMemory,SharedSystemMemory;
  public ulong Luid;public uint Flags;
 }
 static T Method<T>(IntPtr obj,int slot)where T:Delegate=>Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(obj),slot*IntPtr.Size));
 public static GpuAdapter[] List() {
  var iid=new Guid("770aae78-f26f-4dba-a829-253c83d1b387");Marshal.ThrowExceptionForHR(CreateDXGIFactory1(ref iid,out var factory));
  var list=new List<GpuAdapter>();try{for(uint i=0;i<16;i++){int hr=Method<EnumAdapter>(factory,12)(factory,i,out var adapter);if(hr!=0)break;try{Marshal.ThrowExceptionForHR(Method<GetDesc>(adapter,10)(adapter,out var d));if((d.Flags&2)==0)list.Add(new((int)i,d.Description,d.VendorId));}finally{Marshal.Release(adapter);}}}finally{Marshal.Release(factory);}return list.ToArray();
 }
}

// MediaPipe's local person detector supplies body-center virtual keypoints.
// No identity embedding or retained appearance template is generated.
internal interface IBodyDetector : IDisposable {BodyResult Detect(byte[] bytes);}
internal sealed class LocalBodyDetector : IBodyDetector {
 readonly InferenceSession session;
 readonly string input;
 readonly string provider;
 static readonly (double X,double Y)[] anchors=Anchors();
 static (double,double)[] Anchors(){var a=new List<(double,double)>();foreach(var level in new[]{(28,2),(14,2),(7,6)})for(int y=0;y<level.Item1;y++)for(int x=0;x<level.Item1;x++)for(int k=0;k<level.Item2;k++)a.Add(((x+0.5)/level.Item1,(y+0.5)/level.Item1));return a.ToArray();}
 public LocalBodyDetector(bool gpu) {
  // Official Windows ORT builds expose platform telemetry; disable it before
  // creating any inference session. Fail closed if the disable call fails.
  OrtEnv.Instance().DisableTelemetryEvents();
  string root=Path.Combine(AppContext.BaseDirectory,"models"),file=Path.Combine(root,"person_detection_mediapipe_2023mar.onnx");
  using var metadata=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"body-provenance.json")));
  if(!Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).Equals(metadata.RootElement.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase))throw new IOException("Body model checksum mismatch.");
  using var options=new SessionOptions{EnableMemoryPattern=false,ExecutionMode=ExecutionMode.ORT_SEQUENTIAL,IntraOpNumThreads=1,InterOpNumThreads=1,GraphOptimizationLevel=GraphOptimizationLevel.ORT_ENABLE_ALL};
  if(gpu){var adapters=GpuInventory.List();var selected=adapters.FirstOrDefault(a=>a.Vendor==0x10de)??adapters.FirstOrDefault()??throw new IOException("No GPU adapter available.");options.AppendExecutionProvider_DML(selected.Index);provider="DirectML · "+selected.Name;}else provider="CPU";
  session=new InferenceSession(file,options);input=session.InputMetadata.Keys.Single();
  if(!session.InputMetadata[input].Dimensions.SequenceEqual(new[]{1,3,224,224}))throw new IOException("Unexpected body model input contract.");
 }
 public BodyResult Detect(byte[] bytes) {
  using var image=Cv2.ImDecode(bytes,ImreadModes.Color);if(image.Empty()||image.Width>1920||image.Height>1080)throw new IOException("Invalid overview image.");
  double ratio=224.0/Math.Max(image.Width,image.Height);int w=(int)(image.Width*ratio),h=(int)(image.Height*ratio),left=(224-w)/2,top=(224-h)/2;
  using var resized=new Mat();Cv2.Resize(image,resized,new OpenCvSharp.Size(w,h));
  var blob=new DenseTensor<float>(new[]{1,3,224,224});
  for(int y=0;y<h;y++)for(int x=0;x<w;x++){var p=resized.At<Vec3b>(y,x);blob[0,0,y+top,x+left]=p.Item2/127.5f-1;blob[0,1,y+top,x+left]=p.Item1/127.5f-1;blob[0,2,y+top,x+left]=p.Item0/127.5f-1;}
  var clock=System.Diagnostics.Stopwatch.StartNew();using var output=session.Run(new[]{NamedOnnxValue.CreateFromTensor(input,blob)});clock.Stop();
  var tensors=output.Select(o=>o.AsTensor<float>()).ToArray();
  var boxes=tensors.Single(t=>t.Dimensions.Length==3&&t.Dimensions[2]==12);var scores=tensors.Single(t=>t.Dimensions.Length==3&&t.Dimensions[2]==1);
  if(boxes.Dimensions[1]!=anchors.Length||scores.Dimensions[1]!=anchors.Length)throw new IOException("Unexpected body anchor contract.");
  var candidates=new List<PersonBox>();double scale=Math.Max(image.Width,image.Height),padX=left/ratio,padY=top/ratio;
  for(int i=0;i<anchors.Length;i++) {
   double score=1/(1+Math.Exp(-Math.Clamp(scores[0,i,0],-100,100)));if(score<0.65)continue;
   double Kx(int n)=>(boxes[0,i,4+n*2]/224+anchors[i].X)*scale-padX;
   double Ky(int n)=>(boxes[0,i,5+n*2]/224+anchors[i].Y)*scale-padY;
   double hipX=Kx(0),hipY=Ky(0),radius=Math.Sqrt(Math.Pow(Kx(1)-hipX,2)+Math.Pow(Ky(1)-hipY,2));
   double cx=(hipX+Kx(2))/2,cy=(hipY+Ky(2))/2;
   double x=Math.Clamp(hipX-radius,0,image.Width),y=Math.Clamp(hipY-radius,0,image.Height),right=Math.Clamp(hipX+radius,0,image.Width),bottom=Math.Clamp(hipY+radius,0,image.Height);
   if(!double.IsFinite(cx+cy+radius)||radius<8||cx<0||cx>=image.Width||cy<0||cy>=image.Height||right<=x||bottom<=y)continue;
   double radians=Math.PI/2-Math.Atan2(-(Ky(1)-hipY),Kx(1)-hipX);radians-=2*Math.PI*Math.Floor((radians+Math.PI)/(2*Math.PI));
   candidates.Add(new(x,y,right-x,bottom-y,cx,cy,score,hipX,hipY,radius,radians*180/Math.PI));
  }
  var kept=new List<PersonBox>();foreach(var p in candidates.OrderByDescending(p=>p.Confidence))if(!kept.Any(q=>Overlap(p,q)>0.3))kept.Add(p);
  return new(kept.ToArray(),image.Width,image.Height,provider,clock.ElapsedMilliseconds);
 }
 static double Overlap(PersonBox p,PersonBox q){double area=Math.Max(0,Math.Min(p.X+p.Width,q.X+q.Width)-Math.Max(p.X,q.X))*Math.Max(0,Math.Min(p.Y+p.Height,q.Y+q.Height)-Math.Max(p.Y,q.Y));return area/(p.Width*p.Height+q.Width*q.Height-area);}
 public void Dispose()=>session.Dispose();
}
