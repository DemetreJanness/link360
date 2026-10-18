using System.Text.Json;
using Link360;
using Link360Desktop;
internal static class RoomMapAudit {
 public static void Run(string file){
  var settings=JsonSerializer.Deserialize<Settings>(File.ReadAllText(file),Link360Desktop.Program.Json)!;var points=settings.RoomPoints;var map=RoomMap.Fit(points);
  var state=new CameraReading{Controls=new(){new(){Name="Pan",Min=-145,Max=145,Step=1,Capabilities=2},new(){Name="Tilt",Min=-90,Max=100,Step=1,Capabilities=2},new(){Name="Zoom",Min=100,Max=400,Step=1,Capabilities=2,Value=100}}};
  int virtualInside=0,outside=0;for(int y=0;y<=40;y++)for(int x=0;x<=40;x++){if(map.Target(x/40.0,y/40.0,state,bounded:false)==null)outside++;else virtualInside++;}
  var validation=new List<object>();for(int i=0;i<points.Length;i++){try{var without=RoomMap.Fit(points.Where((_,j)=>j!=i).ToArray());var prediction=without.Target(points[i].X,points[i].Y,state,bounded:false);validation.Add(new{point=i+1,canCrossValidate=prediction!=null,panDisagreement=prediction==null?(int?)null:Math.Abs(prediction.Pan-points[i].Pan),tiltDisagreement=prediction==null?(int?)null:Math.Abs(prediction.Tilt-points[i].Tilt)});}catch(IOException){validation.Add(new{point=i+1,canCrossValidate=false});}}
  Console.WriteLine(JsonSerializer.Serialize(new{measuredPoints=points.Length,virtualInside,outside,validation,meaning="Virtual samples are interpolation of the existing map, not new measurements or 3D depth. Leave-one-out disagreement is a consistency indicator, not a measured aiming error."},Link360Desktop.Program.Json));
 }
}
