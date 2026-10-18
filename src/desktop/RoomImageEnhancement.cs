using OpenCvSharp;
namespace Link360Desktop;
// Experimental replay options only. Runtime remains raw until measured against
// representative raw room video; brightness is not evidence of better detection.
internal enum RoomImagePreparation{Raw,ShadowLift,LocalContrast}
internal static class RoomImageEnhancement {
 public static void Apply(Mat image,RoomImagePreparation preparation){
  if(preparation==RoomImagePreparation.Raw)return;
  if(preparation==RoomImagePreparation.ShadowLift){using var lookup=new Mat(1,256,MatType.CV_8UC1);for(int i=0;i<256;i++)lookup.Set(0,i,(byte)Math.Round(255*Math.Pow(i/255.0,.8)));Cv2.LUT(image,lookup,image);return;}
  if(preparation!=RoomImagePreparation.LocalContrast)throw new ArgumentOutOfRangeException(nameof(preparation));
  using var lab=new Mat();Cv2.CvtColor(image,lab,ColorConversionCodes.BGR2Lab);var channels=Cv2.Split(lab);try{using var clahe=Cv2.CreateCLAHE(2,new OpenCvSharp.Size(8,8));clahe.Apply(channels[0],channels[0]);Cv2.Merge(channels,lab);Cv2.CvtColor(lab,image,ColorConversionCodes.Lab2BGR);}finally{foreach(var channel in channels)channel.Dispose();}
 }
}
