namespace Link360Desktop;
internal sealed class SceneScanDialog : Form {
 readonly Label stage=new(){Dock=DockStyle.Fill,Font=new Font("Segoe UI",15,FontStyle.Bold),Text="Ready · camera movement is off"};
 readonly Label details=new(){Dock=DockStyle.Fill,ForeColor=SuiteTheme.Muted,Text="Start the countdown, leave both camera views, then wait for the spoken completion message."};
 readonly PreviewSurface room=new(){Dock=DockStyle.Fill},link=new(){Dock=DockStyle.Fill};
 readonly NumericUpDown delay=new(){Minimum=10,Maximum=120,Value=30,Width=80};
 readonly CheckBox nativeOff=new(){Text="Built-in camera tracking is off",AutoSize=true};
 readonly Button start,stop,mounts,collectPoints,guided;
 SceneTarget[] targets=[];
 readonly Label inventory=new(){Dock=DockStyle.Fill,ForeColor=SuiteTheme.Muted,Text="Object inventory appears here during the scan."};
 public SceneScanDialog(Action<int,bool> begin,Action cancel,Action moved,Action collect,Action report,Action guidedCalibration){
  Text="LINK360 · Room scene setup";Size=new Size(1120,780);MinimumSize=new Size(920,720);BackColor=SuiteTheme.Background;ForeColor=SuiteTheme.Text;Font=new Font("Segoe UI",10);StartPosition=FormStartPosition.CenterParent;
  var grid=new TableLayoutPanel(){Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=1,RowCount=7};foreach(float height in new[]{58f,68f,0f,76f,52f,100f,52f})grid.RowStyles.Add(height==0?new RowStyle(SizeType.Percent,100):new RowStyle(SizeType.Absolute,height));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));Controls.Add(grid);grid.Controls.Add(stage,0,0);grid.Controls.Add(details,0,1);
  var views=new TableLayoutPanel(){Dock=DockStyle.Fill,ColumnCount=2,RowCount=2};views.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));views.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));views.RowStyles.Add(new RowStyle(SizeType.Absolute,26));views.RowStyles.Add(new RowStyle(SizeType.Percent,100));views.Controls.Add(SuiteTheme.Label("ACER · scene targets",10,true),0,0);views.Controls.Add(SuiteTheme.Label("LINK · fixed feature matches",10,true),1,0);views.Controls.Add(SuiteTheme.Card(room,6),0,1);views.Controls.Add(SuiteTheme.Card(link,6),1,1);grid.Controls.Add(views,0,2);
  grid.Controls.Add(new Label(){Dock=DockStyle.Fill,Text="Each scan sets a wide starting view, then finds up to 25 fresh locations.\nRings are targets; numbered green dots are verified this scan. Movable objects are excluded.\nPrevious maps are archived. Collect new person points after a completed scan."},0,3);
  var options=new FlowLayoutPanel(){Dock=DockStyle.Fill};options.Controls.Add(new Label(){Text="Time to leave (seconds)",AutoSize=true,Padding=new Padding(0,5,0,0)});options.Controls.Add(delay);options.Controls.Add(nativeOff);grid.Controls.Add(options,0,4);
  var buttons=new FlowLayoutPanel(){Dock=DockStyle.Fill,WrapContents=true,AutoScroll=true};start=SuiteTheme.Button("Start countdown",()=>begin((int)delay.Value,nativeOff.Checked),true);stop=SuiteTheme.Button("Stop scan",cancel);mounts=SuiteTheme.Button("Mounts moved",moved);collectPoints=SuiteTheme.Button("Collect points",collect);buttons.Controls.Add(start);buttons.Controls.Add(stop);buttons.Controls.Add(mounts);buttons.Controls.Add(collectPoints);buttons.Controls.Add(SuiteTheme.Button("Report",report));guided=SuiteTheme.Button("Guided calibration",guidedCalibration);buttons.Controls.Add(guided);grid.Controls.Add(buttons,0,5);grid.Controls.Add(inventory,0,6);SuiteTheme.Inputs(this);stop.Enabled=false;FormClosing+=(_,_)=>cancel();
 }
 public void ResetForScan(){targets=[];room.SetPoints([]);room.Clear("Finding new scene features…");link.Clear("Finding new scene features…");inventory.Text="Fresh scan · no previous measurements reused.";}
 public void Targets(SceneTarget[] discovered){targets=discovered.ToArray();}
 public void Configure(bool off){nativeOff.Checked=off;}
 public void Previews(byte[] roomBytes,byte[] linkBytes){room.UpdateFrame(roomBytes);link.UpdateFrame(linkBytes);}
 public void Anchors(IReadOnlyList<SceneAnchor> anchors){if(!IsDisposed)room.SetPoints(anchors.Select(a=>new RoomPoint(a.X,a.Y,a.Pan,a.Tilt)).ToArray());}
 public void Status(string title,string detail,bool running){stage.Text=title;details.Text=detail;start.Enabled=!running;start.Text=running?"Scan running":"Start countdown";stop.Enabled=running;mounts.Enabled=!running;collectPoints.Enabled=!running;guided.Enabled=!running;delay.Enabled=!running;nativeOff.AutoCheck=!running;}
 public void Frames(byte[] roomBytes,byte[] linkBytes,BodyResult r,BodyResult l,SceneMatch? match,int target){room.UpdateFrame(roomBytes,r);link.UpdateFrame(linkBytes,l);room.SceneOverlay(match?.RoomFeatures??[],target,match==null?null:new((float)match.X,(float)match.Y),true,r.Objects??[],targets);link.SceneOverlay(match?.LinkFeatures??[],target,null,false,l.Objects??[],targets);var names=(r.Objects??[]).Concat(l.Objects??[]).Select(o=>o.Kind).Distinct().Order().ToArray();inventory.Text="Detected (may move): "+(names.Length==0?"none":string.Join(" · ",names.Take(6)))+(names.Length>6?$" · +{names.Length-6} more":"")+". Scores and locations are in the local report.";}
}
