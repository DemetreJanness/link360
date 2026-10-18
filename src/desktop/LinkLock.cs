namespace Link360Desktop;
internal static class LinkLock {
 // Geometric guard against head-only/edge fragments. This is not a skeleton or
 // identity match; the room camera remains the coarse fallback when uncertain.
 public static bool HasUsableTorso(BodyResult? result){
  if(result==null||result.Error!=null||result.AnchorKind=="missing"||result.Width<=0||result.Height<=0||result.People.Length!=1)return false;
  var p=result.People[0];bool upper=result.AnchorKind is "upper-body" or "head";bool anchor=result.AnchorKind=="pose"||upper?p.CenterX>=0&&p.CenterX<result.Width&&p.CenterY>=0&&p.CenterY<result.Height:result.AnchorKind=="box"&&p.CenterY/result.Height>=.15&&p.CenterY/result.Height<=.75;
  return anchor&&p.Confidence>=TrackingConfidence.Minimum&&p.Confidence<=1&&double.IsFinite(p.X+p.Y+p.Width+p.Height+p.CenterX+p.CenterY+p.Confidence)&&p.CenterX>=p.X&&p.CenterX<=p.X+p.Width&&p.CenterY>=p.Y&&p.CenterY<=p.Y+p.Height&&p.Height/result.Height>=(upper?.20:.35)&&p.Width/result.Width>=.10;
 }
 public static int GuidanceCount(BodyResult? result)=>result==null?0:result.People.Length==1?(HasUsableTorso(result)?1:0):result.People.Length;
}
