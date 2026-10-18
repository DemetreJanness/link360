namespace Link360Desktop;
// One-Euro-style local coordinate filter. Quiet measurements get more
// smoothing; sustained fast motion raises the cutoff and reduces lag.
internal sealed class AdaptiveAim {
 double x,y,vx,vy,rawX,rawY;DateTime? previous;
 public double Speed=>Math.Max(Math.Abs(vx),Math.Abs(vy));
 public void Reset(){previous=null;vx=vy=0;}
 public (double X,double Y) Update(double nextX,double nextY,DateTime now){
  if(!double.IsFinite(nextX+nextY))throw new ArgumentException("Invalid aim measurement");
  if(previous==null||now<previous||now-previous>TimeSpan.FromMilliseconds(600)){x=rawX=nextX;y=rawY=nextY;vx=vy=0;previous=now;return(x,y);}
  double dt=Math.Clamp((now-previous.Value).TotalSeconds,.001,.25);previous=now;
  double Alpha(double cutoff)=>dt/(dt+1/(2*Math.PI*cutoff));
  double derivativeAlpha=Alpha(1.5);vx+=derivativeAlpha*((nextX-rawX)/dt-vx);vy+=derivativeAlpha*((nextY-rawY)/dt-vy);rawX=nextX;rawY=nextY;
  x+=Alpha(1.2+5*Math.Abs(vx))*(nextX-x);y+=Alpha(1.2+5*Math.Abs(vy))*(nextY-y);return(x,y);
 }
}
internal sealed class AimDeadband {
 bool active;
 public void Reset()=>active=false;
 public double Error(double error){double magnitude=Math.Abs(error);if(active&&magnitude<=.035)active=false;else if(!active&&magnitude>=.065)active=true;return active?error:0;}
}
