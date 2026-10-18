using System.Drawing;
using System.Windows.Forms;
using Link360Desktop;
internal static class GuideRender {
 public static void Run(string output){
  Exception? error=null;var thread=new Thread(()=>{try{
   using var image=new Bitmap(640,360);using(var g=Graphics.FromImage(image)){g.Clear(Color.FromArgb(45,50,60));}
   using var bytes=new MemoryStream();image.Save(bytes,System.Drawing.Imaging.ImageFormat.Jpeg);
   using var preview=new PreviewSurface(collectionGuide:true){Size=new Size(510,300),Font=new Font("Segoe UI",10)};
   preview.UpdateFrame(bytes.ToArray(),new BodyResult([new(220,70,160,240,300,166,.72)],640,360,"fixture",7));
   preview.SetPoints([new(.2,.25,0,0),new(.8,.25,0,0),new(.8,.75,0,0),new(.2,.75,0,0)]);
   preview.CreateControl();using var result=new Bitmap(preview.Width,preview.Height);preview.DrawToBitmap(result,preview.ClientRectangle);result.Save(output);
  }catch(Exception e){error=e;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(error!=null)throw error;
 }
}
