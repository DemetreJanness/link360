using Link360;
namespace Link360Desktop;

// DBT_DEVNODES_CHANGED describes the whole Windows device tree, not a camera
// disconnect. Compare device paths, never enumeration order or unrelated devices.
internal static class CameraInventory {
 public static readonly string[] Names=["Insta360 Link","Acer FHD User Facing"];
 public static string? Changed(IReadOnlyList<VideoDevice> previous,IReadOnlyList<VideoDevice> current,bool roomRequired){
  foreach(var name in Names.Take(roomRequired?2:1)){
   var before=previous.Where(d=>d.Name.Equals(name,StringComparison.OrdinalIgnoreCase)).Select(d=>d.Path).Order(StringComparer.OrdinalIgnoreCase).ToArray();
   var after=current.Where(d=>d.Name.Equals(name,StringComparison.OrdinalIgnoreCase)).Select(d=>d.Path).Order(StringComparer.OrdinalIgnoreCase).ToArray();
   if(!before.SequenceEqual(after,StringComparer.OrdinalIgnoreCase))return name;
  }
  return null;
 }
}

internal sealed partial class ControllerForm {
 List<VideoDevice>? knownCameras;int inventoryRevision;
 async Task InitializeCameraInventory(){
  try{knownCameras=await Task.Run(CameraProbe.VideoDevices);}catch{knownCameras=null;}
 }
 async void CheckCameraInventory(){
  int revision=++inventoryRevision;
  try{
   await Task.Delay(150,lifetime.Token);
   if(revision!=inventoryRevision)return;
   var current=await Task.Run(CameraProbe.VideoDevices,lifetime.Token);
   if(!started||revision!=inventoryRevision)return;
   bool roomRequired=sceneScanning||overview!=null||tracking!=null&&roomMode.Checked;
   string? changed=knownCameras==null?null:CameraInventory.Changed(knownCameras,current,roomRequired);
   knownCameras=current;
   RecordSceneEvent(new{type="device-notification",phase=scenePhase,atUtc=DateTime.UtcNow,cameraChanged=changed,linkCount=current.Count(d=>d.Name==CameraInventory.Names[0]),roomCount=current.Count(d=>d.Name==CameraInventory.Names[1])});
   if(changed==null)return;
   Cancel(changed+" connection changed");returnPose=null;cameraAvailable=false;videoRoute.DeviceChanged();refreshTimer.Interval=1000;status.Text=changed+" connection changed · waiting for fresh camera state";
  }catch(OperationCanceledException)when(lifetime.IsCancellationRequested){}
  catch(Exception e){
   if(!started)return;
   // An inventory error cannot establish that the active cameras are unchanged.
   Cancel("Could not verify camera connections: "+e.Message);
   cameraAvailable=false;refreshTimer.Interval=1000;
  }
 }
}
