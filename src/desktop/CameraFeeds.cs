namespace Link360Desktop;
// Direct capture is the default. Existing OBS captures can be reused locally
// when a driver refuses a second client; no scenes or source settings change.
internal sealed class CameraFeeds : IAsyncDisposable {
 readonly DirectFrames room,link;
 public CameraFeeds(bool sceneDetail=false){room=new("room",sceneDetail);link=new("link",sceneDetail);}
 readonly OverviewFrames obsRoom=new();readonly ObsVideoRoute obsLink=new(new LocalObsRpc());
 bool roomFallback,linkFallback;
 public bool AllowObsFallback {get;set;}=true;
 public string RoomRoute {get;private set;}="Room camera waiting";
 public string LinkRoute {get;private set;}="Link camera waiting";
 public void RetryDirect(){roomFallback=false;linkFallback=false;}
 public async Task StopCapture(){await Task.WhenAll(room.DisposeAsync().AsTask(),link.DisposeAsync().AsTask());}
 public async Task<byte[]> Room(CancellationToken ct)=>(await RoomFrame(ct)).Bytes;
 public async Task<FeedFrame> RoomFrame(CancellationToken ct) {
  if(!roomFallback){try{var frame=await room.Read(ct);RoomRoute="Room · direct camera";return frame;}catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}catch(Exception e){if(!AllowObsFallback||System.Diagnostics.Process.GetProcessesByName("obs64").Length==0)throw new IOException("Acer capture failed · "+e.Message,e);roomFallback=true;}}
  try{var frame=await obsRoom.Read(ct);RoomRoute="Room · local OBS fallback";return new(frame,null,DateTime.UtcNow);}catch(Exception e){throw new IOException("Room camera unavailable. Close other camera clients and restart preview, or enable its OBS capture. "+e.Message,e);}
 }
 public async Task<byte[]> Link(CancellationToken ct)=>(await LinkFrame(ct)).Bytes;
 public async Task<FeedFrame> LinkFrame(CancellationToken ct) {
  if(!linkFallback){try{var frame=await link.Read(ct);LinkRoute="Link · direct camera";return frame;}catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}catch(Exception e){if(!AllowObsFallback)throw new IOException("Link direct capture: "+e.Message,e);linkFallback=true;}}
  try{var frame=await obsLink.TrackingFrame(ct,true);LinkRoute="Link · local OBS fallback";return new(frame,null,DateTime.UtcNow);}catch(Exception e){throw new IOException("Link preview unavailable. Close other camera clients and restart preview, or enable its OBS capture. "+e.Message,e);}
 }
 public async ValueTask DisposeAsync(){await room.DisposeAsync();await link.DisposeAsync();await obsRoom.DisposeAsync();await obsLink.DisposeAsync();}
}
