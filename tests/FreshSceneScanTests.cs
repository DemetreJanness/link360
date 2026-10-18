using System.Drawing;
using System.Text.Json;
using Link360Desktop;
internal static class FreshSceneScanTests {
 static void Check(bool condition,string reason){if(!condition)throw new Exception(reason);}
 static void Reject(Action action,string reason){try{action();}catch(IOException){return;}throw new Exception(reason);}
 static PointF[] Cloud(double rx,double ry){
  var points=new List<PointF>();
  for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int j=-2;j<=2;j++)for(int k=-2;k<=2;k++)
   points.Add(new((float)(.5+x*rx+j*.017),(float)(.5+y*ry+k*.017)));
  return points.ToArray();
 }
 public static void Run(){
  var match=new SceneMatch(.5,.5,200,.95,Cloud(.26,.24));
  var plan=SceneTargetDiscovery.Discover(match);
  Check(plan.Length>=4&&plan.Length<=25,"Adaptive target count outside limits");
  var changed=SceneTargetDiscovery.Discover(match with{RoomFeatures=Cloud(.20,.19)});
  Check(!plan.SequenceEqual(changed),"Changed feature locations reused a previous target plan");
  var unchanged=SceneTargetDiscovery.Discover(match);Check(plan.SequenceEqual(unchanged),"Unchanged scene was randomized rather than freshly measured");
  var copy=match.RoomFeatures!.ToArray();plan[1]=plan[1] with{X=.01};Check(match.RoomFeatures!.SequenceEqual(copy),"Plan mutated measured features");plan=SceneTargetDiscovery.Discover(match);
  Reject(()=>SceneTargetDiscovery.Discover(match with{RoomFeatures=[]}),"Missing current features reused a plan");
  Reject(()=>SceneTargetDiscovery.Discover(match with{RoomFeatures=Cloud(.06,.06)}),"Tiny support became a clean map");
  Reject(()=>SceneTargetDiscovery.Discover(match with{RoomFeatures=Cloud(.26,.24).Where(p=>p.X<.5).ToArray()}),"One-sided features became a surrounding map");
  Reject(()=>SceneTargetDiscovery.Discover(match with{InlierRatio=.2}),"Ambiguous current match generated targets");
  // A nearby cluster can be the only upper-left support. Dropping it merely
  // because it is close to home makes a usable triangle look one-sided.
  var nearSupport=new List<PointF>();
  foreach(var c in new[]{new PointF(.42f,.42f),new PointF(.75f,.42f),new PointF(.5f,.79f)})
   for(int x=-2;x<=2;x++)for(int y=-2;y<=2;y++)nearSupport.Add(new(c.X+x*.012f,c.Y+y*.012f));
  var nearbyPlan=SceneTargetDiscovery.Discover(match with{RoomFeatures=nearSupport.ToArray()});
  Check(nearbyPlan.Length>=4,"Nearby supported features were discarded before coverage could be measured");
  var home=new SceneAnchor(.5,.5,0,0,100,50,.9);
  var measured=plan.Select((p,i)=>new SceneAnchor(p.X,p.Y,i,i,100,40,.9)).ToArray();SceneScanPolicy.ValidateAnchors(measured,plan);
  Reject(()=>SceneScanPolicy.ValidateAnchors(measured[..3],plan[..3]),"Three measurements became a complete map");
  // Four fresh measurements may be sufficient if they enclose usable coverage.
  SceneTarget[] triangle=[SceneTargetDiscovery.Centre,new(.25,.25,"a"),new(.8,.3,"b"),new(.5,.8,"c")];
  SceneScanPolicy.ValidateAnchors(triangle.Select(p=>new SceneAnchor(p.X,p.Y,0,0,100,40,.9)).ToArray(),triangle);
  Reject(()=>SceneTargetDiscovery.Validate([..triangle,triangle[1]],home),"Duplicate discovery target accepted");
  Reject(()=>SceneTargetDiscovery.Validate([..triangle[..3],triangle[3] with{X=double.NaN}],home),"Nonfinite discovery target accepted");
  // Partial room coverage is useful: measured points need not surround home.
  SceneTarget[] patch=[SceneTargetDiscovery.Centre,new(.2,.8,"a"),new(.55,.8,"b"),new(.75,.45,"c")];
  var patchAnchors=patch.Select(p=>new SceneAnchor(p.X,p.Y,0,0,100,40,.9)).ToArray();
  SceneScanPolicy.ValidateAnchors(patchAnchors,patch);
  Reject(()=>SceneScanPolicy.RecoveryPoints(patchAnchors.Select(a=>a with{Zoom=200}).ToArray()),"Mixed framing became a wide recovery map");
  Check(SceneScanPolicy.CanFinish(patchAnchors,patch),"Four good partial-area points did not finish");
  Check(!SceneScanPolicy.CanFinish(patchAnchors[..3],patch[..3]),"Three points finished the scan");
  Check(!SceneScanPolicy.CanFinish(patchAnchors,patch[..3]),"Unmeasured target counted as saved");
  Check(!SceneScanPolicy.CanFinish([patchAnchors[0],patchAnchors[1],patchAnchors[1],patchAnchors[3]],patch),"Repeated measurement finished the scan");
  Check(!SceneScanPolicy.CanFinish(patchAnchors.Select(a=>a with{InlierRatio=.3}).ToArray(),patch),"Weak optical matches finished the scan");
  SceneTarget[] line=[SceneTargetDiscovery.Centre,new(.6,.6,"a"),new(.7,.7,"b"),new(.8,.8,"c")];
  Reject(()=>SceneScanPolicy.ValidateAnchors(line.Select(p=>new SceneAnchor(p.X,p.Y,0,0,100,40,.9)).ToArray(),line),"Collinear points became usable coverage");
  var oldPoints=Enumerable.Range(0,10).Select(i=>new RoomPoint(.2+i*.05,.3+i*.01,i,-i)).ToArray();
  var settings=new Settings{RoomPoints=oldPoints,RoomMapVersion=1,RoomMapSceneId="old",SceneHome=new("old",DateTime.UtcNow,measured,plan)};
  var root=Path.Combine(Path.GetTempPath(),"LINK360-fresh-scan-tests",Guid.NewGuid().ToString("N"));string? saved=null;
  void Persist()=>saved=JsonSerializer.Serialize(settings);
  var id=FreshSceneScan.Begin(settings,root,Persist);
  Check(settings.RoomPoints.Length==0&&settings.SceneHome==null&&settings.RoomMapSceneId==null&&settings.GuidedPlacementId==null,"Scan retained active old calibration");
  Check(settings.PendingSceneScanId==id&&settings.PreviousRoomPoints.SequenceEqual(oldPoints),"Scan lost old points or pending generation");
  using(var archive=JsonDocument.Parse(File.ReadAllText(Directory.GetFiles(root).Single()))){var prior=archive.RootElement.GetProperty("settings");Check(prior.GetProperty("RoomPoints").GetArrayLength()==10&&prior.GetProperty("SceneHome").GetProperty("Id").GetString()=="old","Archive did not preserve previous calibration");}
  Reject(()=>SceneScanPolicy.RequireScene(settings,true,false),"Failed/incomplete scan reused an old map");
  var loaded=JsonSerializer.Deserialize<Settings>(saved!)!;Reject(()=>SceneScanPolicy.RequireScene(loaded,true,false),"Restart recovered stale calibration after an incomplete scan");
  var second=FreshSceneScan.Begin(settings,root,Persist);Check(second!=id&&settings.PreviousRoomPoints.SequenceEqual(oldPoints),"Retry reused scan ID or erased old points");
  Reject(()=>FreshSceneScan.Complete(settings,id,measured,plan,Persist),"Stale scan completion replaced current run");
  Reject(()=>FreshSceneScan.Complete(settings,second,measured[..3],plan[..3],Persist),"Partial scan published a map");
  string snapshot=JsonSerializer.Serialize(settings);
  Reject(()=>FreshSceneScan.Complete(settings,second,measured,plan,()=>throw new IOException("fixture write failure")),"Failed save reported success");
  Check(JsonSerializer.Serialize(settings)==snapshot,"Failed completion changed pending state");
  FreshSceneScan.Complete(settings,second,measured,plan,Persist);
  Check(settings.SceneHome?.Id==second&&settings.PendingSceneScanId==null&&settings.RoomPoints.SequenceEqual(measured.Select(a=>new RoomPoint(a.X,a.Y,a.Pan,a.Tilt)))&&settings.RoomMapVersion==2&&settings.RoomMapSceneId==second,"Fresh scan did not publish exactly its measured recovery pairs");
  SceneScanPolicy.RequireScene(settings,true,true);SceneScanPolicy.RequireScene(settings,true,false);
  var roundTrip=JsonSerializer.Deserialize<Settings>(saved!)!;SceneScanPolicy.RequireScene(roundTrip,true,false);
  Check(roundTrip.RoomMapVersion==2&&roundTrip.RoomPoints.SequenceEqual(settings.RoomPoints),"Recovery source or pairs lost on restart");
  var scanMap=RoomMap.Fit(roundTrip.RoomPoints);var stateForMap=new Link360.CameraReading{Controls=[new(){Name="Pan",Min=-145,Max=145,Step=1,Capabilities=2},new(){Name="Tilt",Min=-90,Max=100,Step=1,Capabilities=2},new(){Name="Zoom",Min=100,Max=400,Step=1,Capabilities=2,Value=100}]};
  Check(scanMap.Target(measured[1].X,measured[1].Y,stateForMap,false)?.Pan==measured[1].Pan,"Scene recovery changed measured direction");
  Check(scanMap.Target(0,0,stateForMap,false)==null,"Partial scene map extrapolated into an unmeasured area");
  Check(settings.PreviousRoomPoints.SequenceEqual(oldPoints),"Successful fresh scan changed archived old points");
  snapshot=JsonSerializer.Serialize(settings);
  Reject(()=>FreshSceneScan.Begin(settings,root,()=>throw new IOException("fixture write failure")),"Failed scan start reported success");
  Check(JsonSerializer.Serialize(settings)==snapshot,"Failed scan start did not restore settings");
  var blocked=Path.Combine(root,"not-a-directory");File.WriteAllText(blocked,"fixture");Reject(()=>FreshSceneScan.Begin(settings,blocked,Persist),"Scan started without an archive");
  Check(JsonSerializer.Serialize(settings)==snapshot,"Archive failure changed settings");
  Console.WriteLine($"PASS: fresh per-scan discovery ({plan.Length} candidates), adaptive coverage, no stale reuse, archives, restart/cancel gates and write-failure rollback");
 }
}