using Link360;
namespace Link360Desktop;
internal record NativeSpeedDemand(int Pan,int Tilt,bool Valid=true);
// Image feedback drives velocity directly. No relative-position accumulation or
// finite waypoint stop. Native transport separately caps/ramp-limits each axis.
internal sealed class NativeSpeedPlanner {
 readonly AdaptiveAim aim=new();readonly AimDeadband horizontal=new(),vertical=new();DateTime last;int stable;
 internal static int Level(double error)=>Math.Clamp(error<=.12?(int)(error*12)+1:2+(int)Math.Ceiling((error-.12)*28),1,NativeSpeedPacket.TrackingMaximum);
 public void Reset(){aim.Reset();horizontal.Reset();vertical.Reset();last=default;stable=0;}
 public NativeSpeedDemand? Plan(BodyResult? result,DateTime at,DateTime now,bool reversePan=false,bool reverseTilt=false){
  if(result==null||at>now||now-at>=TimeSpan.FromMilliseconds(300)||!LinkLock.HasUsableTorso(result)||result.Error!=null||result.Width<=0||result.Height<=0||result.People.Length!=1){Reset();return new(0,0,false);}
  var p=result.People[0];if(!double.IsFinite(p.CenterX+p.CenterY+p.Confidence)||p.Confidence<TrackingConfidence.Minimum||p.Confidence>1||p.CenterX<0||p.CenterX>result.Width||p.CenterY<0||p.CenterY>result.Height){Reset();return new(0,0,false);}
  if(at<=last)return null;last=at;if(++stable<2)return new(0,0);
  double rawX=p.CenterX/result.Width-.5,rawY=p.CenterY/result.Height-.5;var filtered=aim.Update(rawX+.5,rawY+.5,at);
  int Axis(double error,double raw){if(error==0||Math.Abs(raw)<.035||Math.Sign(error)!=Math.Sign(raw))return 0;return Math.Sign(error)*Level(Math.Abs(error));}
  return new(Axis(horizontal.Error(filtered.X-.5),rawX)*(reversePan?-1:1),Axis(vertical.Error(filtered.Y-.5),rawY)*(reverseTilt?1:-1));
 }
}
