namespace Link360Desktop;
internal sealed class CoverageWizard:Form {
 readonly PreviewSurface room=new(false,true),link=new(true);
 readonly Label status=new(){Dock=DockStyle.Fill,AutoSize=false,ForeColor=SuiteTheme.Text,Font=new Font("Segoe UI",13,FontStyle.Bold)};
 readonly Label detail=new(){Dock=DockStyle.Fill,AutoSize=false,ForeColor=SuiteTheme.Muted};
 readonly ProgressBar hold=new(){Dock=DockStyle.Fill,Maximum=100};readonly Button run;
 public CoverageWizard(Action start,Action test,Action stop){
  Text="LINK360 · Fresh coverage calibration";Width=1180;Height=780;MinimumSize=new(900,650);BackColor=SuiteTheme.Background;ForeColor=SuiteTheme.Text;Font=new("Segoe UI",10);
  var grid=new TableLayoutPanel(){Dock=DockStyle.Fill,Padding=new(20),ColumnCount=1,RowCount=6};grid.RowStyles.Add(new(SizeType.Absolute,65));grid.RowStyles.Add(new(SizeType.Absolute,65));grid.RowStyles.Add(new(SizeType.Percent,100));grid.RowStyles.Add(new(SizeType.Absolute,28));grid.RowStyles.Add(new(SizeType.Absolute,58));grid.RowStyles.Add(new(SizeType.Absolute,40));Controls.Add(grid);
  grid.Controls.Add(status,0,0);grid.Controls.Add(detail,0,1);var views=new TableLayoutPanel(){Dock=DockStyle.Fill,ColumnCount=2,RowCount=2};views.ColumnStyles.Add(new(SizeType.Percent,50));views.ColumnStyles.Add(new(SizeType.Percent,50));views.RowStyles.Add(new(SizeType.Absolute,28));views.RowStyles.Add(new(SizeType.Percent,100));views.Controls.Add(SuiteTheme.Label("ACER · fresh measured coverage",10,true),0,0);views.Controls.Add(SuiteTheme.Label("LINK · head lock / horizontal reacquisition",10,true),1,0);room.Dock=link.Dock=DockStyle.Fill;views.Controls.Add(room,0,1);views.Controls.Add(link,1,1);grid.Controls.Add(views,0,2);grid.Controls.Add(hold,0,3);
  var actions=new FlowLayoutPanel(){Dock=DockStyle.Fill,WrapContents=false};actions.Controls.Add(SuiteTheme.Button("Start / resume calibration",start,true));run=SuiteTheme.Button("Test fresh map · 60 sec",test);run.Enabled=false;actions.Controls.Add(run);actions.Controls.Add(SuiteTheme.Button("Stop movement",stop));grid.Controls.Add(actions,0,4);grid.Controls.Add(SuiteTheme.Label("LOCAL DEBUG SESSION · saved maps and presets stay unchanged · minimum 4 fresh points",9,false,SuiteTheme.Muted),0,5);
  FormClosing+=(_,_)=>stop();State("Ready for fresh calibration","Walk slowly left/right and nearer/farther; pause for automatic capture. No arm pose, empty-room scan or manual point button.",0,false);
 }
 public void State(string text,string hint,double progress,bool ready){if(IsDisposed)return;status.Text=text;detail.Text=hint;hold.Value=(int)Math.Clamp(progress*100,0,100);run.Enabled=ready;}
 public void Room(byte[] bytes,BodyResult result,RoomPoint[] points){if(IsDisposed)return;room.UpdateFrame(bytes,result);room.SetPoints(points);}
 public void Link(byte[] bytes,BodyResult result){if(!IsDisposed)link.UpdateFrame(bytes,result);}
 public void Captured(){if(!IsDisposed){room.Feedback("Captured · measured from this session",Color.MediumSpringGreen);link.Feedback("Captured · motor stopped, head/shoulders framed",Color.MediumSpringGreen);}}
}
