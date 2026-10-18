namespace Link360Desktop;
// A fresh, unique body must remain near its starting position for two seconds.
// After capture, require a new room position before another capture can fire.
internal sealed class StillnessCapture {
 DateTime? since;double x,y,savedX,savedY;bool waitingForMove;int samples;
 public double Progress(DateTime now)=>since==null?0:Math.Clamp((now-since.Value).TotalSeconds/2,0,1);
 public bool WaitingForMove=>waitingForMove;
 public bool Tick(bool valid,double cx,double cy,DateTime now) {
  if(!valid||!double.IsFinite(cx+cy)){since=null;samples=0;return false;}
  if(waitingForMove){if(Math.Sqrt(Math.Pow(cx-savedX,2)+Math.Pow(cy-savedY,2))<0.07)return false;waitingForMove=false;}
  if(since==null||Math.Abs(cx-x)>0.025||Math.Abs(cy-y)>0.025){since=now;x=cx;y=cy;samples=1;return false;}
  samples++;if(samples<3||now-since.Value<TimeSpan.FromSeconds(2))return false;
  savedX=cx;savedY=cy;waitingForMove=true;since=null;samples=0;return true;
 }
}
