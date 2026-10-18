namespace Link360Desktop;

// Geometric framing only, not face recognition. A detected head near the top
// of the one detected person supplies a lower-face/neck composition target.
internal static class HeadFramingAnchor {
 public static (double X,double Y)? Select(FaceResult faces,PersonBox person){
  if(faces.Error!=null||faces.Faces.Length!=1||faces.Width<=0||faces.Height<=0||person.Width<=0||person.Height<=0)return null;
  var f=faces.Faces[0];
  if(!double.IsFinite(f.X+f.Y+f.Width+f.Height+f.Confidence+person.X+person.Y+person.Width+person.Height)||f.Confidence<.85||f.Confidence>1||f.Width<16||f.Height<16||f.X<0||f.Y<0||f.X+f.Width>faces.Width||f.Y+f.Height>faces.Height)return null;
  double x=f.X+f.Width*.5,y=f.Y+f.Height*.85;
  if(f.Width<person.Width*.1||f.Width>person.Width*.9||f.Height>person.Height*.8||x<person.X||x>person.X+person.Width||f.Y+f.Height*.5>person.Y+person.Height*.6||f.Y<person.Y-f.Height*.15||y<person.Y||y>person.Y+person.Height)return null;
  return(x,y);
 }
}
