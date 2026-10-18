using System.Drawing.Drawing2D;

namespace Link360Desktop;

// Displays only in-memory frames. Capture and motion remain owned by the controller.
internal sealed class PreviewSurface : Control {
 Image? frame;
 RectangleF[] boxes=[];
 RectangleF[] chairs=[];
 PointF[] centers=[];
 string caption="Waiting for live camera";
 Color overlay=Color.FromArgb(79,221,178);
 RoomPoint[] mapped=[];
 readonly bool collectionGuide;
 RoomCollectionGuide guide=new([],null);
 bool sceneMode,sceneRoom;PointF[] sceneFeatures=[];PointF? sceneCentre;int sceneTarget;SceneObject[] sceneObjects=[];
 readonly bool framing;string? feedback;DateTime feedbackUntil;Color feedbackColor;
 public PreviewSurface(bool framing=false,bool collectionGuide=false){this.framing=framing;this.collectionGuide=collectionGuide;DoubleBuffered=true;BackColor=Color.FromArgb(15,19,25);ForeColor=Color.WhiteSmoke;MinimumSize=new Size(220,130);}
 public void UpdateFrame(byte[] bytes,BodyResult? body=null,FaceResult? faces=null) {
  sceneMode=false;
  using var stream=new MemoryStream(bytes);using var decoded=Image.FromStream(stream);
  var next=new Bitmap(decoded);var previous=frame;frame=next;previous?.Dispose();
  if(body!=null){boxes=body.People.Select(p=>new RectangleF((float)(p.X/body.Width),(float)(p.Y/body.Height),(float)(p.Width/body.Width),(float)(p.Height/body.Height))).ToArray();centers=body.People.Select(p=>new PointF((float)(p.CenterX/body.Width),(float)(p.CenterY/body.Height))).ToArray();caption=body.People.Length==1?(body.ArmsOut?"Ready · arms-out recognized":body.PoseReason??"Torso target detected"):$"Capture held · {body.People.Length} people visible";overlay=body.People.Length!=1?Color.Tomato:body.PoseReason!=null&&!body.ArmsOut?Color.Goldenrod:Color.MediumSpringGreen;}
  else if(faces!=null){boxes=faces.Faces.Select(p=>new RectangleF((float)(p.X/faces.Width),(float)(p.Y/faces.Height),(float)(p.Width/faces.Width),(float)(p.Height/faces.Height))).ToArray();centers=[];caption=faces.Faces.Length==1?"Face detected":$"{faces.Faces.Length} faces visible";}
  else{boxes=[];centers=[];caption="Live local preview";}
  if(framing&&body!=null){if(body.People.Length==1&&LinkLock.HasUsableTorso(body)){var p=body.People[0];bool centered=Math.Abs(p.CenterX/body.Width-.5)<.065&&Math.Abs(p.CenterY/body.Height-.5)<.065;overlay=centered?Color.MediumSpringGreen:Color.Goldenrod;caption=body.AnchorKind=="head"?(centered?"Head detected · centred":"Head detected · outside framing target"):body.AnchorKind=="upper-body"?(centered?"Head + shoulders detected · centred":"Head + shoulders detected · outside framing target"):(centered?"Subject detected · centred":"Subject detected · outside framing target");}else if(body.People.Length<=1){overlay=Color.Goldenrod;caption=body.People.Length==1?(body.AnchorKind=="missing"?"Upper-body lock uncertain":"Partial Link view"):"Subject not detected";}}
  chairs=body?.Chairs?.Select(p=>new RectangleF((float)(p.X/body.Width),(float)(p.Y/body.Height),(float)(p.Width/body.Width),(float)(p.Height/body.Height))).ToArray()??[];
  if(body!=null){string score=body.People.Length>0?body.People.Max(p=>p.Confidence).ToString("P0"):"below threshold / missing";caption+=$" · score {score} · minimum {TrackingConfidence.Minimum:P0}";}
  Invalidate();
  AccessibleName=caption;
 }
 public void Clear(string message){frame?.Dispose();frame=null;boxes=[];chairs=[];centers=[];caption=message;AccessibleName=message;Invalidate();}
 public void SetPoints(RoomPoint[] points){if(mapped.SequenceEqual(points))return;mapped=points.ToArray();if(collectionGuide)guide=RoomCollectionGuide.Create(mapped);Invalidate();}
 public void Feedback(string text,Color color){feedback=text;feedbackColor=color;feedbackUntil=DateTime.UtcNow.AddSeconds(5);Invalidate();}
 public void SceneOverlay(PointF[] features,int targetIndex,PointF? centre,bool roomView,SceneObject[] objects){sceneMode=true;sceneFeatures=features.Where((_,i)=>i%Math.Max(1,features.Length/160)==0).Take(160).ToArray();sceneTarget=targetIndex;sceneCentre=centre;sceneRoom=roomView;sceneObjects=objects;caption=features.Length>0?$"{features.Length} fixed-feature matches · objects excluded":"Looking for fixed room detail · objects excluded";AccessibleName=caption;overlay=Color.LightSeaGreen;Invalidate();}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
  if(frame==null){TextRenderer.DrawText(g,caption,Font,ClientRectangle,ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis);return;}
  float scale=Math.Min(Width/(float)frame.Width,Height/(float)frame.Height);var view=new RectangleF((Width-frame.Width*scale)/2,(Height-frame.Height*scale)/2,frame.Width*scale,frame.Height*scale);g.DrawImage(frame,view);
  if(collectionGuide){
   PointF Screen(PointF p)=>new(view.X+p.X*view.Width,view.Y+p.Y*view.Height);
   if(guide.Boundary.Length>=3){using var coverage=new SolidBrush(Color.FromArgb(25,64,215,182));var polygon=guide.Boundary.Select(Screen).ToArray();g.FillPolygon(coverage,polygon);using var edge=new Pen(Color.FromArgb(150,64,215,182),1){DashStyle=DashStyle.Dot};g.DrawPolygon(edge,polygon);}
   if(guide.Next is PointF suggestion){var at=Screen(suggestion);using var ring=new Pen(Color.Gold,3){DashStyle=DashStyle.Dash};float rx=view.Width*.055f,ry=view.Height*.055f;g.DrawEllipse(ring,at.X-rx,at.Y-ry,rx*2,ry*2);g.DrawLine(ring,at.X-6,at.Y,at.X+6,at.Y);g.DrawLine(ring,at.X,at.Y-6,at.X,at.Y+6);}
   string hint=mapped.Length>=25?"25 points saved · test your map":guide.Next!=null?"Gold ring: suggested gap · place your torso here":mapped.Length<4?"Start with 4 spread positions · left/right, near/far":"No large gaps · test map or expand its edges";
   using var banner=new SolidBrush(Color.FromArgb(215,15,19,25));g.FillRectangle(banner,0,0,Width,52);TextRenderer.DrawText(g,hint,Font,new Rectangle(10,5,Width-20,44),Color.Gold,TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis);
  }
  if(sceneMode){
   PointF Screen(PointF p)=>new(view.X+p.X*view.Width,view.Y+p.Y*view.Height);
   using var objectFill=new SolidBrush(Color.FromArgb(80,200,130,40));using var objectPen=new Pen(Color.Goldenrod,1);
   foreach(var o in sceneObjects){float x=view.X+(float)o.X/frame.Width*view.Width,y=view.Y+(float)o.Y/frame.Height*view.Height,w=(float)o.Width/frame.Width*view.Width,h=(float)o.Height/frame.Height*view.Height;g.FillRectangle(objectFill,x,y,w,h);g.DrawRectangle(objectPen,x,y,w,h);g.DrawString($"{o.Kind} {o.Confidence:P0}",Font,Brushes.Gold,x,y);}
   foreach(var point in sceneFeatures){var p=Screen(point);g.FillEllipse(Brushes.Aquamarine,p.X-2,p.Y-2,4,4);}
   if(sceneRoom){for(int i=0;i<SceneScanPolicy.Targets.Length;i++){var t=SceneScanPolicy.Targets[i];var p=Screen(new((float)t.X,(float)t.Y));using var pen=new Pen(i==sceneTarget?Color.Gold:Color.LightSlateGray,i==sceneTarget?3:1);g.DrawEllipse(pen,p.X-13,p.Y-13,26,26);TextRenderer.DrawText(g,i==0?"C":i.ToString(),Font,new Point((int)p.X-5,(int)p.Y-8),i==sceneTarget?Color.Gold:Color.White);}if(sceneCentre is {} centre){var p=Screen(centre);using var pen=new Pen(Color.Aqua,3);g.DrawLine(pen,p.X-8,p.Y,p.X+8,p.Y);g.DrawLine(pen,p.X,p.Y-8,p.X,p.Y+8);}}
  }else using(var chairPen=new Pen(Color.SteelBlue,1)){foreach(var b in chairs){float x=view.X+b.X*view.Width,y=view.Y+b.Y*view.Height;g.DrawRectangle(chairPen,x,y,b.Width*view.Width,b.Height*view.Height);g.DrawString("chair",Font,Brushes.LightSteelBlue,x,y);}}
  var activeColor=DateTime.UtcNow<feedbackUntil?feedbackColor:overlay;using var outline=new Pen(activeColor,2);foreach(var b in boxes)g.DrawRectangle(outline,view.X+b.X*view.Width,view.Y+b.Y*view.Height,b.Width*view.Width,b.Height*view.Height);
  foreach(var c in centers)g.DrawEllipse(outline,view.X+c.X*view.Width-4,view.Y+c.Y*view.Height-4,8,8);
  using var target=new Pen(Color.FromArgb(220,255,255,255),1){DashStyle=DashStyle.Dash};float cx=view.X+view.Width/2,cy=view.Y+view.Height/2;g.DrawLine(target,cx-12,cy,cx+12,cy);g.DrawLine(target,cx,cy-12,cx,cy+12);
  g.DrawRectangle(target,cx-view.Width*.045f,cy-view.Height*.055f,view.Width*.09f,view.Height*.11f);
  for(int i=0;i<mapped.Length;i++){float px=view.X+(float)mapped[i].X*view.Width,py=view.Y+(float)mapped[i].Y*view.Height;using var dot=new SolidBrush(Color.MediumSpringGreen);g.FillEllipse(dot,px-5,py-5,10,10);TextRenderer.DrawText(g,sceneMode?(i==0?"C":i.ToString()):(i+1).ToString(),Font,new Point((int)px+7,(int)py-10),Color.White);}
  using var shade=new SolidBrush(Color.FromArgb(190,15,19,25));g.FillRectangle(shade,0,Height-44,Width,44);TextRenderer.DrawText(g,DateTime.UtcNow<feedbackUntil?feedback:caption,Font,new Rectangle(10,Height-41,Width-20,38),activeColor,TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis);
 }
 protected override void Dispose(bool disposing){if(disposing){frame?.Dispose();frame=null;}base.Dispose(disposing);}
}

internal sealed class CalibrationDialog : Form {
 readonly Label stage=new(){AutoSize=false,Dock=DockStyle.Fill,Text="Ready to prepare · camera movement is off",Font=new Font("Segoe UI",14,FontStyle.Bold)};
 readonly Label failure=new(){AutoSize=false,Dock=DockStyle.Fill,ForeColor=Color.FromArgb(255,192,120),Text=""};
 readonly Label points=new(){AutoSize=true,Text="0 / 25 saved · minimum 4"};
 readonly CheckBox nativeOff=new(){AutoSize=true,Text="I have turned off the Link’s built-in tracking"};
 readonly PreviewSurface room=new(collectionGuide:true){Dock=DockStyle.Fill},link=new(true){Dock=DockStyle.Fill};
 readonly Button start;
 readonly Button testMap;
 readonly Button finish;
 readonly ProgressBar hold=new(){Width=190,Height=22,Maximum=100};
 [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
 public bool NativeOff {get=>nativeOff.Checked;set=>nativeOff.Checked=value;}
 public CalibrationDialog(Action<bool> startCalibration,Action cancel,Action clearMap,Action runTest,Action finishCalibration) {
  Text="LINK360 · Room calibration";Size=new Size(1160,850);MinimumSize=new Size(920,780);StartPosition=FormStartPosition.CenterParent;BackColor=SuiteTheme.Background;ForeColor=SuiteTheme.Text;Font=new Font("Segoe UI",10);
  var layout=new TableLayoutPanel(){Dock=DockStyle.Fill,Padding=new Padding(28),ColumnCount=1,RowCount=7};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
  foreach(var height in new[]{76f,56f,0f,84f,42f,48f,104f})layout.RowStyles.Add(height==0?new RowStyle(SizeType.Percent,100):new RowStyle(SizeType.Absolute,height));Controls.Add(layout);
  stage.Font=new Font("Segoe UI",15,FontStyle.Bold);stage.AutoEllipsis=true;stage.Padding=new Padding(0,8,0,0);failure.AutoEllipsis=true;failure.ForeColor=Color.FromArgb(255,192,120);failure.Padding=new Padding(0,4,0,0);points.Font=new Font("Segoe UI",11,FontStyle.Bold);points.ForeColor=SuiteTheme.Accent;
  layout.Controls.Add(stage,0,0);layout.Controls.Add(failure,0,1);
  var previews=new TableLayoutPanel(){Dock=DockStyle.Fill,ColumnCount=2,RowCount=2};previews.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));previews.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));previews.RowStyles.Add(new RowStyle(SizeType.Absolute,28));previews.RowStyles.Add(new RowStyle(SizeType.Percent,100));
  previews.Controls.Add(SuiteTheme.Label("ROOM CAMERA  /  Torso position",10,true,SuiteTheme.Muted),0,0);previews.Controls.Add(SuiteTheme.Label("LINK CAMERA  /  Subject framing",10,true,SuiteTheme.Muted),1,0);previews.Controls.Add(SuiteTheme.Card(room,8),0,1);previews.Controls.Add(SuiteTheme.Card(link,8),1,1);layout.Controls.Add(previews,0,2);
  layout.Controls.Add(new Label(){Dock=DockStyle.Fill,Text="Stand still where both cameras see your torso. Wait for ‘Point saved’, then move. No pose needed.\nGreen dots: saved points. Shaded area: measured coverage. Gold ring: suggested gap, not a saved point.\nUse safe, visible positions only. Save 4–25 points; add positions near the coverage edges to expand it."},0,3);
  layout.Controls.Add(nativeOff,0,4);var progress=new FlowLayoutPanel(){Dock=DockStyle.Fill};progress.Controls.Add(points);progress.Controls.Add(hold);progress.Controls.Add(new Label(){Text="2-second stillness hold",AutoSize=true});layout.Controls.Add(progress,0,5);
  var buttons=new FlowLayoutPanel(){Dock=DockStyle.Fill,WrapContents=true,AutoScroll=true};start=MakeButton("Start listening",()=>{failure.Text="";startCalibration(NativeOff);});var stop=MakeButton("Stop calibration",cancel);testMap=MakeButton("Test room map",runTest);testMap.Enabled=false;finish=MakeButton("Finish calibration",finishCalibration);finish.Enabled=false;buttons.Controls.Add(start);buttons.Controls.Add(stop);buttons.Controls.Add(finish);buttons.Controls.Add(testMap);buttons.Controls.Add(MakeButton("Reset room points",clearMap));buttons.Controls.Add(MakeButton("Close",Close));layout.Controls.Add(buttons,0,6);
  start.BackColor=SuiteTheme.Accent;start.ForeColor=SuiteTheme.Background;start.FlatAppearance.BorderColor=SuiteTheme.Accent;SuiteTheme.Inputs(this);
  FormClosing+=(_,_)=>cancel();
 }
 static Button MakeButton(string text,Action action)=>SuiteTheme.Button(text,action);
 public void UpdateStatus(string text){if(IsDisposed)return;stage.Text=text;bool failed=text.Contains("failed",StringComparison.OrdinalIgnoreCase)||text.Contains("not saved",StringComparison.OrdinalIgnoreCase)||text.Contains("stopped",StringComparison.OrdinalIgnoreCase)||text.Contains("Turn off",StringComparison.OrdinalIgnoreCase)||text.Contains("unavailable",StringComparison.OrdinalIgnoreCase)||text.Contains("needs",StringComparison.OrdinalIgnoreCase);if(failed)failure.Text="Last issue: "+text;stage.ForeColor=failed?Color.FromArgb(255,192,120):Color.WhiteSmoke;if(text.StartsWith("Saved room point")||text.StartsWith("Room map ready")){room.Feedback("Captured · "+text,Color.MediumSpringGreen);link.Feedback("Captured · "+text,Color.MediumSpringGreen);}}
 public void UpdateFailure(string text){if(IsDisposed)return;failure.Text="Last issue: "+text;stage.ForeColor=Color.FromArgb(255,192,120);bool mapIncomplete=text.StartsWith("Point saved;");if(!mapIncomplete){start.Text="Retry this position";hold.Value=0;}var color=mapIncomplete?Color.Goldenrod:Color.Tomato;var prefix=mapIncomplete?"Map incomplete · ":"Rejected · ";room.Feedback(prefix+text,color);link.Feedback(prefix+text,color);}
 public void UpdateRoom(byte[] bytes,BodyResult result){if(!IsDisposed)room.UpdateFrame(bytes,result);}
 public void UpdateLink(byte[] bytes,FaceResult? result=null){if(!IsDisposed)link.UpdateFrame(bytes,faces:result);}
 public void UpdateLinkBody(byte[] bytes,BodyResult result){if(!IsDisposed)link.UpdateFrame(bytes,result);}
 public void UpdateHold(double fraction){if(!IsDisposed)hold.Value=(int)Math.Clamp(fraction*100,0,100);}
 public void SetPoints(RoomPoint[] saved){if(!IsDisposed)room.SetPoints(saved);}
 public void UpdatePoints(int count){if(!IsDisposed)points.Text=$"{count} / 25 saved · minimum 4";}
 public void SetMapReady(bool ready){if(!IsDisposed){testMap.Enabled=ready;finish.Enabled=ready;}}
}
