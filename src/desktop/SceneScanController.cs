using System.Diagnostics;
using System.Text.Json;
using Link360;
namespace Link360Desktop;

internal sealed partial class ControllerForm {
 readonly CheckBox sceneUnmoved=new(){AutoSize=true,MaximumSize=new Size(248,0),Text="Camera mounts have not moved since calibration setup"};
 readonly Label sceneStatus=new(){AutoSize=true,MaximumSize=new Size(248,0),Text="Scene setup required before collecting a new room map."};
 SceneScanDialog? sceneDialog;bool sceneScanning;string? lastSceneReport;
 string scenePhase="idle";string? sceneCancelReason;List<object>? sceneEvidence;
 void RecordSceneEvent(object entry){if(sceneEvidence is {Count:<500})sceneEvidence.Add(entry);}
 void SetScenePhase(string phase){scenePhase=phase;RecordSceneEvent(new{type="phase",phase,atUtc=DateTime.UtcNow});}
 void OpenSceneSetup(){
  if(sceneDialog is {IsDisposed:false}){sceneDialog.Activate();return;}
  sceneDialog=new(ScanRoomScene,()=>{if(sceneScanning)Cancel("Stop scan or scan window closed");},CamerasMoved,()=>{if(sceneScanning){SceneStatus("Scan in progress","Stop the scan before collecting points.",true);return;}OpenCalibration();},ShowSceneReport,BeginGuidedCalibration);
  sceneDialog.Configure(nativeOff.Checked);sceneDialog.Show(this);
  if(settings.PendingSceneScanId!=null){SceneStatus("New scan incomplete","Start a fresh scan. Previous calibration is archived and will not be reused.",false);return;}
  if(settings.GuidedPlacementId!=null){SceneStatus("Guided placement · empty-room scan incomplete","Collect person points to measure recovery coverage. Start a new setup whenever a mount moves.",false);return;}
  SceneStatus(settings.SceneHome==null?"Ready · room scene setup":"Scene reference saved",settings.SceneHome==null?"Start the spoken countdown, then leave both camera views.":"Confirm the mounts are unchanged, or scan again after moving either camera.",false);
 }
 void SceneStatus(string title,string detail,bool running){sceneStatus.Text=title+" · "+detail;if(sceneDialog is {IsDisposed:false})sceneDialog.Status(title,detail,running);}
 string? SceneProblem(bool collecting=false){try{RequireScene(collecting);return null;}catch(IOException e){return e.Message;}}
 void RequireScene(bool collecting=false)=>SceneScanPolicy.RequireScene(settings,sceneUnmoved.Checked,collecting);
 void CamerasMoved(){Cancel();settings.CameraPlacementChanged=true;sceneUnmoved.Checked=false;sceneUnmoved.Enabled=false;roomAlignment=null;try{PersistSettings();SceneStatus("Camera placement changed","Run scene setup again, then collect a new map. Saved points are retained.",sceneScanning);}catch(Exception e){SceneStatus("Could not save placement state",e.Message,sceneScanning);}}
 void BeginGuidedCalibration(){
  if(sceneScanning)return;
  calibrationDialog?.Close();
  Cancel("Preparing guided calibration");
  var oldScene=settings.SceneHome;var oldGuided=settings.GuidedPlacementId;var oldPending=settings.PendingSceneScanId;bool oldChanged=settings.CameraPlacementChanged;
  try{
   // This is a new placement identity, not a successful empty-room scan.
   // PrepareSceneMap archives the old person map before new capture begins.
   settings.SceneHome=null;settings.GuidedPlacementId=Guid.NewGuid().ToString("N");settings.PendingSceneScanId=null;settings.CameraPlacementChanged=false;PersistSettings();
  }catch(Exception e){settings.SceneHome=oldScene;settings.GuidedPlacementId=oldGuided;settings.PendingSceneScanId=oldPending;settings.CameraPlacementChanged=oldChanged;SceneStatus("Guided setup could not start",e.Message,false);return;}
  sceneUnmoved.Enabled=true;sceneUnmoved.Checked=true;roomAlignment=null;
  SceneStatus("Guided placement · empty-room scan incomplete","No background boundaries measured. Capture at least four spread person positions before testing recovery.",false);
  OpenCalibration();
 }
 void PrepareSceneMap(){
  RequireScene(true);var placement=SceneScanPolicy.PlacementId(settings)!;if(settings.RoomMapSceneId==placement)return;
  ArchiveRoomMap("New scene placement");
  var oldPoints=settings.RoomPoints;var oldPrevious=settings.PreviousRoomPoints;int oldVersion=settings.RoomMapVersion;var oldPlacement=settings.RoomMapSceneId;
  try{if(oldPoints.Length>0)settings.PreviousRoomPoints=oldPoints;settings.RoomPoints=[];settings.RoomMapVersion=1;settings.RoomMapSceneId=placement;PersistSettings();}
  catch{settings.RoomPoints=oldPoints;settings.PreviousRoomPoints=oldPrevious;settings.RoomMapVersion=oldVersion;settings.RoomMapSceneId=oldPlacement;throw;}
 }
 void ArchiveRoomMap(string reason){
  if(settings.RoomPoints.Length==0)return;
  Directory.CreateDirectory(DiagnosticsDirectory);File.WriteAllText(Path.Combine(DiagnosticsDirectory,$"room-map-archive-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json"),JsonSerializer.Serialize(new{reason,settings.RoomPoints,settings.RoomMapVersion,settings.RoomMapSceneId},Program.Json));
 }
 void ResetRoomMap(){
  Cancel("Room map reset");
  var previous=settings.RoomPoints;var previousArchive=settings.PreviousRoomPoints;
  try{ArchiveRoomMap("User reset");if(previous.Length>0)settings.PreviousRoomPoints=previous;settings.RoomPoints=[];PersistSettings();roomStatus.Text="Room map cleared · previous points archived locally";calibrationDialog?.SetPoints([]);calibrationDialog?.UpdatePoints(0);calibrationDialog?.SetMapReady(false);}
  catch(Exception e){settings.RoomPoints=previous;settings.PreviousRoomPoints=previousArchive;roomStatus.Text="Room map kept · "+e.Message;}
 }
 async void ScanRoomScene(int seconds,bool nativeDisabled){
  if(sceneScanning)return;
  if(!nativeDisabled){SceneStatus("Tracking must be off","Turn off the Link's built-in tracking, then check its box.",false);return;}
  nativeOff.Checked=true;sceneScanning=true;bool success=false,cancelled=false;string failure="Scan did not start.";var evidence=new List<object>();sceneEvidence=evidence;sceneCancelReason=null;var startedUtc=DateTime.UtcNow;var anchors=new List<SceneAnchor>();var verifiedTargets=new List<SceneTarget>();SceneTarget[] targets=[SceneTargetDiscovery.Centre];string? scanId=null;Pose? scanOrigin=null;
  await using var scanFeeds=new CameraFeeds(sceneDetail:true){AllowObsFallback=false};
  try{
   SetScenePhase("preparing");
   calibrationAudio.ValidateSceneAssets();calibrationDialog?.Close();PausePreviews();
   scanId=FreshSceneScan.Begin(settings,Path.Combine(CommunityProfile.DataDirectory,"preserved-maps"),PersistSettings);
   sceneUnmoved.Checked=false;sceneUnmoved.Enabled=false;mainRoomPreview.SetPoints([]);
   sceneDialog?.ResetForScan();sceneDialog?.Targets(targets);
   RecordSceneEvent(new{type="fresh-scan",scanId,previousMeasurementsReused=false});
   SceneStatus("Fresh scan · opening cameras","Previous calibration archived. Finding new features and target locations.",true);
   await feeds.StopCapture();
   await Manual(async ct=>{
    trackingMoving=true;sceneCancelReason=null;
    try{
     // Open video first: a closed Link may be in its parked pose. Keep capture
     // alive through the countdown and measure the origin only after wake-up.
     SetScenePhase("camera-warmup");
     var warmRoom=await scanFeeds.RoomFrame(ct);var warmLink=await scanFeeds.LinkFrame(ct);
     if(sceneDialog is {IsDisposed:false})sceneDialog.Previews(warmRoom.Bytes,warmLink.Bytes);
     await using var detector=new LocalBodySession(ct,wholeBody:true);
     await detector.Infer(warmRoom.Bytes,ct,.35);await detector.Infer(warmLink.Bytes,ct,.35);
     await Task.Delay(1500,ct);
     state=await engine.Execute(new(ReadNativeTracking:true),ct);
     if(state.NativeTrackingEnabled!=false)throw new IOException("Built-in camera tracking is active or unavailable; switch it off before scanning.");
     var origin=CurrentPose();
     RecordSceneEvent(new{type="ready",pose=origin,atUtc=DateTime.UtcNow});SetScenePhase("countdown");
     await calibrationAudio.Announce("scene-leave.wav",ct);
     for(int remaining=Math.Clamp(seconds,10,120);remaining>0;remaining--){var tick=Stopwatch.StartNew();SceneStatus($"Leave the room · {remaining} seconds","Wait outside until the spoken completion message. Stop movement cancels.",true);if(remaining is 30 or 20 or 10 or 5 or 4 or 3 or 2 or 1)await calibrationAudio.Announce($"count-{remaining}.wav",ct);var previewRoom=await scanFeeds.RoomFrame(ct);var previewLink=await scanFeeds.LinkFrame(ct);if(sceneDialog is {IsDisposed:false})sceneDialog.Previews(previewRoom.Bytes,previewLink.Bytes);await Task.Delay(Math.Max(0,1000-(int)tick.ElapsedMilliseconds),ct);}
     await calibrationAudio.Announce("scene-start.wav",ct);
     SetScenePhase("registration");
     using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromMinutes(4));var token=deadline.Token;
     state=await engine.Execute(new(ReadNativeTracking:true),token);
     if(state.NativeTrackingEnabled!=false)throw new IOException("Built-in tracking turned on during the countdown; scene scan stopped.");
     origin=CurrentPose();returnPose=origin;scanOrigin=origin;
     RecordSceneEvent(new{type="scan-origin",pose=origin,atUtc=DateTime.UtcNow});
     // Only this generation's fresh frames and anchors may complete the scan.
     // Failure or cancellation leaves older calibration archived and inactive.
     try{
      DateTime after=DateTime.UtcNow;
      async Task ReturnToFreshCentre(){
       var beforeRoom=await scanFeeds.RoomFrame(token);var beforeLink=await scanFeeds.LinkFrame(token);
       var br=await detector.Infer(beforeRoom.Bytes,token,.35);var bl=await detector.Infer(beforeLink.Bytes,token,.35);
       SceneScanPolicy.Fresh(beforeRoom,DateTime.UtcNow,after);SceneScanPolicy.Fresh(beforeLink,DateTime.UtcNow,after);
       if(br.Error!=null||bl.Error!=null||br.People.Length!=0||bl.People.Length!=0)throw new IOException("Leave both views empty before continuing the fresh scan.");
       state=await engine.Execute(new(ReadNativeTracking:true),token);if(state.NativeTrackingEnabled!=false)throw new IOException("Built-in tracking changed; scan stopped.");
       SceneScanPolicy.Fresh(beforeRoom,DateTime.UtcNow,after);SceneScanPolicy.Fresh(beforeLink,DateTime.UtcNow,after);
       var home=anchors[0];var pose=new Pose(home.Pan,home.Tilt,home.Zoom);
       if(CurrentPose()!=pose){state=await engine.Execute(new(Target:pose,DurationMs:1000),token);after=DateTime.UtcNow;}
       await Task.Delay(350,token);var rf=await scanFeeds.RoomFrame(token);var lf=await scanFeeds.LinkFrame(token);
       var rr=await detector.Infer(rf.Bytes,token,.35);var lr=await detector.Infer(lf.Bytes,token,.35);
       SceneScanPolicy.Fresh(rf,DateTime.UtcNow,after);SceneScanPolicy.Fresh(lf,DateTime.UtcNow,after);
       var check=await Task.Run(()=>SceneRegistration.Match(rf.Bytes,lf.Bytes,rr,lr),token);
       if(Math.Abs(check.X-home.X)>.035||Math.Abs(check.Y-home.Y)>.035)throw new IOException("Fresh centre no longer aligns; scan stopped.");
       RecordSceneEvent(new{type="centre-reverified",scanId,check.X,check.Y,check.Inliers});
      }
      for(int targetIndex=0;targetIndex<targets.Length;targetIndex++){
       var (x,y,name)=targets[targetIndex];bool reached=false,limited=false;double previousError=double.MaxValue;int notImproving=0,matchMisses=0;SceneAnchor? lastSupported=null;string? skipped=null;
       // Each candidate starts from the freshly verified centre, preserving
       // overlap between measurements of distinct fixed features.
       if(targetIndex>0)await ReturnToFreshCentre();
       for(int attempt=0;attempt<35;attempt++){
        SceneStatus($"Scanning {name} · {anchors.Count} verified",limited?"Verifying a reachable boundary; this image edge remains unmeasured.":"Matching fixed room details; detected objects are excluded.",true);await Task.Delay(250,token);
        var room=await scanFeeds.RoomFrame(token);var link=await scanFeeds.LinkFrame(token);var r=await detector.Infer(room.Bytes,token,.35);var l=await detector.Infer(link.Bytes,token,.35);
        SceneScanPolicy.Fresh(room,DateTime.UtcNow,after);SceneScanPolicy.Fresh(link,DateTime.UtcNow,after);
        if(sceneDialog is {IsDisposed:false})sceneDialog.Frames(room.Bytes,link.Bytes,r,l,null,targetIndex);
        evidence.Add(new{type="objects",target=name,attempt,pose=CurrentPose(),roomSize=new{r.Width,r.Height},linkSize=new{l.Width,l.Height},roomPeople=r.People.Length,linkPeople=l.People.Length,roomObjects=r.Objects,linkObjects=l.Objects});
        // Matching runs off-thread; collect its numeric evidence there and append
        // on the UI thread so device notifications cannot race this list.
        var quality=new List<object>();SceneMatch match;
        try{match=await Task.Run(()=>SceneRegistration.Match(room.Bytes,link.Bytes,r,l,quality.Add),token);}
        catch(IOException e){
         RecordSceneEvent(new{type="alignment-held",target=name,attempt,reason=e.Message});
         if(++matchMisses>=4){
          if(r.People.Length!=0||l.People.Length!=0||targetIndex==0)throw;
          if(limited||lastSupported==null){skipped=e.Message;break;}
          // Feature loss defines a possible overlap boundary, not a saved point.
          // Revisit the last supported pose only while capture and controls are
          // healthy, then demand two fresh optical checks before accepting it.
          var expected=CurrentPose();state=await engine.Execute(new(ReadNativeTracking:true),token);
          if(state.NativeTrackingEnabled!=false||CurrentPose()!=expected)throw new IOException("Camera control changed while locating the scene boundary.");
          SceneScanPolicy.Fresh(room,DateTime.UtcNow,after);SceneScanPolicy.Fresh(link,DateTime.UtcNow,after);
          RecordSceneEvent(new{type="boundary-limit",target=name,requestedX=x,requestedY=y,candidateX=lastSupported.X,candidateY=lastSupported.Y,reason=e.Message});
          state=await engine.Execute(new(Target:new(lastSupported.Pan,lastSupported.Tilt,lastSupported.Zoom),DurationMs:1000),token);after=DateTime.UtcNow;
          x=lastSupported.X;y=lastSupported.Y;limited=true;matchMisses=0;notImproving=0;previousError=double.MaxValue;continue;
         }
         SceneStatus($"Checking {name} again · camera held",e.Message,true);await Task.Delay(600,token);continue;
        }
        finally{foreach(var item in quality)RecordSceneEvent(item);}
        matchMisses=0;
        if(sceneDialog is {IsDisposed:false})sceneDialog.Frames(room.Bytes,link.Bytes,r,l,match,targetIndex);
        evidence.Add(new{type="alignment",target=name,attempt,pose=CurrentPose(),match.X,match.Y,match.Inliers,match.InlierRatio});
        double ex=x-match.X,ey=y-match.Y,error=Math.Sqrt(ex*ex+ey*ey);
        if(Math.Abs(ex)<.065&&Math.Abs(ey)<.065){
         await Task.Delay(350,token);var room2=await scanFeeds.RoomFrame(token);var link2=await scanFeeds.LinkFrame(token);var r2=await detector.Infer(room2.Bytes,token,.35);var l2=await detector.Infer(link2.Bytes,token,.35);
         SceneScanPolicy.Fresh(room2,DateTime.UtcNow,room.ReadAtUtc!.Value);SceneScanPolicy.Fresh(link2,DateTime.UtcNow,link.ReadAtUtc!.Value);
         var check=await Task.Run(()=>{SceneRegistration.Stable(room.Bytes,room2.Bytes,r,r2);SceneRegistration.Stable(link.Bytes,link2.Bytes,l,l2);return SceneRegistration.Match(room2.Bytes,link2.Bytes,r2,l2);},token);
         if(Math.Abs(check.X-match.X)>.025||Math.Abs(check.Y-match.Y)>.025||Math.Abs(x-check.X)>=.065||Math.Abs(y-check.Y)>=.065)throw new IOException("Scene alignment is unstable. Clear the room and retry.");
         state=await engine.Execute(new(),token);var pose=CurrentPose();anchors.Add(new(check.X,check.Y,pose.Pan,pose.Tilt,pose.Zoom,check.Inliers,check.InlierRatio,r2.Objects,l2.Objects,limited));verifiedTargets.Add(targets[targetIndex]);RecordSceneEvent(new{type="anchor-verified",scanId,target=name,anchor=anchors[^1]});sceneDialog?.Anchors(anchors);if(targetIndex==0){targets=SceneTargetDiscovery.Discover(check,RecordSceneEvent);RecordSceneEvent(new{type="fresh-target-plan",scanId,targets});sceneDialog?.Targets(targets);}reached=true;break;
        }
        if(!limited&&targetIndex>0&&SceneScanPolicy.BoundaryCandidate(match,targetIndex,anchors[0],targets)){var pose=CurrentPose();lastSupported=new(match.X,match.Y,pose.Pan,pose.Tilt,pose.Zoom,match.Inliers,match.InlierRatio,BoundaryLimited:true);}
        if(error>=previousError-.002)notImproving++;else notImproving=0;previousError=error;if(notImproving>=4)throw new IOException($"Cannot reach {name} reliably. Check overlap and camera direction settings.");
        state=await engine.Execute(new(),token);var next=SceneScanPolicy.Next(match,x,y,state,origin,reversePan.Checked,reverseTilt.Checked);
        SceneScanPolicy.Fresh(room,DateTime.UtcNow,after);SceneScanPolicy.Fresh(link,DateTime.UtcNow,after);
        state=await engine.Execute(new(Target:next,DurationMs:180),token);after=DateTime.UtcNow;
       }
       if(!reached){if(targetIndex==0)throw new IOException("Fresh centre could not be established.");RecordSceneEvent(new{type="target-skipped",scanId,target=name,reason=skipped??"Target timed out; not included in map"});}
      }
      SceneScanPolicy.ValidateAnchors(anchors,verifiedTargets);SetScenePhase("return-to-centre");await ReturnToFreshCentre();
      var homeMovedAt=DateTime.UtcNow;await Task.Delay(350,token);var homeRoom=await scanFeeds.RoomFrame(token);var homeLink=await scanFeeds.LinkFrame(token);var hr=await detector.Infer(homeRoom.Bytes,token,.35);var hl=await detector.Infer(homeLink.Bytes,token,.35);
      SceneScanPolicy.Fresh(homeRoom,DateTime.UtcNow,homeMovedAt);SceneScanPolicy.Fresh(homeLink,DateTime.UtcNow,homeMovedAt);
      var homeMatch=await Task.Run(()=>SceneRegistration.Match(homeRoom.Bytes,homeLink.Bytes,hr,hl),token);if(Math.Abs(homeMatch.X-.5)>=.065||Math.Abs(homeMatch.Y-.5)>=.065)throw new IOException("Return to centre did not align optically; scene was not saved.");
      evidence.Add(new{type="home-verification",homeMatch.X,homeMatch.Y,homeMatch.Inliers,homeMatch.InlierRatio});if(sceneDialog is {IsDisposed:false})sceneDialog.Frames(homeRoom.Bytes,homeLink.Bytes,hr,hl,homeMatch,0);
      FreshSceneScan.Complete(settings,scanId!,anchors.ToArray(),verifiedTargets.ToArray(),PersistSettings);
      sceneUnmoved.Checked=true;sceneUnmoved.Enabled=true;
      roomAlignment=null;success=true;
     }catch(Exception e){failure=e.Message;throw;}
     // A fatal failure stops at the current pose. Previous framing is an explicit manual
     // action; never issue a recovery move after an error or disconnect.
    }catch(OperationCanceledException){cancelled=ct.IsCancellationRequested;failure=cancelled?"Stopped during "+scenePhase+" · "+(sceneCancelReason??"camera action cancelled"):"Timed out during "+scenePhase;throw;}
    catch(Exception e){failure=e.Message;throw;}
    finally{trackingMoving=false;}
   });
   if(success){SceneStatus("Room scan complete · you may return",anchors.Any(a=>a.BoundaryLimited)?$"{anchors.Count} fresh points verified in the shared view. Some limits remain unmeasured; collect new person points next.":$"{anchors.Count} fresh measured scene points saved. Collect new person points next.",false);await calibrationAudio.Announce("scene-complete.wav",lifetime.Token);}
   else{if(!cancelled&&scanOrigin!=null)returnPose=scanOrigin;SceneStatus(cancelled?"Scan cancelled · you may return":"Scan not completed · you may return",$"{anchors.Count} verified · "+failure,false);await calibrationAudio.Announce(cancelled?"scene-cancelled.wav":"scene-failed.wav",lifetime.Token);}
  }catch(Exception e){failure=e.Message;SceneStatus(success?"Scan complete · announcement unavailable":"Scene setup stopped",failure,false);}
  finally{
   try{await scanFeeds.StopCapture();Directory.CreateDirectory(DiagnosticsDirectory);lastSceneReport=Path.Combine(DiagnosticsDirectory,$"scene-scan-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");File.WriteAllText(lastSceneReport,JsonSerializer.Serialize(new{scanId,targets,previousMeasurementsReused=false,startedUtc,finishedUtc=DateTime.UtcNow,phase=scenePhase,success,cancelled,reason=success?"complete":failure,reference=success?settings.SceneHome:null,verifiedAnchors=anchors,observations=evidence},Program.Json));foreach(var old in Directory.GetFiles(DiagnosticsDirectory,"scene-scan-*.json").OrderByDescending(File.GetLastWriteTimeUtc).Skip(10))File.Delete(old);}catch(Exception e){sceneStatus.Text+=" · Report could not be saved: "+e.Message;}
   sceneEvidence=null;scenePhase="idle";sceneScanning=false;
  }
 }
 void ShowSceneReport(){
  var path=lastSceneReport??(Directory.Exists(DiagnosticsDirectory)?Directory.GetFiles(DiagnosticsDirectory,"scene-scan-*.json").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault():null);
  if(path==null){SceneStatus("No scene report yet","Run a scene scan first.",false);return;}
  var report=new Form(){Text="LINK360 · Local scene inventory and scan report",Size=new Size(850,650),BackColor=SuiteTheme.Background};report.Controls.Add(new TextBox(){Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,Font=new Font("Consolas",10),Text=File.ReadAllText(path),BackColor=SuiteTheme.Background,ForeColor=SuiteTheme.Text});report.Show(this);
 }
}
