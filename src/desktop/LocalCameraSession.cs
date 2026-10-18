using System.Diagnostics;
using System.Text.Json;
using Link360;
namespace Link360Desktop;

// Reuses a worker only while tracking is enabled. CameraEngine still serializes
// every request; manual override cancels this process before a fresh manual read.
internal sealed class LocalCameraSession : IAsyncDisposable {
 readonly Process process;
 readonly CancellationTokenRegistration stop;
 readonly bool nativeSpeed;
 public LocalCameraSession(CancellationToken ct,string? executable=null,bool continuous=false,bool limitedTravel=false,bool nativeSpeed=false) {
  this.nativeSpeed=nativeSpeed;
  process=Process.Start(new ProcessStartInfo(executable??Environment.ProcessPath!,(nativeSpeed?"--speed-camera-session":continuous?"--continuous-camera-session":"--camera-session")+(limitedTravel?" --limited-tracking":"")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true})??throw new IOException("Cannot start tracking camera worker.");
  stop=ct.Register(()=>{if(nativeSpeed){try{process.StandardInput.Close();}catch{}}else Kill();});
 }
 void Kill(){try{if(!process.HasExited)process.Kill(!nativeSpeed);}catch(InvalidOperationException){}}
 public async Task<CameraReading> Execute(WorkerRequest request,CancellationToken ct) {
  using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(3000);
  try {
   await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request).AsMemory(),deadline.Token).ConfigureAwait(false);
   var line=await process.StandardOutput.ReadLineAsync(deadline.Token).ConfigureAwait(false)??throw new IOException("Tracking camera worker exited.");
   ct.ThrowIfCancellationRequested();
   var result=JsonSerializer.Deserialize<CameraReading>(line,Program.Json)??throw new IOException("Invalid tracking camera response.");
   if(result.Error!=null)throw new CameraWorkerException(result.Error,result.ErrorKind??"command_failed");CameraSnapshotGuard.Validate(result,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());return result;
  }catch{if(nativeSpeed)await StopSpeed();else Kill();if(ct.IsCancellationRequested)throw new OperationCanceledException(ct);throw;}
 }
 async Task StopSpeed(){try{process.StandardInput.Close();}catch{}try{await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);}catch(TimeoutException){Kill();await process.WaitForExitAsync().ConfigureAwait(false);throw new IOException("Speed worker stalled; independent neutral supervisor takes over");}}
 public async ValueTask DisposeAsync(){stop.Dispose();try{if(nativeSpeed)await StopSpeed();else{Kill();await process.WaitForExitAsync().ConfigureAwait(false);}}finally{process.Dispose();}}
}
internal static class CameraSnapshotGuard {
 public static void Validate(CameraReading reading,long now){if(reading.SnapshotUtcMilliseconds>0&&now-reading.SnapshotUtcMilliseconds>500)throw new CameraWorkerException("Tracking driver state is stale; refresh the camera before enabling again.","camera_unavailable");}
}
