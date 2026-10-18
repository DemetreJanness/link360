using System.Text.Json;
using Link360;
namespace Link360Desktop;
internal sealed partial class ControllerForm {
 CoverageWizard? coverageWizard;FreshCoverageSession? freshCoverage;CancellationTokenSource? coveragePreparation;
 void StopFreshCoverage(){coveragePreparation?.Cancel();StopTracking("Fresh coverage stopped · movement off");if(freshCoverage!=null)coverageWizard?.State("Movement stopped",$"{freshCoverage.Points.Length} fresh points retained. Resume collection or run the first test.",0,freshCoverage.Map!=null&&!freshCoverage.Consumed);}
 void OpenFreshCoverage(){
  if(coverageWizard is {IsDisposed:false}){coverageWizard.Activate();return;}
  coverageWizard=new(StartFreshCoverageCalibration,StartFreshCoverageTest,StopFreshCoverage);coverageWizard.Show(this);if(freshCoverage!=null)UpdateFreshCoverage(freshCoverage);
  if(overview==null||linkPreview==null)RestartPreviews();
 }
 async Task StopPriorCoverage(CancellationToken ct){var prior=activeTrackingLoop;StopTracking("Preparing fresh coverage · movement off");if(prior!=null)await prior.WaitAsync(TimeSpan.FromSeconds(5),ct);}
 async void StartFreshCoverageCalibration(){
  if(coveragePreparation!=null)return;using var prep=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);coveragePreparation=prep;
  try{
   await StopPriorCoverage(prep.Token);if(busy)throw new IOException("Wait for camera controls to finish.");
   if(overview==null||linkPreview==null)RestartPreviews();
   var deadline=DateTime.UtcNow.AddSeconds(18);while((body==null||linkObservation==null)&&DateTime.UtcNow<deadline)await Task.Delay(100,prep.Token);
   if(overview==null||linkPreview==null||body==null||linkObservation==null)throw new IOException("Both camera previews must be live. Check the preview error.");
   var native=await engine.Execute(new(ReadNativeTracking:true),prep.Token);if(native.NativeTrackingEnabled!=false)throw new IOException("Turn built-in tracking off with the palm gesture first.");nativeOff.Checked=true;
   nativeSpeed.Checked=true;nativeSpeedUsed=true;state=await ReadControlState(prep.Token);prep.Token.ThrowIfCancellationRequested();
   if(freshCoverage==null||freshCoverage.Consumed)freshCoverage=new();roomAlignment=null;WriteFreshCoverage(freshCoverage);
   tracking=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);trackingToggle.Text="Pause tracking";
   activeTrackingLoop=TrackRoom(tracking,fresh:freshCoverage);_ = AnnounceFreshCoverage(tracking.Token);
  }catch(Exception e){coverageWizard?.State("Calibration did not start",e.Message,0,false);}finally{if(ReferenceEquals(coveragePreparation,prep))coveragePreparation=null;}
 }
 async Task AnnounceFreshCoverage(CancellationToken ct){try{await calibrationAudio.Announce("coverage-start.wav",ct);}catch(OperationCanceledException){}catch(Exception e){roomStatus.Text="Calibration voice unavailable · "+e.Message;}}
 void UpdateFreshCoverage(FreshCoverageSession fresh){if(!ReferenceEquals(freshCoverage,fresh))return;coverageWizard?.State($"Fresh calibration · {fresh.Points.Length} measured points",fresh.Status,fresh.Progress,fresh.Map!=null&&!fresh.Consumed);trackingStatus.Text=fresh.Status;}
 void FreshPointCaptured(FreshCoverageSession fresh){
  WriteFreshCoverage(fresh);coverageWizard?.Captured();System.Media.SystemSounds.Asterisk.Play();
  if(fresh.Points.Length>=25){StopTracking("Calibration complete · 25 fresh points");calibrationAudio.CoverageComplete();}else calibrationAudio.Saved(false);
 }
 void WriteFreshCoverage(FreshCoverageSession fresh){
  Directory.CreateDirectory(DiagnosticsDirectory);var path=Path.Combine(DiagnosticsDirectory,"fresh-coverage-"+fresh.Id+".local.json");
  File.WriteAllText(path,JsonSerializer.Serialize(new{schema=1,fresh.Id,createdFor="one debug calibration and test",savedMapReused=false,points=fresh.Points,ready=fresh.Map!=null,consumed=fresh.Consumed},Program.Json));
 }
 async void StartFreshCoverageTest(){
  if(coveragePreparation!=null)return;using var prep=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);coveragePreparation=prep;
  try{
   var fresh=freshCoverage??throw new IOException("Start fresh calibration first.");
   if(fresh.Consumed)throw new IOException("This debug map has already been tested. Start fresh calibration for the next test.");
   if(fresh.Map==null)throw new IOException("Collect at least four spread left/right and near/far points first.");
   await StopPriorCoverage(prep.Token);if(!ReferenceEquals(fresh,freshCoverage)||busy||state==null||overview==null||linkPreview==null)throw new IOException("Calibration or cameras changed. Start a fresh session.");
   prep.Token.ThrowIfCancellationRequested();fresh.Consumed=true;WriteFreshCoverage(fresh);
   roomTest=new RoomMapTest(DiagnosticsDirectory,fresh.Points,recordTestVideo.Checked,linkOnly:false,freshCoverageId:fresh.Id);testStopReason=null;
   tracking=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);trackingToggle.Text="Pause tracking";_ = CueTrackingTest(roomTest,tracking.Token);activeTrackingLoop=TrackRoom(tracking,roomTest,fresh);
  }catch(Exception e){coverageWizard?.State("Test did not start",e.Message,0,false);}finally{if(ReferenceEquals(coveragePreparation,prep))coveragePreparation=null;}
 }
}
