using OpenCvSharp;
using Link360Desktop;
internal static class RoomQualityReplay {
 public static void Run(string file){
  using var capture=new VideoCapture(file);if(!capture.IsOpened())throw new IOException("Cannot open room test recording");using var detector=new LocalWholeBodyDetector(true);using var frame=new Mat();
  foreach(var mode in Enum.GetValues<RoomImagePreparation>()){
   int samples=0,single=0,missing=0,multiple=0,chairFrames=0;var times=new List<double>();var confidence=new List<double>();
   for(double t=0;t<Math.Min(30,capture.FrameCount/capture.Fps);t+=.5){capture.Set(VideoCaptureProperties.PosMsec,t*1000);if(!capture.Read(frame))break;var bytes=frame.ToBytes(".jpg");var clock=System.Diagnostics.Stopwatch.StartNew();var result=detector.Detect(bytes,mode);clock.Stop();if(samples>4)times.Add(clock.Elapsed.TotalMilliseconds);samples++;if(result.People.Length==1){single++;confidence.Add(result.People[0].Confidence);}else if(result.People.Length==0)missing++;else multiple++;if(result.Chairs?.Length>0)chairFrames++;}
   Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{mode=mode.ToString(),samples,single,missing,multiple,chairFrames,meanConfidence=confidence.Count==0?0:confidence.Average(),meanPipelineMs=times.Average(),p95PipelineMs=times.Order().ElementAt((int)(times.Count*.95)),limitation="Same recorded frames, no labelled ground truth; overlays/compression in source may affect results. Not a live lock-time test."}));
  }
 }
}
