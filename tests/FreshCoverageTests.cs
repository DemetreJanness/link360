using Link360;
using Link360Desktop;
internal static class FreshCoverageTests {
 static void Check(bool c,string text){if(!c)throw new Exception(text);}
 static CameraReading State(DateTime at,int pan=0,int tilt=0,int panSpeed=0)=>new(){SnapshotUtcMilliseconds=new DateTimeOffset(at).ToUnixTimeMilliseconds(),NativePanSpeed=panSpeed,NativeTiltSpeed=0,CommandedPan=pan,CommandedTilt=tilt,Controls=[new(){Name="Pan",Value=pan,Min=-145,Max=145,Step=1,Capabilities=2},new(){Name="Tilt",Value=tilt,Min=-90,Max=100,Step=1,Capabilities=2},new(){Name="Zoom",Value=100,Min=100,Max=400,Step=1,Capabilities=2}]};
 static BodyResult Room(double x=.5,double y=.5)=>new([new(x*640-80,y*360-60,160,120,x*640,y*360,.95)],640,360,"fixture",0);
 static BodyResult Link()=>new([new(80,10,160,160,160,90,.95)],320,180,"fixture",0,AnchorKind:"head");
 public static void Run(){
  var at=new DateTime(2026,10,18,0,0,0,DateTimeKind.Utc);var fresh=new FreshCoverageSession();Check(fresh.Points.Length==0&&fresh.Map==null,"Debug map reused old points");int captures=0;
  foreach(var point in new[]{new RoomPoint(.3,.4,-12,5),new(.7,.4,12,5),new(.3,.65,-12,-5),new(.7,.65,12,-5)}){
   for(int i=0;i<22;i++){at=at.AddMilliseconds(80);if(fresh.Observe(Room(point.X,point.Y),at,Link(),at,State(at,point.Pan,point.Tilt),at))captures++;}
  }
  Check(captures==4&&fresh.Points.Length==4&&fresh.Map!=null,"Fresh matched capture did not establish spread coverage");
  var saved=fresh.Points;string json=System.Text.Json.JsonSerializer.Serialize(new{fresh.Id,consumed=false,savedMapReused=false,points=saved,ready=true});var restored=FreshCoverageSession.Restore(fresh.Id,json);Check(restored.Id==fresh.Id&&restored.Points.SequenceEqual(saved)&&restored.Map!=null,"Paused untested generation could not resume");bool refused=false;try{FreshCoverageSession.Restore(fresh.Id,json.Replace("\"consumed\":false","\"consumed\":true"));}catch(IOException){refused=true;}Check(refused,"Consumed debug generation was resumed");fresh.Consumed=true;for(int i=0;i<20;i++){at=at.AddMilliseconds(100);fresh.Observe(Room(.5,.5),at,Link(),at,State(at),at);}Check(fresh.Points.SequenceEqual(saved),"Active test modified its calibration");
  Check(new FreshCoverageSession().Points.Length==0,"Next debug test inherited previous calibration");
  var invalid=new FreshCoverageSession();for(int i=0;i<30;i++){at=at.AddMilliseconds(80);invalid.Observe(Room(),at,Link(),at,State(at,panSpeed:1),at);}Check(invalid.Points.Length==0,"Moving motor produced a calibration point");
  for(int i=0;i<30;i++){at=at.AddMilliseconds(80);invalid.Observe(Room(),at.AddSeconds(-2),Link(),at,State(at),at);}Check(invalid.Points.Length==0,"Stale pair produced calibration");
  for(int i=0;i<30;i++){at=at.AddMilliseconds(80);invalid.Observe(Room(),at,Link() with{AnchorKind="missing"},at,State(at),at);}Check(invalid.Points.Length==0,"Acquisition box was promoted to a measured head reference");
  var duplicate=new FreshCoverageSession();for(int i=0;i<30;i++)duplicate.Observe(Room(),at,Link(),at,State(at),at.AddMilliseconds(i*5));Check(duplicate.Points.Length==0,"Duplicate frames created a point");
  foreach(bool right in new[]{false,true}){
   var policy=new HybridTrackingPlanner(fresh.Map,nativeSpeed:true);var missing=Link() with{AnchorKind="missing",People=[new(right?210:10,10,100,160,right?260:60,80,.95)]};TrackingDecision? d=null;
   for(int i=0;i<12;i++){var t=at.AddMilliseconds(i*60);d=policy.Evaluate(Room(right?.9:.1,.5),t,missing,t,State(t),false,false,t);}
   Check(d!.Source==GuidanceSource.Link&&d.NativePanSpeed.HasValue&&(right?d.NativePanSpeed>0:d.NativePanSpeed<0)&&d.NativeTiltSpeed==0,"Visible body outside map did not horizontally reacquire in both directions");
   var t2=at.AddSeconds(2);var ambiguous=Room() with{People=[Room().People[0],Room().People[0]]};d=policy.Evaluate(ambiguous,t2,missing,t2,State(t2),false,false,t2);Check(d.Hold,"Multiple room people allowed body acquisition");
  }
  var recovery=new HybridTrackingPlanner(fresh.Map,nativeSpeed:true);TrackingDecision? coarse=null;for(int i=0;i<8;i++){var t=at.AddMilliseconds(i*80);coarse=recovery.Evaluate(Room(.3,.4),t,null,t,State(t),false,false,t);}Check(coarse!.Source==GuidanceSource.Room&&coarse.Target?.Pan==-12&&coarse.Target.Tilt==5,"Fresh map did not drive Acer recovery to new measured pose");
  Check(!RoomMapTest.UseAcerAt(22999,false,true)&&RoomMapTest.UseAcerAt(23000,false,true)&&!RoomMapTest.UseAcerAt(28000,false,true)&&RoomMapTest.UseAcerAt(40000,false,true)&&!RoomMapTest.UseAcerAt(45000,false,true)&&!RoomMapTest.UseAcerAt(40000,true,true),"Narrated left/right takeover boundaries failed");
  Console.WriteLine("PASS: fresh paired calibration, no reuse, frozen test map, moving/stale/duplicate/uncertain rejection, bilateral body reacquisition, ambiguity hold and Acer recovery");
 }
}
