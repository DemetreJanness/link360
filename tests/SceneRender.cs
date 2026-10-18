using System.Drawing;
using Link360Desktop;
internal static class SceneRender {
 public static void Run(string prefix){Exception? error=null;var thread=new Thread(()=>{try{using var image=SceneTests.Texture();var bytes=image.ToBytes(".png");var body=new BodyResult([],640,360,"fixture",7,Objects:[new(40,220,100,110,.83,"chair",56),new(520,180,30,40,.74,"cup",41)]);var match=SceneRegistration.Match(bytes,bytes,body,body);
  foreach(var size in new[]{new Size(1120,780),new Size(920,720)}){using var dialog=new SceneScanDialog((_,_)=>{},()=>{},()=>{},()=>{},()=>{});dialog.Size=size;dialog.Configure(true);dialog.CreateControl();dialog.Frames(bytes,bytes,body,body,match,2);dialog.Status("Scanning upper right · 2/5 saved","Gold ring: active target. Green dots: fixed-feature matches. Amber boxes: excluded objects.",true);var content=dialog.Controls[0];dialog.Controls.Remove(content);content.BackColor=dialog.BackColor;content.ForeColor=dialog.ForeColor;content.Font=dialog.Font;content.Size=dialog.ClientSize;content.CreateControl();content.PerformLayout();using var bitmap=new Bitmap(content.Width,content.Height);content.DrawToBitmap(bitmap,content.ClientRectangle);bitmap.Save(prefix+"-"+size.Width+".png");}
 }catch(Exception e){error=e;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(error!=null)throw error;}
}
