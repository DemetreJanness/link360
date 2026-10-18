using System.Diagnostics;
using System.Text.Json;
namespace Link360Desktop;

// Explicit, bounded local diagnostics session. Never accepts images, device
// identifiers, embeddings, credentials or arbitrary application state.
internal record RoomTestSample(long ElapsedMs,string Outcome,int RoomPeople,double? Confidence,double? RoomX,double? RoomY,double RoomAgeMs,long InferenceMs,int LinkPeople,double? LinkErrorX,double? LinkErrorY,double? LinkAgeMs,Pose Before,Pose? Requested,Pose? After,double? CommandMs,double? SpeedScale=null,int? RelativePan=null,int? RelativeTilt=null,Pose? ResolvedTarget=null,Pose? Commanded=null,string? Anchor=null,double? SnapshotAgeMs=null,bool ForcedAcer=false,RoomAlignment? RecoveryAlignment=null);
internal sealed class RoomMapTest : IDisposable {
 readonly Stopwatch clock=Stopwatch.StartNew();readonly StreamWriter log;
 int samples,moves,holds,missing,outside;double squaredError;int errors;long lastSample=-200;bool finished;
 long? lostAt;int reacquisitions;long longestGap;int staleRoom,staleLink;double minX=1,maxX=0,minY=1,maxY=0;
 public string LogPath {get;}
 public string? SummaryPath {get;private set;}
 readonly RoomTestVideo? video;public string? VideoPath{get;}
 public bool Expired=>clock.Elapsed>=TimeSpan.FromSeconds(60);
 public long ElapsedMs=>clock.ElapsedMilliseconds;
 public string CompletionMessage=>outside>0?"Test finished · some positions were outside the room map":staleRoom+staleLink>0?"Test finished · camera freshness needs attention":"Test finished · review movement and framing";
 public RoomMapTest(string directory,RoomPoint[] points,bool recordVideo=false,RoomAlignment? alignment=null) {
  RoomMap.Fit(points);Directory.CreateDirectory(directory);
  var old=Directory.GetFiles(directory,"room-test-*.jsonl").OrderByDescending(File.GetLastWriteTimeUtc).ToArray();
  foreach(var file in old.Skip(9)){File.Delete(file);var summary=Path.ChangeExtension(file,"summary.json");if(File.Exists(summary))File.Delete(summary);}
  LogPath=Path.Combine(directory,$"room-test-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..8]}.jsonl");
  if(recordVideo){foreach(var file in Directory.GetFiles(directory,"room-test-*.avi").OrderByDescending(File.GetLastWriteTimeUtc).Skip(2))File.Delete(file);VideoPath=Path.ChangeExtension(LogPath,".avi");video=new(VideoPath);}
  log=new StreamWriter(LogPath,false);Write(new{type="start",schema=3,atUtc=DateTime.UtcNow,durationSeconds=60,map=points,alignment,forcedAcerWindowMs=new[]{35000,40000},privacy="Numeric camera diagnostics only; no frames or identity data",timing="Direct frame ages start at camera Read return; OBS ages start at response receipt. Commanded/resolved poses are issued goals, not physical motor measurements."});
 }
 public void Capture(FeedFrame? room,FeedFrame? link,RoomTestSample sample)=>video?.Push(room,link,sample);
 void Write(object value){if(log.BaseStream.Position>2_000_000)throw new IOException("Local test log reached its size limit.");log.WriteLine(JsonSerializer.Serialize(value));log.Flush();}
 public void Record(RoomTestSample sample) {
  if(finished||(sample.Outcome!="move"&&sample.ElapsedMs-lastSample<200))return;lastSample=sample.ElapsedMs;samples++;
  if(sample.Outcome=="move")moves++;else holds++;
  if(sample.RoomPeople!=1)missing++;if(sample.Outcome=="outside-map")outside++;
  if(sample.RoomAgeMs>=600)staleRoom++;if(sample.LinkAgeMs==null||sample.LinkAgeMs>=600)staleLink++;
  bool visible=sample.RoomPeople==1&&sample.RoomAgeMs<600;
  if(!visible)lostAt??=sample.ElapsedMs;else if(lostAt is long start){long gap=sample.ElapsedMs-start;reacquisitions++;longestGap=Math.Max(longestGap,gap);Write(new{type="room-target-reacquired",elapsedMs=sample.ElapsedMs,gapMs=gap});lostAt=null;}
  if(visible&&sample.RoomX is double rx&&sample.RoomY is double ry){minX=Math.Min(minX,rx);maxX=Math.Max(maxX,rx);minY=Math.Min(minY,ry);maxY=Math.Max(maxY,ry);}
  if(sample.LinkPeople==1&&sample.LinkAgeMs<600&&sample.LinkErrorX is double x&&sample.LinkErrorY is double y){squaredError+=x*x+y*y;errors++;}
  Write(new{type="sample",data=sample});
 }
 public string Finish(string reason) {
  if(finished)return SummaryPath!;
  video?.Dispose();
  var summary=new{type="summary",atUtc=DateTime.UtcNow,reason,elapsedMs=clock.ElapsedMilliseconds,samples,movementRequests=moves,holds,roomMissingOrAmbiguous=missing,outsideMap=outside,staleRoomSamples=staleRoom,staleOrMissingLinkSamples=staleLink,roomTargetReacquisitions=reacquisitions,longestRoomTargetGapMs=longestGap,observedRoomXSpan=Math.Max(0,maxX-minX),observedRoomYSpan=Math.Max(0,maxY-minY),freshLinkErrorSamples=errors,linkCenterRmsNormalized=errors==0?(double?)null:Math.Sqrt(squaredError/errors),localVideo=VideoPath,localVideoError=video?.Error,assessment="Observations only; inspect coverage, lost-target behavior and physical motion before accepting the map"};
  Write(summary);if(video!=null)Write(new{type="local-video",path=VideoPath,error=video.Error,meaning="Latest paired preview frames, not synchronized exposure; overlay positions are issued/driver values, not motor encoders"});SummaryPath=Path.ChangeExtension(LogPath,"summary.json");File.WriteAllText(SummaryPath,JsonSerializer.Serialize(summary,Program.Json));finished=true;log.Dispose();return SummaryPath;
 }
 public void Dispose(){if(!finished)Finish("interrupted");log.Dispose();}
}
