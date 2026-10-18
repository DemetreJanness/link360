namespace Link360Desktop;

internal static class SuiteTheme {
 public static readonly Color Background=Color.FromArgb(13,17,24),Surface=Color.FromArgb(22,29,39),Raised=Color.FromArgb(30,39,52),Text=Color.FromArgb(232,238,247),Muted=Color.FromArgb(151,169,189),Accent=Color.FromArgb(77,210,181),Border=Color.FromArgb(43,56,72);
 public static Label Label(string text,float size=10,bool bold=false,Color? color=null)=>new(){Text=text,AutoSize=true,ForeColor=color??Text,Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular),Margin=new Padding(0,0,0,8)};
 public static Button Button(string text,Action action,bool primary=false){var b=new Button(){Text=text,AutoSize=true,MinimumSize=new Size(110,38),Padding=new Padding(14,5,14,5),Margin=new Padding(0,0,10,0),FlatStyle=FlatStyle.Flat,BackColor=primary?Accent:Raised,ForeColor=primary?Background:Text,Font=new Font("Segoe UI",10,FontStyle.Bold),Cursor=Cursors.Hand};b.FlatAppearance.BorderColor=primary?Accent:Border;b.FlatAppearance.MouseOverBackColor=primary?Color.FromArgb(109,226,201):Color.FromArgb(41,54,72);b.Click+=(_,_)=>action();return b;}
 public static FlowLayoutPanel Stack()=>new(){Dock=DockStyle.Top,AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(0),Margin=new Padding(0)};
 public static Panel Card(Control content,int padding=20,bool autoSize=false){var card=new Panel(){Dock=DockStyle.Fill,BackColor=Surface,Padding=new Padding(padding),Margin=new Padding(0,0,16,12),AutoSize=autoSize,AutoSizeMode=AutoSizeMode.GrowAndShrink};content.Dock=autoSize?DockStyle.Top:DockStyle.Fill;card.Controls.Add(content);card.Paint+=(_,e)=>{using var p=new Pen(Border);e.Graphics.DrawRectangle(p,0,0,card.Width-1,card.Height-1);};return card;}
 public static void Inputs(Control root){foreach(Control c in root.Controls){if(c is NumericUpDown or TextBox){c.BackColor=Raised;c.ForeColor=Text;}else if(c is CheckBox){c.ForeColor=Text;c.Margin=new Padding(0,6,0,10);}Inputs(c);}}
}
