namespace Link360Desktop;
// Room positions can exceed one 30-unit pan span. Wider travel is permitted
// only while optical centering error improves; each command remains <=5 units.
internal sealed class CenteringPolicy {
 readonly Pose anchor;double best=double.PositiveInfinity;int stalled;
 public CenteringPolicy(Pose anchor){this.anchor=anchor;}
 public void Observe(double x,double y) {
  double error=Math.Sqrt(x*x+y*y);if(!double.IsFinite(error))throw new IOException("Invalid torso position; point not saved.");
  if(error<best-.008){best=error;stalled=0;}else if(++stalled>=6)throw new IOException("Centering is not improving. Check camera direction or reframe; saved points are unchanged.");
 }
 public void Check(Pose current,Pose target) {
  if(target==current)throw new IOException("Driver pan/tilt limit reached; choose a position the Link can frame.");
  if(Math.Abs(target.Pan-anchor.Pan)>60||Math.Abs(target.Tilt-anchor.Tilt)>40)throw new IOException("Room position exceeds the 60-pan / 40-tilt centering range; choose a nearer position.");
 }
}
