using System.Text.Json;
namespace Link360Desktop;

// Each accepted scan starts a new generation. Old measurements are archival,
// never input to discovery or an automatic fallback after failure/cancellation.
internal static class FreshSceneScan {
 public static string Begin(Settings settings,string archiveDirectory,Action persist){
  var id=Guid.NewGuid().ToString("N");
  var before=Snapshot.Take(settings);
  Directory.CreateDirectory(archiveDirectory);
  var path=Path.Combine(archiveDirectory,$"before-scene-scan-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{id}.json");
  using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
   JsonSerializer.Serialize(file,new{reason="Fresh scene scan",scanId=id,archivedUtc=DateTime.UtcNow,settings},Program.Json);
   file.Flush(true);
  }
  try{
   if(settings.RoomPoints.Length>0)settings.PreviousRoomPoints=settings.RoomPoints.ToArray();
   settings.RoomPoints=[];settings.RoomMapVersion=1;settings.RoomMapSceneId=null;
   settings.SceneHome=null;settings.GuidedPlacementId=null;settings.PendingSceneScanId=id;
   persist();return id;
  }catch{before.Restore(settings);throw;}
 }
 public static void Complete(Settings settings,string id,SceneAnchor[] anchors,SceneTarget[] targets,Action persist){
  if(settings.PendingSceneScanId!=id)throw new IOException("This scan is no longer current; no calibration saved.");
  SceneScanPolicy.ValidateAnchors(anchors,targets);var recovery=SceneScanPolicy.RecoveryPoints(anchors);
  var before=Snapshot.Take(settings);
  try{
   settings.SceneHome=new(id,DateTime.UtcNow,anchors.ToArray(),targets.ToArray());
   settings.RoomPoints=recovery;settings.RoomMapVersion=2;settings.RoomMapSceneId=id;
   settings.GuidedPlacementId=null;settings.PendingSceneScanId=null;settings.CameraPlacementChanged=false;
   persist();
  }catch{before.Restore(settings);throw;}
 }
 sealed record Snapshot(RoomPoint[] Points,RoomPoint[] Previous,int Version,string? MapId,
  SceneReference? Scene,string? Guided,string? Pending,bool Moved){
  public static Snapshot Take(Settings s)=>new(s.RoomPoints,s.PreviousRoomPoints,s.RoomMapVersion,s.RoomMapSceneId,s.SceneHome,s.GuidedPlacementId,s.PendingSceneScanId,s.CameraPlacementChanged);
  public void Restore(Settings s){s.RoomPoints=Points;s.PreviousRoomPoints=Previous;s.RoomMapVersion=Version;s.RoomMapSceneId=MapId;s.SceneHome=Scene;s.GuidedPlacementId=Guided;s.PendingSceneScanId=Pending;s.CameraPlacementChanged=Moved;}
 }
}