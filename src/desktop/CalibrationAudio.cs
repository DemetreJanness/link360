using System.Media;
namespace Link360Desktop;
// Fixed, locally generated speech assets. No text or camera data leaves the app.
internal sealed class CalibrationAudio : IDisposable {
 readonly SemaphoreSlim queue=new(1);readonly CancellationTokenSource lifetime=new();SoundPlayer? current;
 public bool Enabled=true;
 public void Saved(bool complete)=>Play("captured-tracking-point.wav",complete?"calibration-done.wav":"next-calibration-point.wav");
 public void Test()=>Play("captured-tracking-point.wav");
 public void CoverageComplete()=>Play("captured-tracking-point.wav","calibration-done.wav","tracking-settle.wav");
 public void Complete()=>Play("calibration-done.wav");
 public void ValidateSceneAssets(){if(!Enabled)throw new IOException("Enable British voice calibration prompts in Settings before room setup.");foreach(var name in new[]{"scene-leave","scene-start","scene-complete","scene-failed","scene-cancelled","count-30","count-20","count-10","count-5","count-4","count-3","count-2","count-1"}){var path=Path.Combine(AppContext.BaseDirectory,"audio",name+".wav");if(!File.Exists(path)||new FileInfo(path).Length<44)throw new IOException("Missing local countdown voice file: "+name);}}
 public async Task Announce(string file,CancellationToken ct){
  if(!Enabled)throw new IOException("Enable calibration voice prompts for the room countdown.");
  using var both=CancellationTokenSource.CreateLinkedTokenSource(ct,lifetime.Token);var token=both.Token;
  await queue.WaitAsync(token);try{await Task.Run(()=>{using var player=new SoundPlayer(Path.Combine(AppContext.BaseDirectory,"audio",file));try{current=player;using var stop=token.Register(player.Stop);token.ThrowIfCancellationRequested();player.Load();player.PlaySync();token.ThrowIfCancellationRequested();}finally{current=null;}},token);}finally{queue.Release();}
 }
 void Play(params string[] files){if(!Enabled)return;_ = Task.Run(async()=>{try{await queue.WaitAsync(lifetime.Token);try{foreach(var file in files){if(lifetime.IsCancellationRequested)return;using var player=new SoundPlayer(Path.Combine(AppContext.BaseDirectory,"audio",file));current=player;player.Load();player.PlaySync();current=null;}}finally{queue.Release();}}catch(OperationCanceledException){}catch{System.Media.SystemSounds.Asterisk.Play();}});}
 public void Dispose(){lifetime.Cancel();current?.Stop();}
}
