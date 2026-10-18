namespace Link360Desktop;
internal enum GuidanceSource { Hold,Link,Room }
internal static class HybridGuidance {
 public static GuidanceSource Select(int roomPeople,double roomAgeMs,int linkPeople,double? linkAgeMs){
  if(roomPeople>1&&roomAgeMs<600)return GuidanceSource.Hold;
  if(linkAgeMs is >=0 and <600){if(linkPeople>1)return GuidanceSource.Hold;if(linkPeople==1)return GuidanceSource.Link;}
  return roomPeople==1&&roomAgeMs>=0&&roomAgeMs<600?GuidanceSource.Room:GuidanceSource.Hold;
 }
}
internal sealed class HybridHandoff {
 bool hadLink;DateTime? missingSince,acquiring,lastCandidate;int candidateFrames;GuidanceSource lastSource=GuidanceSource.Hold;
 public GuidanceSource Select(int roomPeople,double roomAgeMs,int linkPeople,double? linkAgeMs,DateTime now){
  var source=HybridGuidance.Select(roomPeople,roomAgeMs,linkPeople,linkAgeMs);
  if(source==GuidanceSource.Link){missingSince=null;if(!hadLink){acquiring??=now;DateTime observation=now.AddMilliseconds(-linkAgeMs!.Value);if(lastCandidate==null||observation-lastCandidate>TimeSpan.FromMilliseconds(10)){lastCandidate=observation;candidateFrames++;}if(candidateFrames<3||now-acquiring<TimeSpan.FromMilliseconds(125))return lastSource==GuidanceSource.Room?GuidanceSource.Room:GuidanceSource.Hold;}hadLink=true;acquiring=null;lastCandidate=null;candidateFrames=0;return lastSource=source;}
  acquiring=null;lastCandidate=null;candidateFrames=0;
  if(source==GuidanceSource.Hold){missingSince=null;if(roomPeople>1||linkPeople>1)hadLink=false;return lastSource=source;}
  if(!hadLink||linkAgeMs==null||linkAgeMs>=600){hadLink=false;return lastSource=source;}
  missingSince??=now;if(now-missingSince.Value<TimeSpan.FromMilliseconds(140))return lastSource=GuidanceSource.Hold;
  hadLink=false;missingSince=null;return lastSource=GuidanceSource.Room;
 }
}
