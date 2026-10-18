using System.Runtime.InteropServices;
using System.Text.Json;
using Link360;

namespace Link360Desktop;
internal sealed partial class ControllerForm : Form {
 [DllImport("user32.dll",SetLastError=true)] static extern bool RegisterHotKey(IntPtr window,int id,uint modifiers,uint key);
 [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr window,int id);
 readonly CameraEngine engine=new();
 readonly ObsVideoRoute videoRoute=new(new LocalObsRpc());
 readonly CancellationTokenSource lifetime=new();
 readonly Label videoStatus=new(){AutoSize=true,MaximumSize=new Size(680,0),Text="Checking local OBS video route…"};
 CancellationTokenSource cancellation=new();
 CameraReading? state;
 Pose? returnPose;
 bool busy,refreshing,hotkeysEnabled=true,started,cameraAvailable;
 int failures;
 int requestVersion;
 readonly Label status=new(){AutoSize=true,MaximumSize=new Size(660,0),Text="Connecting…"};
 readonly Dictionary<string,NumericUpDown> values=new();
 readonly Dictionary<int,Action> shortcuts=new();
 readonly List<int> registered=new();
 readonly Button[] presetButtons=new Button[4];
 readonly Settings settings;
 readonly string settingsFile;
 readonly System.Windows.Forms.Timer refreshTimer=new(){Interval=5000};
 readonly HttpClient localObs=new(new HttpClientHandler(){UseProxy=false,AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(5)};
 readonly Label hotkeyStatus=new(){AutoSize=true};
 readonly Label trackingStatus=new(){AutoSize=true,MaximumSize=new Size(660,0),Text="Local AI tracking off · Ctrl + Alt + Shift + T"};
 readonly CheckBox reversePan=new(){Text="Reverse tracking pan",AutoSize=true},reverseTilt=new(){Text="Reverse tracking tilt",AutoSize=true};
 CancellationTokenSource? tracking;
 bool trackingMoving;
 Pose? trackingOrigin;
 readonly TrackingPlanner trackingPlanner=new();
 readonly OverviewFrames overviewFrames=new();
 readonly CameraFeeds feeds=new();
 readonly SemaphoreSlim trackingFramesReady=new(0,1);
 readonly CalibrationAudio calibrationAudio=new();
 readonly PreviewSurface mainRoomPreview=new(),mainLinkPreview=new(true);
 CalibrationDialog? calibrationDialog;
 CancellationTokenSource? linkPreview;
 readonly Label roomStatus=new(){AutoSize=true,MaximumSize=new Size(650,0),Text="Room overview off · Acer + local GPU body detection"};
 readonly Label gestureStatus=new(){AutoSize=true,MaximumSize=new Size(650,0),Text="Arm gesture detector off"};
 readonly CheckBox roomMode=new(){Text="Use room/body tracking with T",AutoSize=true},nativeOff=new(){Text="Camera's built-in tracking is off",AutoSize=true};
 CancellationTokenSource? overview;
 BodyResult? body;
 DateTime bodyTime;
 int bodyVersion;

 int armFrames;
 string? mapFitIssue;
 readonly ComboBox framingSelector=new(){DropDownStyle=ComboBoxStyle.DropDownList};
 readonly ComboBox trackingModeSelector=new(){DropDownStyle=ComboBoxStyle.DropDownList};
 readonly Button trackingToggle;
 bool preparingTracking;
 BodyResult? linkObservation;DateTime linkObservationTime;
 RoomMapTest? roomTest;string? testStopReason,lastTestReport;
 FeedFrame? lastRoomFrame,lastLinkFrame;
 RoomAlignment? roomAlignment;
 readonly Label syncStatus=new(){AutoSize=true,MaximumSize=new Size(248,0),Text="Sync is optional: centre your torso, pause, then sync. Applies this session only."};
 readonly CheckBox recordTestVideo=new(){Text="Save local test video (no audio)",AutoSize=true};
 static string DiagnosticsDirectory=>CommunityProfile.DiagnosticsDirectory;
 public ControllerForm() {
  Text=CommunityProfile.Title; Width=1380; Height=920; MinimumSize=new Size(1100,760);
  BackColor=SuiteTheme.Background;ForeColor=SuiteTheme.Text;Font=new Font("Segoe UI",10);
  Directory.CreateDirectory(CommunityProfile.DataDirectory); settingsFile=CommunityProfile.SettingsFile;
  try {settings=File.Exists(settingsFile)?JsonSerializer.Deserialize<Settings>(File.ReadAllText(settingsFile),Program.Json)!:new();if(settings.Presets.Length!=4)throw new Exception();}
  catch {settings=new();status.Text="Saved settings could not be read; existing file preserved.";}
  try{RoomMap.Fit(settings.RoomPoints);roomMode.Checked=true;}catch(IOException){}
  framingSelector.Items.AddRange(new object[]{"Wide shot","Torso shot"});framingSelector.SelectedIndex=Math.Clamp(settings.FramingMode,0,1);framingSelector.SelectedIndexChanged+=(_,_)=>ChangeFraming();
  trackingModeSelector.Items.AddRange(new object[]{"Software + Acer recovery","Software · Link only","Built-in AI · manual"});trackingModeSelector.SelectedIndex=roomMode.Checked?0:1;trackingModeSelector.SelectedIndexChanged+=(_,_)=>{Cancel();roomMode.Checked=trackingModeSelector.SelectedIndex==0;if(trackingModeSelector.SelectedIndex==2)trackingStatus.Text="Built-in AI · use palm activation; automatic restart is unavailable";};
  var shell=new TableLayoutPanel(){Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Padding=new Padding(0),Margin=new Padding(0)};shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,150));shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));Controls.Add(shell);
  var sidebar=new Panel(){Dock=DockStyle.Fill,BackColor=Color.FromArgb(17,23,32),Padding=new Padding(16,28,12,20)};shell.Controls.Add(sidebar,0,0);
  var navigation=SuiteTheme.Stack();sidebar.Controls.Add(navigation);navigation.Controls.Add(SuiteTheme.Label("LINK360",19,true));navigation.Controls.Add(SuiteTheme.Label("COMMUNITY REVIEW",9,true,SuiteTheme.Muted));navigation.Controls.Add(new Panel(){Height=24,Width=1});
  var content=new Panel(){Dock=DockStyle.Fill,Padding=new Padding(22,22,16,18)};shell.Controls.Add(content,1,0);
  var pages=new Dictionary<string,Panel>();var navButtons=new Dictionary<string,Button>();
  Panel Page(string name,string subtitle){var page=new Panel(){Dock=DockStyle.Fill,Visible=false,AutoScroll=true,BackColor=SuiteTheme.Background};pages[name]=page;content.Controls.Add(page);var header=new Panel(){Dock=DockStyle.Top,Height=90};header.Controls.Add(new Label(){Text=name,Font=new Font("Segoe UI",25,FontStyle.Bold),ForeColor=SuiteTheme.Text,AutoSize=true,Location=new Point(0,0)});header.Controls.Add(new Label(){Text=subtitle,ForeColor=SuiteTheme.Muted,AutoSize=true,Location=new Point(2,49)});page.Controls.Add(header);return page;}
  void Navigate(string name){foreach(var pair in pages)pair.Value.Visible=pair.Key==name;pages[name].BringToFront();foreach(var pair in navButtons){pair.Value.BackColor=pair.Key==name?SuiteTheme.Raised:sidebar.BackColor;pair.Value.ForeColor=pair.Key==name?SuiteTheme.Accent:SuiteTheme.Muted;}}
  foreach(var name in new[]{"Tracking","Cameras","Controls","Settings","Sharing"}){var destination=name;var b=SuiteTheme.Button(name,()=>Navigate(destination));b.Width=122;b.Height=46;b.TextAlign=ContentAlignment.MiddleLeft;b.Margin=new Padding(0,0,0,8);b.FlatAppearance.BorderSize=0;navigation.Controls.Add(b);navButtons[name]=b;}
  var local=new Label(){Text="LOCAL WORKSPACE\nLocal processing.\nTest clips only if enabled.",Dock=DockStyle.Bottom,Height=55,ForeColor=SuiteTheme.Muted,Font=new Font("Segoe UI",9)};sidebar.Controls.Add(local);
  var trackingPage=Page("Tracking","LINK STUDIO  /  Your live camera workspace");
  var trackingLayout=new TableLayoutPanel(){Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Padding=new Padding(0,90,0,0),MinimumSize=new Size(0,520)};trackingLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));trackingLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,300));trackingPage.Controls.Add(trackingLayout);trackingLayout.SendToBack();
  var stageGrid=new TableLayoutPanel(){Dock=DockStyle.Fill,ColumnCount=1,RowCount=3};stageGrid.RowStyles.Add(new RowStyle(SizeType.Absolute,34));stageGrid.RowStyles.Add(new RowStyle(SizeType.Percent,100));stageGrid.RowStyles.Add(new RowStyle(SizeType.Absolute,100));stageGrid.Controls.Add(SuiteTheme.Label("INSTA360 LINK  /  LIVE FRAMING",10,true,SuiteTheme.Muted),0,0);mainLinkPreview.Dock=DockStyle.Fill;stageGrid.Controls.Add(mainLinkPreview,0,1);
  var stageActions=Row(SuiteTheme.Button("Start / retry previews",RestartPreviews),SuiteTheme.Button("Pause previews",PausePreviews),SuiteTheme.Button("Room scene setup",OpenSceneSetup),SuiteTheme.Button("Test tracking · 60 sec",StartRoomTest),recordTestVideo);stageActions.WrapContents=true;stageActions.AutoSize=false;stageActions.Dock=DockStyle.Fill;stageActions.Margin=new Padding(0,10,0,0);stageGrid.Controls.Add(stageActions,0,2);trackingLayout.Controls.Add(SuiteTheme.Card(stageGrid,14),0,0);
  var inspector=new TableLayoutPanel(){Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=new Padding(0)};inspector.RowStyles.Add(new RowStyle(SizeType.Percent,100));inspector.RowStyles.Add(new RowStyle(SizeType.Absolute,225));trackingLayout.Controls.Add(inspector,1,0);
  var inspectorCard=new TableLayoutPanel(){Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=new Padding(0)};inspectorCard.RowStyles.Add(new RowStyle(SizeType.AutoSize));inspectorCard.RowStyles.Add(new RowStyle(SizeType.Percent,100));
  var inspectorHeadline=SuiteTheme.Stack();inspectorHeadline.Controls.Add(SuiteTheme.Label("TRACKING",9,true,SuiteTheme.Accent));trackingStatus.Font=new Font("Segoe UI",11,FontStyle.Bold);trackingStatus.AutoSize=false;trackingStatus.Size=new Size(248,84);trackingStatus.MaximumSize=new Size(248,84);trackingStatus.AutoEllipsis=true;trackingStatus.Margin=new Padding(0,0,0,12);inspectorHeadline.Controls.Add(trackingStatus);inspectorCard.Controls.Add(inspectorHeadline,0,0);
  var inspectorScroll=new Panel(){Dock=DockStyle.Fill,AutoScroll=true,Margin=new Padding(0)};var inspectorBody=SuiteTheme.Stack();inspectorBody.Dock=DockStyle.None;inspectorBody.AutoSizeMode=AutoSizeMode.GrowAndShrink;inspectorBody.Padding=new Padding(0,0,0,20);inspectorBody.Location=Point.Empty;inspectorScroll.Controls.Add(inspectorBody);inspectorCard.Controls.Add(inspectorScroll,0,1);
  Button InspectorButton(string label,Action callback,bool primary=false){var button=SuiteTheme.Button(label,callback,primary);button.AutoSize=false;button.Width=248;button.Height=39;button.Margin=new Padding(0,0,0,8);return button;}
  trackingToggle=InspectorButton("Start tracking",ToggleTracking,true);inspectorHeadline.Controls.Add(trackingToggle);inspectorHeadline.Controls.Add(InspectorButton("Stop movement",Cancel));
  nativeOff.Text="Built-in camera tracking is off";nativeOff.MaximumSize=new Size(248,0);nativeOff.Margin=new Padding(0,4,0,8);inspectorHeadline.Controls.Add(nativeOff);
  void InspectorSelector(string title,ComboBox selector){inspectorBody.Controls.Add(SuiteTheme.Label(title,10,true));selector.Width=248;selector.DropDownWidth=300;selector.DropDownStyle=ComboBoxStyle.DropDownList;selector.BackColor=SuiteTheme.Raised;selector.ForeColor=SuiteTheme.Text;selector.FlatStyle=FlatStyle.Flat;selector.Margin=new Padding(0,0,0,12);inspectorBody.Controls.Add(selector);}
  InspectorSelector("Tracking mode",trackingModeSelector);InspectorSelector("Framing",framingSelector);
  var confidenceLabel=SuiteTheme.Label("Minimum body score · 50%",10,true);inspectorBody.Controls.Add(confidenceLabel);
  var confidenceSlider=new TrackBar(){Minimum=35,Maximum=80,Value=50,TickFrequency=15,Width=248,Height=40,AutoSize=false,AccessibleName="Minimum body detection score; lower accepts weaker detections"};
  confidenceSlider.ValueChanged+=(_,_)=>{TrackingConfidence.Minimum=confidenceSlider.Value/100.0;confidenceLabel.Text=$"Minimum body score · {confidenceSlider.Value}%";};inspectorBody.Controls.Add(confidenceSlider);
  inspectorBody.Controls.Add(new Label(){AutoSize=true,MaximumSize=new Size(248,0),ForeColor=SuiteTheme.Muted,Text="Lower = more lenient. Higher = stricter. Live scores appear on previews. Scores are model estimates; body-shape guards remain active. Session only."});
  inspectorBody.Controls.Add(SuiteTheme.Label("ROOM SCENE SETUP",9,true,SuiteTheme.Accent));
  inspectorBody.Controls.Add(InspectorButton("Room scene setup",OpenSceneSetup));inspectorBody.Controls.Add(sceneUnmoved);inspectorBody.Controls.Add(sceneStatus);
  sceneUnmoved.Enabled=settings.SceneHome!=null&&!settings.CameraPlacementChanged;if(settings.SceneHome!=null)sceneStatus.Text="Scene saved · confirm mounts unchanged, or run setup again.";
  sceneUnmoved.CheckedChanged+=(_,_)=>{if(!sceneUnmoved.Checked&&!sceneScanning)Cancel();};
  inspectorBody.Controls.Add(InspectorButton("Collect room points",OpenCalibration));inspectorBody.Controls.Add(InspectorButton("Test room map · 60 sec",StartRoomTest));inspectorBody.Controls.Add(InspectorButton("Last test report",ShowTestReport));

  inspectorBody.Controls.Add(InspectorButton("Sync subject",SyncSubject));inspectorBody.Controls.Add(syncStatus);
  inspectorBody.Controls.Add(SuiteTheme.Label("WORKSPACE STATUS",9,true,SuiteTheme.Muted));foreach(var label in new[]{status,roomStatus,gestureStatus}){label.MaximumSize=new Size(248,0);label.ForeColor=SuiteTheme.Muted;label.Font=new Font("Segoe UI",9);label.Margin=new Padding(0,0,0,7);inspectorBody.Controls.Add(label);}inspector.Controls.Add(SuiteTheme.Card(inspectorCard,14),0,0);
  void SizeInspectorBody(){int width=Math.Max(120,inspectorScroll.ClientSize.Width-4);inspectorBody.SuspendLayout();inspectorBody.MinimumSize=new Size(width,0);inspectorBody.MaximumSize=new Size(width,0);inspectorBody.Width=width;foreach(Control item in inspectorBody.Controls){if(item is Label label)label.MaximumSize=new Size(width,0);else if(item is System.Windows.Forms.Button or ComboBox)item.Width=Math.Max(110,width-item.Margin.Horizontal);}inspectorBody.ResumeLayout(true);}
  inspectorScroll.ClientSizeChanged+=(_,_)=>SizeInspectorBody();
  inspectorScroll.HandleCreated+=(_,_)=>SizeInspectorBody();
  inspectorBody.SizeChanged+=(_,_)=>inspectorScroll.AutoScrollMinSize=new Size(0,inspectorBody.Height+8);
  var roomGrid=new TableLayoutPanel(){Dock=DockStyle.Fill,ColumnCount=1,RowCount=2};roomGrid.RowStyles.Add(new RowStyle(SizeType.Absolute,28));roomGrid.RowStyles.Add(new RowStyle(SizeType.Percent,100));roomGrid.Controls.Add(SuiteTheme.Label("ROOM VIEW  /  ACER",9,true,SuiteTheme.Muted),0,0);mainRoomPreview.Dock=DockStyle.Fill;mainRoomPreview.MinimumSize=new Size(180,120);roomGrid.Controls.Add(mainRoomPreview,0,1);inspector.Controls.Add(SuiteTheme.Card(roomGrid,12),0,1);
  FlowLayoutPanel PageStack(Panel page){var stack=SuiteTheme.Stack();stack.Width=800;page.Controls.Add(stack);stack.BringToFront();return stack;}
  var cameraPage=Page("Cameras","Your connected cameras and their roles in this workspace.");var cameras=PageStack(cameraPage);
  cameras.Controls.Add(SuiteTheme.Label("Insta360 Link",17,true));cameras.Controls.Add(SuiteTheme.Label("PTZ CAMERA  /  Primary framing",10,true,SuiteTheme.Accent));cameras.Controls.Add(SuiteTheme.Label("Pan, tilt and zoom controls use the installed Windows camera interface.",10,false,SuiteTheme.Muted));cameras.Controls.Add(new Panel(){Height=22});
  cameras.Controls.Add(SuiteTheme.Label("Acer FHD User Facing",17,true));cameras.Controls.Add(SuiteTheme.Label("FIXED CAMERA  /  Room overview",10,true,SuiteTheme.Accent));cameras.Controls.Add(SuiteTheme.Label("Local body detection gives the Link a wider view of the room.",10,false,SuiteTheme.Muted));cameras.Controls.Add(new Panel(){Height=20});cameras.Controls.Add(Row(SuiteTheme.Button("Restart camera previews",RestartPreviews),SuiteTheme.Button("Pause camera previews",PausePreviews)));
  cameras.Controls.Add(SuiteTheme.Label("Additional camera roles will appear here when their capture and control adapters are supported.",10,false,SuiteTheme.Muted));
  var controlsPage=Page("Controls","Manual framing and saved positions. Any movement overrides tracking.");var manual=PageStack(controlsPage);
  manual.Controls.Add(Row(SuiteTheme.Button("Refresh camera",()=>_ = Run(async ct=>state=await engine.Execute(new(),ct))),SuiteTheme.Button("Previous framing",()=>RecallReturn()),SuiteTheme.Button("Stop movement",Cancel)));
  manual.Controls.Add(SuiteTheme.Label("Manual movement",16,true));
  foreach(var name in new[]{"Pan","Tilt","Zoom"}){var input=new NumericUpDown(){Width=115,Minimum=-1000,Maximum=1000};values[name]=input;manual.Controls.Add(Row(new Label(){Text=name,Width=80,TextAlign=ContentAlignment.MiddleLeft},input,SuiteTheme.Button("Apply",()=>Set(name,(int)input.Value)),SuiteTheme.Button("−",()=>Nudge(name,-1)),SuiteTheme.Button("+",()=>Nudge(name,1))));}
  manual.Controls.Add(Row(SuiteTheme.Button("← Pan",()=>Nudge("Pan",-1)),SuiteTheme.Button("Pan →",()=>Nudge("Pan",1)),SuiteTheme.Button("Tilt ↑",()=>Nudge("Tilt",1)),SuiteTheme.Button("Tilt ↓",()=>Nudge("Tilt",-1))));manual.Controls.Add(Row(SuiteTheme.Button("Zoom +",()=>Nudge("Zoom",1)),SuiteTheme.Button("Zoom −",()=>Nudge("Zoom",-1))));
  manual.Controls.Add(SuiteTheme.Label("Saved positions",16,true));var duration=new NumericUpDown(){Minimum=.5m,Maximum=6,DecimalPlaces=1,Increment=.1m,Width=90,Value=Math.Clamp(settings.TransitionMs/1000m,.5m,6m)};duration.ValueChanged+=(_,_)=>{settings.TransitionMs=(int)(duration.Value*1000);try{PersistSettings();}catch(Exception e){status.Text=e.Message;}};manual.Controls.Add(Row(new Label(){Text="Transition seconds",Width=150},duration));
  for(int i=0;i<4;i++){int slot=i;var name=new TextBox(){Width=180,Text=settings.Presets[i]?.Name??$"Preset {i+1}"};var recall=SuiteTheme.Button("Recall",()=>Recall(slot));presetButtons[i]=recall;manual.Controls.Add(Row(new Label(){Text=$"0{i+1}",Width=35},name,SuiteTheme.Button("Save current",()=>SavePreset(slot,name.Text)),recall));}
  var settingsPage=Page("Settings","Tracking, calibration audio and keyboard shortcuts.");var extra=PageStack(settingsPage);
  extra.Controls.Add(SuiteTheme.Label("Tracking behavior",16,true));extra.Controls.Add(SuiteTheme.Label("Choose software, Acer recovery or built-in AI on the Tracking page.",10,false,SuiteTheme.Muted));extra.Controls.Add(Row(reversePan,reverseTilt));reversePan.CheckedChanged+=(_,_)=>Cancel();reverseTilt.CheckedChanged+=(_,_)=>Cancel();nativeOff.CheckedChanged+=(_,_)=>{if(!nativeOff.Checked)Cancel();};
  var obsFallback=new CheckBox(){Text="Allow local OBS feed if a camera is busy",AutoSize=true,Checked=true};obsFallback.CheckedChanged+=(_,_)=>feeds.AllowObsFallback=obsFallback.Checked;extra.Controls.Add(obsFallback);
  extra.Controls.Add(SuiteTheme.Label("Calibration",16,true));var speech=new CheckBox(){Text="British voice calibration prompts",AutoSize=true,Checked=true};speech.CheckedChanged+=(_,_)=>calibrationAudio.Enabled=speech.Checked;extra.Controls.Add(Row(speech,SuiteTheme.Button("Test voice",calibrationAudio.Test)));extra.Controls.Add(Row(SuiteTheme.Button("Open calibration",OpenCalibration),SuiteTheme.Button("Save mapping point",SaveRoomPoint),SuiteTheme.Button("Clear room map",ResetRoomMap)));
  extra.Controls.Add(SuiteTheme.Label("Global hotkeys",16,true));extra.Controls.Add(SuiteTheme.Label("CTRL + ALT + SHIFT",9,true,SuiteTheme.Accent));extra.Controls.Add(SuiteTheme.Label("T  Tracking     1–4  Presets     Arrows  Pan / tilt     Page Up / Down  Zoom\nHome  Previous framing     End  Stop movement\nF5  Reaction     F6  Replay     F7  Acer     F8  Link",10,false,SuiteTheme.Muted));extra.Controls.Add(hotkeyStatus);extra.Controls.Add(SuiteTheme.Button("Enable / suspend hotkeys",()=>ToggleHotkeys()));
  var sharingPage=Page("Sharing","Optional OBS and Discord integration for your camera output.");var integrations=PageStack(sharingPage);integrations.Controls.Add(SuiteTheme.Label("OBS Virtual Camera",17,true));integrations.Controls.Add(SuiteTheme.Label("OBS captures your camera output. Select OBS Virtual Camera in Discord.",10,false,SuiteTheme.Muted));integrations.Controls.Add(videoStatus);integrations.Controls.Add(Row(SuiteTheme.Button("Start virtual camera",()=>VideoAction(false),true),SuiteTheme.Button("Repair capture",()=>VideoAction(true))));integrations.Controls.Add(SuiteTheme.Label("Companion actions",16,true));integrations.Controls.Add(Row(SuiteTheme.Button("Reaction",()=>Obs("reaction")),SuiteTheme.Button("Save replay",()=>Obs("clip")),SuiteTheme.Button("Acer camera",()=>Obs("cameraTwo")),SuiteTheme.Button("Return to Link",()=>Obs("return"))));
  SuiteTheme.Inputs(this);Navigate("Tracking");
  Shown+=async(_,_)=>{await InitializeCameraInventory();if(IsDisposed)return;started=true;RegisterShortcuts();await Run(async ct=>state=await engine.Execute(new(),ct));refreshTimer.Start();_ = WatchVideo();};
  refreshTimer.Tick+=async(_,_)=>{if(!busy&&tracking==null)await Run(async ct=>state=await engine.Execute(new(),ct),false);};
  FormClosing+=(_,_)=>{started=false;Cancel();overview?.Cancel();linkPreview?.Cancel();lifetime.Cancel();calibrationAudio.Dispose();refreshTimer.Stop();foreach(var id in registered)UnregisterHotKey(Handle,id);localObs.Dispose();_ = videoRoute.DisposeAsync();_ = overviewFrames.DisposeAsync();_ = feeds.DisposeAsync();};
 }
 static FlowLayoutPanel Row(params Control[] controls) {var row=new FlowLayoutPanel(){AutoSize=true,WrapContents=false,Margin=new Padding(0,3,0,3)};row.Controls.AddRange(controls);return row;}
 static Button Button(string text,Action action) {var b=new Button(){Text=text,AutoSize=true,BackColor=Color.FromArgb(45,53,64),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};b.Click+=(_,_)=>action();return b;}
 Pose CurrentPose() {if(state==null)throw new Exception("Refresh to connect the camera.");int Get(string n)=>state.Controls.Single(c=>c.Name==n&&c.GetHResult==0&&c.RangeHResult==0).Value;return new(Get("Pan"),Get("Tilt"),Get("Zoom"));}
 async Task Run(Func<CancellationToken,Task> work,bool announce=true) {
  int version=announce?++requestVersion:requestVersion;
  var source=cancellation;
  while(busy){
   if(!announce)return;
   if(!refreshing){status.Text="Camera busy. Cancel before another action.";return;}
   await Task.Delay(25);
   if(version!=requestVersion||source.IsCancellationRequested)return;
  }
  if(source.IsCancellationRequested)return;
  bool previouslyConnected=state!=null;
  busy=true;refreshing=!announce;try{await work(source.Token);source.Token.ThrowIfCancellationRequested();if(state!=null){foreach(var pair in values){var c=state.Controls.Single(x=>x.Name==pair.Key);if(c.RangeHResult!=0||c.GetHResult!=0||c.Step<=0||(c.Capabilities&2)==0)throw new Exception($"{pair.Key} unavailable");pair.Value.Minimum=c.Min;pair.Value.Maximum=c.Max;pair.Value.Increment=c.Step;pair.Value.Value=c.Value;pair.Value.Enabled=true;}cameraAvailable=true;failures=0;refreshTimer.Interval=3000;if(announce||!previouslyConnected)status.Text="Connected · driver state confirmed";}}
  catch(Exception e){state=null;returnPose=null;if(e is CameraWorkerException failure&&failure.Kind=="camera_unavailable"){cameraAvailable=false;videoRoute.DeviceChanged();}source.Cancel();if(ReferenceEquals(source,cancellation))cancellation=new();++requestVersion;foreach(var input in values.Values)input.Enabled=false;failures=Math.Min(failures+1,3);refreshTimer.Interval=Math.Min(8000,1000*(1<<failures));status.Text=e.Message;}finally{busy=false;refreshing=false;}
 }
 void StopTracking(string reason="Tracking paused") {if(roomTest!=null)testStopReason=reason;tracking?.Cancel();tracking=null;trackingToggle.Text="Start tracking";trackingPlanner.Reset();trackingStatus.Text=reason;}
 void Cancel()=>Cancel("Stop movement or tracking settings changed");
 void Cancel(string reason){if(sceneScanning){sceneCancelReason=reason;RecordSceneEvent(new{type="cancel",phase=scenePhase,atUtc=DateTime.UtcNow,reason});}++requestVersion;StopTracking();cancellation.Cancel();cancellation=new();state=null;foreach(var input in values.Values)input.Enabled=false;status.Text="Cancelled. Camera state unknown until refreshed.";}
 async Task Manual(Func<CancellationToken,Task> action) {
  bool wasTracking=tracking!=null;StopTracking("Local AI tracking off · manual override");
  if(trackingMoving){Cancel("Manual camera control took over");while(busy)await Task.Delay(25);}
  await Run(async ct=>{if(wasTracking||state==null)state=await engine.Execute(new(),ct);await action(ct);});
 }
 void Set(string control,int value)=>_ = Manual(async ct=>{returnPose=CurrentPose();state=await engine.Execute(new(control,value),ct);});
 void Nudge(string control,int direction) {
  _ = Manual(async ct=>{var c=state!.Controls.Single(x=>x.Name==control);int delta=control=="Zoom"?10:2;
   int target=Math.Clamp(c.Value+direction*Math.Max(c.Step,delta/c.Step*c.Step),c.Min,c.Max);returnPose=CurrentPose();state=await engine.Execute(new(control,target),ct);});
 }
 async Task MoveCamera(Pose p,CancellationToken ct) {
  state=await engine.Execute(new(Target:p,DurationMs:settings.TransitionMs),ct);
 }
 void Recall(int slot)=>_ = Manual(async ct=>{var p=settings.Presets[slot]??throw new Exception("Save this preset first.");returnPose=CurrentPose();await MoveCamera(p.Position,ct);});
 void RecallReturn()=>_ = Manual(async ct=>{var p=returnPose??throw new Exception("No previous framing saved this session.");var previous=CurrentPose();await MoveCamera(p,ct);returnPose=previous;});
 void SavePreset(int slot,string name)=>_ = Run(async ct=>{
  if(String.IsNullOrWhiteSpace(name)||name.Length>60)throw new Exception("Preset name must be 1–60 characters.");
  state=await engine.Execute(new(),ct);
  settings.Presets[slot]=new(name.Trim(),CurrentPose());PersistSettings();
 });
 void PersistSettings(){var temp=settingsFile+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(settings,Program.Json));File.Move(temp,settingsFile,true);}
 async void ToggleTracking() {
  if(tracking!=null){StopTracking();if(trackingMoving){cancellation.Cancel();cancellation=new();state=null;}return;}
  if(preparingTracking)return;
  if(trackingModeSelector.SelectedIndex==2){trackingStatus.Text="Built-in AI mode · activate with palm; LINK360 motor commands are paused";return;}
  if(!nativeOff.Checked){trackingStatus.Text="First turn off the camera's built-in tracking, then check the box below.";return;}
  preparingTracking=true;trackingToggle.Enabled=false;
  try{
  if(roomMode.Checked&&(overview==null||linkPreview==null)){trackingStatus.Text="Starting both camera previews…";await StartPreviews();}
  int version=requestVersion;
  while(busy&&refreshing&&!lifetime.IsCancellationRequested)await Task.Delay(25);
  if(lifetime.IsCancellationRequested||version!=requestVersion||tracking!=null)return;
  if(!cameraAvailable||state==null||busy){trackingStatus.Text="Refresh the camera and finish movement before enabling tracking.";return;}
   if(roomMode.Checked){try{if(!nativeOff.Checked)throw new IOException("Turn off built-in tracking with the palm gesture first, then check the box.");RoomMap.Fit(settings.RoomPoints);if(overview==null&&linkPreview==null)throw new IOException("Start camera previews first. Tracking waits for a fresh torso.");}catch(Exception e){trackingStatus.Text=e.Message;return;}}
  trackingOrigin=CurrentPose();tracking=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);trackingToggle.Text="Pause tracking";trackingPlanner.Reset();if(roomMode.Checked)_ = TrackRoom(tracking);else _ = Track(tracking);
  }catch(Exception e){trackingStatus.Text="Tracking could not start · "+e.Message;}finally{preparingTracking=false;trackingToggle.Enabled=true;}
 }
 async void ChangeFraming(){bool resume=tracking!=null;settings.FramingMode=framingSelector.SelectedIndex;try{PersistSettings();await Manual(async ct=>{var zoom=state!.Controls.Single(c=>c.Name=="Zoom");int desired=Math.Clamp(settings.FramingMode==1?160:100,zoom.Min,zoom.Max);desired=zoom.Min+(int)Math.Round((desired-zoom.Min)/(double)zoom.Step)*zoom.Step;var pose=CurrentPose();state=await engine.Execute(new(Target:pose with{Zoom=desired},DurationMs:750),ct);});if(resume&&state!=null&&cameraAvailable)ToggleTracking();}catch(Exception e){trackingStatus.Text="Framing stopped · "+e.Message;}}
 void ToggleOverview(){if(overview!=null){overview.Cancel();overview=null;body=null;StopTracking();roomStatus.Text="Room overview off";gestureStatus.Text="Arm gesture detector off";return;}armFrames=0;overview=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);_ = WatchOverview(overview);}
 async void RestartPreviews(){try{await StartPreviews();}catch(Exception e){trackingStatus.Text="Previews could not start · "+e.Message;}}
 async Task StartPreviews(){if(sceneScanning){trackingStatus.Text="Room scene scan is using both cameras · stop the scan before restarting previews.";return;}PausePreviews();int version=requestVersion;trackingStatus.Text="Starting camera previews · movement off";mainRoomPreview.Clear("Starting Acer camera…");mainLinkPreview.Clear("Starting Link camera…");await feeds.StopCapture();if(lifetime.IsCancellationRequested)return;if(version!=requestVersion){trackingStatus.Text="Preview start interrupted · retry when the camera is connected";return;}feeds.RetryDirect();ToggleOverview();linkPreview=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);_ = WatchLinkPreview(linkPreview);}
 void PausePreviews(){roomAlignment=null;syncStatus.Text="Session alignment cleared · sync is optional.";++requestVersion;overview?.Cancel();overview=null;linkPreview?.Cancel();linkPreview=null;body=null;linkObservation=null;lastRoomFrame=null;lastLinkFrame=null;if(trackingMoving)Cancel();else StopTracking();roomStatus.Text="Previews paused · camera movement off";gestureStatus.Text="Arm gesture detector paused";mainRoomPreview.Clear("Preview paused");mainLinkPreview.Clear("Preview paused");_ = feeds.StopCapture();}
 void SignalTrackingFrame(){if(trackingFramesReady.CurrentCount==0)trackingFramesReady.Release();}
 void PreviewReady(){if(tracking==null&&lastRoomFrame!=null&&lastLinkFrame!=null&&trackingStatus.Text=="Starting camera previews · movement off")trackingStatus.Text="Previews live · tracking off";}
 async Task WatchLinkPreview(CancellationTokenSource session){try{await using var detector=new LocalBodySession(session.Token,wholeBody:true,torso:true);while(!session.IsCancellationRequested){var packet=await feeds.LinkFrame(session.Token);var frame=packet.Bytes;var detected=await detector.Infer(frame,session.Token);session.Token.ThrowIfCancellationRequested();lastLinkFrame=packet;PreviewReady();linkObservation=detected;linkObservationTime=packet.ReadAtUtc??packet.ReceivedUtc;SignalTrackingFrame();mainLinkPreview.UpdateFrame(frame,detected);if(calibrationDialog is {IsDisposed:false})calibrationDialog.UpdateLinkBody(frame,detected);}}catch(OperationCanceledException)when(session.IsCancellationRequested){}catch(Exception e){if(!IsDisposed){mainLinkPreview.Clear(e.Message);if(calibrationDialog is {IsDisposed:false})calibrationDialog.UpdateFailure(e.Message);}}finally{if(ReferenceEquals(linkPreview,session))linkPreview=null;session.Dispose();}}
 void CalibrationStatus(string text){trackingStatus.Text=text;if(calibrationDialog is {IsDisposed:false}){calibrationDialog.UpdateStatus(text);if(text.StartsWith("Blocked")||text.StartsWith("Point not saved")||text.StartsWith("Calibration stopped"))calibrationDialog.UpdateFailure(text);calibrationDialog.UpdatePoints(settings.RoomPoints.Length);}}
 void PrepareTorsoMap(){PrepareSceneMap();if(settings.RoomMapVersion==1)return;settings.PreviousRoomPoints=settings.RoomPoints;settings.RoomPoints=[];settings.RoomMapVersion=1;PersistSettings();}
 void OpenCalibration(){
  if(calibrationDialog is {IsDisposed:false}){calibrationDialog.Activate();return;}
  StopTracking("Calibration preparation · movement off");
  if(SceneProblem(true) is string sceneIssue){OpenSceneSetup();SceneStatus("Scene setup comes first",sceneIssue,false);return;}
  calibrationDialog=new CalibrationDialog(confirmed=>{nativeOff.Checked=confirmed;StartGestureCalibration();},()=>{Cancel();if(calibrationDialog is {IsDisposed:false})calibrationDialog.UpdateStatus("Calibration stopped · movement off");},ResetRoomMap,StartRoomTest,FinishRoomCalibration);
  calibrationDialog.NativeOff=nativeOff.Checked;calibrationDialog.SetPoints(settings.RoomPoints);calibrationDialog.UpdatePoints(settings.RoomPoints.Length);calibrationDialog.Show(this);
  try{RoomMap.Fit(settings.RoomPoints);calibrationDialog.SetMapReady(true);}catch(IOException){}
  if(overview==null||linkPreview==null)RestartPreviews();
 }
 async Task WatchOverview(CancellationTokenSource session) {
  try {
   await using var gpu=new LocalBodySession(session.Token,wholeBody:true);byte[]? last=null;var changed=DateTime.UtcNow;
   roomStatus.Text="Starting local GPU room watch…";
   while(!session.IsCancellationRequested) {
    var packet=await feeds.RoomFrame(session.Token);var frame=packet.Bytes;var hash=System.Security.Cryptography.SHA256.HashData(frame);
    if(last==null||!hash.SequenceEqual(last))changed=DateTime.UtcNow;last=hash;
    var result=await gpu.Infer(frame,session.Token);session.Token.ThrowIfCancellationRequested();
    mainRoomPreview.UpdateFrame(frame,result);mainRoomPreview.SetPoints(settings.RoomPoints);if(calibrationDialog is {IsDisposed:false}){calibrationDialog.UpdateRoom(frame,result);calibrationDialog.SetPoints(settings.RoomPoints);}
    if(result.ArmsOut)armFrames++;
    gestureStatus.Text=result.People.Length==1?$"Torso detected · {result.People[0].Confidence:P0} confidence · GPU {result.InferenceMs} ms":result.People.Length==0?"No torso in Acer overview":"Multiple people · capture and movement held";
    if(packet.ReadAtUtc==null&&DateTime.UtcNow-changed>TimeSpan.FromSeconds(2)){body=null;roomStatus.Text="Acer frame freshness uncertain";}
    else {lastRoomFrame=packet;PreviewReady();body=result;bodyTime=packet.ReadAtUtc??packet.ReceivedUtc;bodyVersion++;roomStatus.Text=$"{result.People.Length} person(s) · {settings.RoomPoints.Length} map points · {feeds.RoomRoute}";if(calibrationDialog is {IsDisposed:false})calibrationDialog.UpdatePoints(settings.RoomPoints.Length);}
    SignalTrackingFrame();
   }
  }catch(OperationCanceledException)when(session.IsCancellationRequested){}
  catch(Exception e){if(ReferenceEquals(overview,session)){overview=null;body=null;roomStatus.Text="Acer unavailable · "+e.Message;mainRoomPreview.Clear(roomStatus.Text);}}
  finally{session.Dispose();}
 }
 void SaveRoomPoint()=>_ = Manual(async ct=>{
  RequireScene(true);
  if(!nativeOff.Checked)throw new IOException("Turn off built-in camera tracking before calibrating.");
  if(body==null||body.People.Length!=1||DateTime.UtcNow-bodyTime>TimeSpan.FromMilliseconds(800))throw new IOException("Calibration needs one live visible body in the Acer view.");
  state=await engine.Execute(new(),ct);var pose=CurrentPose();var person=body.People[0];
  PrepareTorsoMap();
  if(settings.RoomPoints.Length>=25)throw new IOException("Room map holds at most twenty-five points; finish or reset the map.");
  settings.RoomPoints=[..settings.RoomPoints,new(person.CenterX/body.Width,person.CenterY/body.Height,pose.Pan,pose.Tilt)];PersistSettings();
  try{RoomMap.Fit(settings.RoomPoints);roomStatus.Text=$"Room map ready with {settings.RoomPoints.Length} points · test slow movement first";}catch(Exception e){roomStatus.Text=$"Saved {settings.RoomPoints.Length} points · "+e.Message;}
 });
 async void StartGestureCalibration() {
  StopTracking();int version=requestVersion;while(busy&&refreshing&&!lifetime.IsCancellationRequested)await Task.Delay(25);
  if(lifetime.IsCancellationRequested||version!=requestVersion)return;
  string? blocked=SceneProblem(true)??(!nativeOff.Checked?"Confirm the camera's built-in tracking is off in this dialog.":overview==null?"Start camera previews first.":body==null?"Room camera needs fresh frames; restart previews.":state==null?"Link controls unavailable; refresh the camera.":busy?"A camera movement is in progress; wait or cancel.":null);
  if(blocked!=null){CalibrationStatus("Blocked · "+blocked);return;}
  PrepareTorsoMap();calibrationDialog?.UpdatePoints(settings.RoomPoints.Length);
  CalibrationStatus("Calibration listening · stand still for two seconds; no arm pose needed");
  tracking=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);_ = GestureCalibration(tracking);
 }
 async Task GestureCalibration(CancellationTokenSource session) {
  try {
   await using var linkBody=new LocalBodySession(session.Token,wholeBody:true,torso:true);await using var camera=new LocalCameraSession(session.Token);
   var stillness=new StillnessCapture();int seen=-1;
   while(!session.IsCancellationRequested) {
    if(!cameraAvailable||state==null)throw new IOException("Link unavailable; restart calibration after recovery.");
    if(bodyVersion!=seen&&!busy) {
     seen=bodyVersion;var sample=body;
     bool valid=sample!=null&&sample.People.Length==1&&DateTime.UtcNow-bodyTime<TimeSpan.FromMilliseconds(600);
     var person=valid?sample!.People[0]:null;double x=person==null?0:person.CenterX/sample!.Width,y=person==null?0:person.CenterY/sample!.Height;
     bool nearby=valid&&settings.RoomPoints.Any(p=>Math.Sqrt(Math.Pow(p.X-x,2)+Math.Pow(p.Y-y,2))<0.07);
     if(calibrationDialog is {IsDisposed:false})calibrationDialog.UpdateStatus(!valid?"Calibration listening · need one fresh torso in the room view":nearby||stillness.WaitingForMove?"Move to a new room position · current area already captured":"Listening · stand still for two seconds; no arm pose needed");
     bool capture=stillness.Tick(valid&&!nearby,x,y,DateTime.UtcNow);calibrationDialog?.UpdateHold(stillness.Progress(DateTime.UtcNow));
     if(capture) {
      if(settings.RoomPoints.Any(p=>Math.Sqrt(Math.Pow(p.X-x,2)+Math.Pow(p.Y-y,2))<0.07)){CalibrationStatus("Point not saved · Already mapped nearby; lower arms and move farther");System.Media.SystemSounds.Exclamation.Play();}
      else {
       try{mapFitIssue=null;await CentreAndRecord(linkBody,camera,session.Token);calibrationAudio.Saved(tracking==null);CalibrationStatus(tracking==null?"Room map ready · Ctrl + Alt + Shift + T starts body tracking":$"Saved room point {settings.RoomPoints.Length} · move to the next position");if(mapFitIssue!=null)calibrationDialog?.UpdateFailure("Point saved; map needs another position · "+mapFitIssue);}
       catch(OperationCanceledException){throw;}
       catch(Exception e){var issue="Point not saved · "+e.Message+" · Retry this position when ready.";StopTracking(issue);CalibrationStatus(issue);System.Media.SystemSounds.Exclamation.Play();}
       await Task.Delay(1500,session.Token);
      }
     }
    }
    await Task.Delay(25,session.Token);
   }
  }catch(OperationCanceledException)when(session.IsCancellationRequested){}
  catch(Exception e){if(ReferenceEquals(tracking,session)){StopTracking("Calibration stopped · "+e.Message);CalibrationStatus(trackingStatus.Text);}}
  finally{session.Dispose();}
 }
 async Task CentreAndRecord(LocalBodySession linkBody,LocalCameraSession camera,CancellationToken ct) {
  if(settings.RoomPoints.Length>=25)throw new IOException("Room map has twenty-five points; finish calibration or reset the map.");
  var anchor=CurrentPose();var policy=new CenteringPolicy(anchor);var origin=body!.People[0];var clock=System.Diagnostics.Stopwatch.StartNew();int settled=0;var hashes=new HashSet<string>();
  while(clock.Elapsed<TimeSpan.FromSeconds(30)) {
   ct.ThrowIfCancellationRequested();
   if(body==null||body.People.Length!=1||DateTime.UtcNow-bodyTime>TimeSpan.FromMilliseconds(800))throw new IOException("Keep your torso visible in the Acer view.");
   if(Math.Abs(body.People[0].CenterX-origin.CenterX)/body.Width>0.05||Math.Abs(body.People[0].CenterY-origin.CenterY)/body.Height>0.05)throw new IOException("Hold still until the saved-point beep.");
   CalibrationStatus("Centering · Link is moving toward your torso; stay still");
   var frame=await feeds.Link(ct);hashes.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(frame)));var result=await linkBody.Infer(frame,ct);ct.ThrowIfCancellationRequested();
   mainLinkPreview.UpdateFrame(frame,result);if(calibrationDialog is {IsDisposed:false})calibrationDialog.UpdateLinkBody(frame,result);
   if(result.People.Length!=1||!LinkLock.HasUsableTorso(result))throw new IOException("Link needs visible shoulders and torso; face the Link or step back into view.");var person=result.People[0];double ex=person.CenterX/result.Width-0.5,ey=person.CenterY/result.Height-0.5;
   if(Math.Abs(ex)<0.045&&Math.Abs(ey)<0.055){settled++;if(settled>=3&&hashes.Count>=2)break;await Task.Delay(150,ct);continue;}settled=0;
   policy.Observe(ex,ey);
   int Axis(string name,double error,int sign){var c=state!.Controls.Single(c=>c.Name==name);if(c.Step<=0||(c.Capabilities&2)==0)throw new IOException("Calibration control unsupported.");int units=Math.Abs(error)<(name=="Pan"?0.045:0.055)?0:Math.Clamp((int)Math.Round(Math.Abs(error)*20),1,5);int delta=Math.Max(c.Step,units/c.Step*c.Step)*Math.Sign(error)*sign;return units==0?c.Value:Math.Clamp(c.Value+delta,c.Min,c.Max);}
   var pose=CurrentPose();var target=new Pose(Axis("Pan",ex,reversePan.Checked?-1:1),Axis("Tilt",ey,reverseTilt.Checked?1:-1),pose.Zoom);
   policy.Check(pose,target);
   trackingMoving=true;try{await Run(async cancel=>{using var both=CancellationTokenSource.CreateLinkedTokenSource(cancel,ct);state=await engine.Execute(new(Target:target,DurationMs:75),both.Token,camera);},false);}finally{trackingMoving=false;}
   if(state==null)throw new IOException("Centering was interrupted.");
  }
  if(settled<3||hashes.Count<2)throw new IOException("Centering/fresh-frame check timed out; no mapping point was saved.");
  ct.ThrowIfCancellationRequested();var sample=body;if(sample==null||sample.People.Length!=1||DateTime.UtcNow-bodyTime>TimeSpan.FromMilliseconds(800))throw new IOException("Room target lost before saving.");var p=sample.People[0];var confirmed=CurrentPose();
  settings.RoomPoints=[..settings.RoomPoints,new(p.CenterX/sample.Width,p.CenterY/sample.Height,confirmed.Pan,confirmed.Tilt)];PersistSettings();
  if(settings.RoomPoints.Length>=4){try{RoomMap.Fit(settings.RoomPoints);calibrationDialog?.SetMapReady(true);if(settings.RoomPoints.Length==25){roomMode.Checked=true;StopTracking("Room map ready · Test room map is available");}}catch(IOException e){mapFitIssue=e.Message;calibrationDialog?.SetMapReady(false);}}
 }
 void FinishRoomCalibration(){try{RoomMap.Fit(settings.RoomPoints);if(trackingMoving)throw new IOException("Wait for centering to finish before completing the map.");StopTracking("Room map complete · Test room map is available");roomMode.Checked=true;calibrationDialog?.UpdateStatus(trackingStatus.Text);calibrationAudio.Complete();}catch(Exception e){calibrationDialog?.UpdateFailure(e.Message);}}
 void SyncSubject()=>_ = Manual(async ct=>{
  state=await engine.Execute(new(ReadNativeTracking:true),ct);
  try{
   if(state.NativeTrackingEnabled!=false)throw new IOException("Turn built-in AI off before syncing.");
   if(overview==null||linkPreview==null)throw new IOException("Start both previews first.");
   RequireScene();var map=RoomMap.Fit(settings.RoomPoints);var observations=new List<RoomSyncObservation>();var initial=CurrentPose();
   syncStatus.Text="Sync listening · keep your torso centred and still…";
   for(int i=0;i<5;i++){await Task.Delay(120,ct);if(body==null||linkObservation==null)throw new IOException("Need one visible torso in both views.");observations.Add(new(body,bodyTime,linkObservation,linkObservationTime));}
   state=await engine.Execute(new(),ct);if(CurrentPose()!=initial)throw new IOException("Camera moved during sync; pause and retry.");
   roomAlignment=RoomAlignment.Sync(map,observations,state,DateTime.UtcNow);
   syncStatus.Text=$"Synced this session · pan {roomAlignment.Pan:+0;-0;0}, tilt {roomAlignment.Tilt:+0;-0;0}. Saved points unchanged.";trackingStatus.Text="Room alignment synced · Start tracking when ready";
  }catch(IOException e){syncStatus.Text="Sync not changed · "+e.Message;trackingStatus.Text=syncStatus.Text;}
 });
 async Task TrackRoom(CancellationTokenSource session,RoomMapTest? test=null) {
  string testReason="interrupted";
  try {
   RequireScene();var map=RoomMap.Fit(settings.RoomPoints);await using var cameraSession=new LocalCameraSession(session.Token,continuous:true);
   var planner=new HybridTrackingPlanner(map,roomAlignment);
   while(!session.IsCancellationRequested) {
    if(test?.Expired==true){testReason="60-second test completed";StopTracking("Room-map test complete · last report is ready");break;}
    if(!cameraAvailable||state==null)throw new IOException("PTZ controls unavailable · "+status.Text);
    if(!busy) {
     var sample=body;var decisionRoomAt=bodyTime;var decisionLink=linkObservation;var decisionLinkAt=linkObservationTime;var decisionRoomFrame=lastRoomFrame;var decisionLinkFrame=lastLinkFrame;bool forcedAcer=test is {ElapsedMs:>=35000 and <40000};var before=CurrentPose();var decision=planner.Evaluate(sample,decisionRoomAt,forcedAcer&&decisionLink?.People.Length is not >1?null:decisionLink,decisionLinkAt,state,reversePan.Checked,reverseTilt.Checked,DateTime.UtcNow);
     var target=decision.Target;Pose? requested=target;double? commandMs=null;double? motionScale=target==null?null:decision.SpeedScale;int? relativePan=decision.RelativePan,relativeTilt=decision.RelativeTilt;string outcome=decision.Outcome;double? roomX=decision.RoomX,roomY=decision.RoomY;if(test==null)trackingStatus.Text=decision.Status;
     if(decision.Hold)state=await engine.Execute(new(Hold:true),session.Token,cameraSession);
     else if((target==null||target==CurrentPose())&&state!=null&&DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()-state.SnapshotUtcMilliseconds>150)
      state=await engine.Execute(new(DurationMs:0),session.Token,cameraSession);
      if(target!=null&&target!=CurrentPose()) {
       outcome="move";var commandClock=System.Diagnostics.Stopwatch.StartNew();
       double speed=decision.SpeedScale;
       trackingMoving=true;try{await Run(async ct=>{using var both=CancellationTokenSource.CreateLinkedTokenSource(ct,session.Token);state=await engine.Execute(new(Target:target,DurationMs:75,SpeedScale:speed,RelativePan:relativePan,RelativeTilt:relativeTilt),both.Token,cameraSession);},false);}finally{trackingMoving=false;}
       commandMs=commandClock.Elapsed.TotalMilliseconds;
      }
     if(test!=null){var link=decisionLink;var targetPerson=link?.People.Length==1?link.People[0]:null;Pose? resolved=state?.ResolvedTargetPan is int rp&&state.ResolvedTargetTilt is int rt?new(rp,rt,CurrentPose().Zoom):null;Pose? commanded=state?.CommandedPan is int cp&&state.CommandedTilt is int ct?new(cp,ct,CurrentPose().Zoom):null;var testSample=new RoomTestSample(test.ElapsedMs,outcome,sample?.People.Length??0,sample?.People.Length==1?sample.People[0].Confidence:null,roomX,roomY,(DateTime.UtcNow-decisionRoomAt).TotalMilliseconds,sample?.InferenceMs??0,link?.People.Length??0,targetPerson==null?null:targetPerson.CenterX/link!.Width-.5,targetPerson==null?null:targetPerson.CenterY/link!.Height-.5,link==null?null:(DateTime.UtcNow-decisionLinkAt).TotalMilliseconds,before,requested,state==null?null:CurrentPose(),commandMs,motionScale,relativePan,relativeTilt,resolved,commanded,link?.AnchorKind,state?.SnapshotUtcMilliseconds is >0?DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()-state.SnapshotUtcMilliseconds:null,forcedAcer);testSample=testSample with{RecoveryAlignment=planner.RecoveryAlignment};test.Record(testSample);test.Capture(decisionRoomFrame,decisionLinkFrame,testSample);trackingStatus.Text=$"Tracking test · {test.ElapsedMs/1000}/60 sec\n{TestPhase(decision,forcedAcer)}";calibrationDialog?.UpdateStatus(trackingStatus.Text);}
    }
    await trackingFramesReady.WaitAsync(TimeSpan.FromMilliseconds(100),session.Token);
   }
  }catch(OperationCanceledException)when(session.IsCancellationRequested){testReason=testStopReason??"cancelled";}
  catch(Exception e){testReason="failed · "+e.Message;if(ReferenceEquals(tracking,session))StopTracking("Room tracking stopped · "+e.Message);}
  finally{if(test!=null){try{lastTestReport=test.Finish(testReason);if(testReason=="60-second test completed")trackingStatus.Text=test.CompletionMessage;roomStatus.Text="Room-map test report saved locally · "+testReason;calibrationDialog?.UpdateStatus(roomStatus.Text);}catch(Exception e){roomStatus.Text="Test report error · "+e.Message;}try{test.Dispose();}catch{}if(ReferenceEquals(roomTest,test))roomTest=null;}session.Dispose();}
 }
 static string TestPhase(TrackingDecision decision,bool forcedAcer){
  string phase=decision.Outcome switch{"outside-map"=>"Outside saved room area","alignment-range"=>"Framing exceeds camera range","handoff-hold"=>"Waiting for a clear subject","acquiring"=>"Finding subject","fine-hold"=>"Holding framing","fine-wait"=>"Link view guiding","fine-centering"=>"Adjusting framing",_=>"Acer view guiding"};
  return forcedAcer?"Acer takeover · "+phase:phase;
 }
 async void StartRoomTest(){
  if(roomTest!=null){trackingStatus.Text="Test already running · Stop movement ends it";return;}
  StopTracking();int version=requestVersion;while(busy&&refreshing&&!lifetime.IsCancellationRequested)await Task.Delay(25);if(version!=requestVersion||lifetime.IsCancellationRequested)return;
  try{RequireScene();RoomMap.Fit(settings.RoomPoints);if(!nativeOff.Checked)throw new IOException("Confirm built-in tracking is off using the checkbox above.");if(state==null||busy||overview==null||linkPreview==null||body==null)throw new IOException("Start both previews and wait for a connected Link and live torso. "+roomStatus.Text);
   roomTest=new RoomMapTest(DiagnosticsDirectory,settings.RoomPoints,recordTestVideo.Checked,roomAlignment);testStopReason=null;tracking=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);trackingToggle.Text="Pause tracking";_ = TrackRoom(tracking,roomTest);
  }catch(Exception e){trackingStatus.Text="Test not started · "+e.Message;}
 }
 void ShowTestReport(){var directory=DiagnosticsDirectory;if(!Directory.Exists(directory))directory=Path.Combine(AppContext.BaseDirectory,"logs");var path=lastTestReport??(Directory.Exists(directory)?Directory.GetFiles(directory,"room-test-*.summary.json").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault():null);if(path==null){trackingStatus.Text="No room-map test report yet";return;}var report=new Form(){Text="LINK360 · Local room-map test report",Size=new Size(760,650),BackColor=SuiteTheme.Background};report.Controls.Add(new TextBox(){Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,Font=new Font("Consolas",11),BackColor=SuiteTheme.Background,ForeColor=SuiteTheme.Text,Text=File.ReadAllText(path)+"\r\n\r\nSaved locally: "+path});report.Show(this);}
 async Task Track(CancellationTokenSource session) {
  byte[]? lastHash=null;var lastChanged=DateTime.UtcNow;
  try {
   await using var inference=new LocalBodySession(session.Token,wholeBody:true,torso:true);
   await using var cameraSession=new LocalCameraSession(session.Token,continuous:true,limitedTravel:true);
   trackingStatus.Text="Local AI tracking on · manual movement turns it off";
   while(!session.IsCancellationRequested) {
    if(!cameraAvailable||state==null)throw new IOException("Camera unavailable; enable tracking again after recovery.");
    if(!busy) {
     var cycle=System.Diagnostics.Stopwatch.StartNew();
     var packet=await feeds.LinkFrame(session.Token);var frame=packet.Bytes;
     var hash=System.Security.Cryptography.SHA256.HashData(frame);
     if(lastHash==null||!hash.SequenceEqual(lastHash))lastChanged=DateTime.UtcNow;lastHash=hash;
     if(packet.ReadAtUtc==null&&DateTime.UtcNow-lastChanged>TimeSpan.FromSeconds(2))throw new IOException("Camera frames unchanged; restart preview before tracking.");
     var result=await inference.Infer(frame,session.Token);session.Token.ThrowIfCancellationRequested();
     if(state==null||busy){trackingPlanner.Reset();continue;}
     var target=trackingPlanner.PlanBody(result,state,reversePan.Checked,reverseTilt.Checked,packet.ReadAtUtc??packet.ReceivedUtc);
     if(target==null)state=await engine.Execute(new(Hold:true),session.Token,cameraSession);
     if(target!=null&&trackingOrigin!=null&&(Math.Abs(target.Pan-trackingOrigin.Pan)>20||Math.Abs(target.Tilt-trackingOrigin.Tilt)>15))throw new IOException("Tracking travel limit reached; reframe manually before enabling again.");
     trackingStatus.Text=result.People.Length==0?"Tracking on · no torso; holding position":result.People.Length>1?"Tracking on · multiple people; holding position":"Tracking on · torso target detected";
     if(target!=null&&!busy) {
      trackingMoving=true;
      var before=CurrentPose();try {await Run(async ct=>{using var both=CancellationTokenSource.CreateLinkedTokenSource(ct,session.Token);state=await engine.Execute(new(Target:target,DurationMs:75,SpeedScale:trackingPlanner.SpeedScale,RelativePan:target.Pan-before.Pan,RelativeTilt:target.Tilt-before.Tilt),both.Token,cameraSession);},false);}
      finally{trackingMoving=false;}
      if(state==null)throw new IOException("Tracking movement interrupted; enable again to resume.");
     }
     if(!session.IsCancellationRequested)trackingStatus.Text+=$" · {cycle.ElapsedMilliseconds} ms update";
    }
    if(busy)await Task.Delay(15,session.Token);
   }
  }catch(OperationCanceledException)when(session.IsCancellationRequested){}
  catch(Exception e){if(ReferenceEquals(tracking,session))StopTracking("Local AI tracking stopped · "+e.Message);}
  finally{session.Dispose();}
 }
 async Task WatchVideo() {
  while(!lifetime.IsCancellationRequested) {
   try {var obsProcesses=System.Diagnostics.Process.GetProcessesByName("obs64");bool running=obsProcesses.Length>0;foreach(var process in obsProcesses)process.Dispose();videoStatus.Text=running?await videoRoute.Check(cameraAvailable,lifetime.Token):"OBS is closed · sharing optional; direct previews work independently";}
   catch(OperationCanceledException)when(lifetime.IsCancellationRequested){break;}
   catch(Exception e){if(!IsDisposed)videoStatus.Text="OBS route: "+e.Message;}
   try{await Task.Delay(2000,lifetime.Token);}catch(OperationCanceledException){break;}
  }
 }
 async void VideoAction(bool repair) {
  try{if(repair)await videoRoute.Repair(lifetime.Token);else await videoRoute.StartShared(lifetime.Token);videoStatus.Text=repair?"OBS capture repair requested":"OBS Virtual Camera running · select it in Discord";}
  catch(Exception e){if(!IsDisposed)videoStatus.Text=e.Message;}
 }
 void RegisterShortcuts() {
  for(int i=0;i<4;i++){int slot=i;shortcuts[1+i]=()=>Recall(slot);Register(1+i,Keys.D1+i);}
  var map=new[]{(Keys.Left,"Pan",-1),(Keys.Right,"Pan",1),(Keys.Up,"Tilt",1),(Keys.Down,"Tilt",-1),(Keys.PageUp,"Zoom",1),(Keys.PageDown,"Zoom",-1)};
  int id=10;foreach(var item in map){var entry=item;shortcuts[id]=()=>Nudge(entry.Item2,entry.Item3);Register(id++,entry.Item1);}
  shortcuts[20]=RecallReturn;Register(20,Keys.Home);shortcuts[21]=()=>{Cancel();hotkeysEnabled=false;hotkeyStatus.Text="Global hotkeys suspended";};Register(21,Keys.End);
  shortcuts[22]=ToggleTracking;Register(22,Keys.T);
  var obsKeys=new[]{(Keys.F5,"reaction"),(Keys.F6,"clip"),(Keys.F7,"cameraTwo"),(Keys.F8,"return")};
  id=30;foreach(var item in obsKeys){var entry=item;shortcuts[id]=()=>Obs(entry.Item2);Register(id++,entry.Item1);}
  if(hotkeyStatus.Text.Length==0)hotkeyStatus.Text="Global hotkeys registered";
 }
 void Register(int id,Keys key) {if(RegisterHotKey(Handle,id,0x4007,(uint)key))registered.Add(id);else hotkeyStatus.Text+=$"Unavailable: {key}. ";}
 void ToggleHotkeys(){Cancel();hotkeysEnabled=!hotkeysEnabled;hotkeyStatus.Text=hotkeysEnabled?"Global hotkeys enabled":"Global hotkeys suspended";}
 protected override void WndProc(ref Message m){
  if(started&&m.Msg==0x219&&m.WParam.ToInt64()==7)CheckCameraInventory();
  if(m.Msg==0x312 && shortcuts.TryGetValue(m.WParam.ToInt32(),out var action) && (hotkeysEnabled||m.WParam.ToInt32()==21))action();
  base.WndProc(ref m);
 }
 async void Obs(string action) {
  try {
   // Optional companion location is resolved from the workspace supplied at launch.
   var workspace=Environment.GetEnvironmentVariable("LINK360_OBS_DIR");
   if(workspace==null)throw new Exception("OBS companion path is not configured by this launcher.");
   var tokenFile=Path.Combine(workspace,"director/.token");
   using var request=new HttpRequestMessage(HttpMethod.Post,"http://127.0.0.1:4466/events");
   request.Headers.Authorization=new("Bearer",File.ReadAllText(tokenFile).Trim());
   request.Content=new StringContent(JsonSerializer.Serialize(new{source="link360",action}),System.Text.Encoding.UTF8,"application/json");
   using var response=await localObs.SendAsync(request);if(!response.IsSuccessStatusCode)throw new Exception("OBS companion rejected the action; check its status.");status.Text=$"OBS: {action}";
  }catch(Exception e){status.Text=e.Message;}
 }
}
