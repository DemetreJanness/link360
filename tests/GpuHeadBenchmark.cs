using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Link360Desktop;
using OpenCvSharp;

internal static class GpuHeadBenchmark {
 public static void Run(string video,string model,bool useGpu=true){
  using var capture=new VideoCapture(video);if(!capture.IsOpened())throw new IOException("Local test video unavailable");using var gpu=new LocalOnnxFaceDetector(model,useGpu);using var pair=new Mat();using var resized=new Mat();var cpuMs=new List<double>();var gpuMs=new List<double>();int samples=0,countMismatch=0,headFrames=0;double maxBoxDifference=0,maxScoreDifference=0;
  for(double t=0;t<Math.Min(60,capture.FrameCount/capture.Fps);t+=.25){capture.Set(VideoCaptureProperties.PosMsec,t*1000);if(!capture.Read(pair))break;using var tile=new Mat(pair,new Rect(480,0,480,270));Cv2.Resize(tile,resized,new Size(320,180));var bytes=resized.ToBytes(".jpg");FaceResult cpu,g;
   // Alternate order to avoid giving one backend a systematic cache advantage.
   var watch=Stopwatch.StartNew();if(samples%2==0){cpu=OpenCvFaceDetector.Detect(bytes);double ms=watch.Elapsed.TotalMilliseconds;watch.Restart();g=gpu.Detect(bytes);if(samples>=8){cpuMs.Add(ms);gpuMs.Add(watch.Elapsed.TotalMilliseconds);}}
   else{g=gpu.Detect(bytes);double ms=watch.Elapsed.TotalMilliseconds;watch.Restart();cpu=OpenCvFaceDetector.Detect(bytes);if(samples>=8){gpuMs.Add(ms);cpuMs.Add(watch.Elapsed.TotalMilliseconds);}}
   samples++;if(cpu.Faces.Length!=g.Faces.Length){countMismatch++;continue;}if(cpu.Faces.Length>0)headFrames++;var a=cpu.Faces.OrderBy(x=>x.X).ToArray();var b=g.Faces.OrderBy(x=>x.X).ToArray();for(int i=0;i<a.Length;i++){maxBoxDifference=Math.Max(maxBoxDifference,new[]{Math.Abs(a[i].X-b[i].X),Math.Abs(a[i].Y-b[i].Y),Math.Abs(a[i].Width-b[i].Width),Math.Abs(a[i].Height-b[i].Height)}.Max());maxScoreDifference=Math.Max(maxScoreDifference,Math.Abs(a[i].Confidence-b[i].Confidence));}
  }
  if(samples<20||countMismatch!=0||maxBoxDifference>.01||maxScoreDifference>.001)throw new Exception("Head backend equivalence check failed");
  object Stats(List<double> x)=>new{mean=x.Average(),p50=x.Order().ElementAt(x.Count/2),p95=x.Order().ElementAt((int)(x.Count*.95))};
  Console.WriteLine(JsonSerializer.Serialize(new{candidateProvider=useGpu?"DirectML":"ORT CPU",samples,headFrames,countMismatch,maxBoxDifference,maxScoreDifference,cpuMs=Stats(cpuMs),candidateMs=Stats(gpuMs),modelSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(model))),meaning="Local paired-recording replay; complete JPEG decode, prepare, inference, decode/NMS timing. First eight frames excluded. No motor commands or saved frames."}));
 }
}
