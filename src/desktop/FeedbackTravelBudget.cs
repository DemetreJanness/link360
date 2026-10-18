using Link360;
namespace Link360Desktop;

// Bound corrections whose optical effect may not yet be present in the frame.
// This is a conservative command-history budget, not an exposure timestamp or
// a prediction of the physical motor position. Repeated frames cannot ratchet
// a small residual into a large movement before feedback catches up.
internal sealed class FeedbackTravelBudget {
 readonly Queue<(DateTime At,int Pan,int Tilt)> history=new();
 public void Reset()=>history.Clear();
 public void Observe(CameraReading state,DateTime now){
  int Value(string name)=>state.Controls.Single(c=>c.Name==name).Value;
  if(history.Count>0&&now<history.Last().At)Reset();
  if(history.Count>0&&now==history.Last().At)return;
  history.Enqueue((now,state.CommandedPan??Value("Pan"),state.CommandedTilt??Value("Tilt")));
  while(history.Count>1&&now-history.ElementAt(1).At>TimeSpan.FromMilliseconds(350))history.Dequeue();
  while(history.Count>128)history.Dequeue();
 }
 public int Limit(string axis,int desired){
  if(desired==0||history.Count<2)return desired;
  var start=history.Peek();var end=history.Last();
  int moved=axis=="Pan"?end.Pan-start.Pan:end.Tilt-start.Tilt;
  // Do not reverse against an earlier correction until its image feedback has
  // had time to arrive. Manual stop/override still cancels immediately.
  if(moved!=0&&Math.Sign(moved)!=Math.Sign(desired))return 0;
  return Math.Sign(desired)*Math.Max(0,Math.Abs(desired)-Math.Abs(moved));
 }
}
