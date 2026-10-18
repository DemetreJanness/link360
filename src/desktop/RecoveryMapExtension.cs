namespace Link360Desktop;
// Explicit same-placement extension. Existing pairs and archived maps are never
// edited; only a newly centred, independently measured subject pair is appended.
internal static class RecoveryMapExtension {
 public static RoomPoint[] Append(RoomPoint[] current,RoomPoint point){
  var map=RoomMap.Fit(current);
  if(current.Length>=25)throw new IOException("Room map already has 25 points.");
  if(!double.IsFinite(point.X+point.Y)||point.X<0||point.X>1||point.Y<0||point.Y>1)throw new IOException("Invalid measured room position.");
  if(current.Any(p=>Math.Pow(p.X-point.X,2)+Math.Pow(p.Y-point.Y,2)<.04*.04))throw new IOException("Already captured nearby; move farther into the missing area.");
  var next=current.Append(point).ToArray();RoomMap.Fit(next);return next;
 }
 public static void Save(Settings settings,RoomPoint point,Action persist){
  if(settings.CameraPlacementChanged||settings.PendingSceneScanId!=null||string.IsNullOrWhiteSpace(settings.RoomMapSceneId)||settings.RoomMapSceneId!=SceneScanPolicy.PlacementId(settings))throw new IOException("Confirm unchanged camera mounts before extending coverage.");
  var prior=settings.RoomPoints;int version=settings.RoomMapVersion;
  var next=Append(prior,point);
  try{settings.RoomPoints=next;settings.RoomMapVersion=3;persist();}
  catch{settings.RoomPoints=prior;settings.RoomMapVersion=version;throw;}
 }
}
