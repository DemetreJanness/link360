using System.Text.Json;
using Link360Desktop;
internal static class VapeObsTests {
 sealed class Fake: IObsRpc {
  public readonly List<(string Type,JsonElement Data)> Calls=new();public readonly CancellationTokenSource Cancel=new();int id=20;
  public Task<JsonElement> Call(string type,object? data,CancellationToken ct){var d=JsonSerializer.SerializeToElement(data??new{});Calls.Add((type,d));object result=type switch{"GetCurrentProgramScene"=>new{currentProgramSceneName="Fixture"},"GetInputList"=>new{inputs=Array.Empty<object>()},"CreateInput"=>new{sceneItemId=++id},_=>new{}};if(type=="TriggerMediaInputAction")Cancel.Cancel();return Task.FromResult(JsonSerializer.SerializeToElement(result));}
  public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
 }
 public static void Run(){var fake=new Fake();var alert=new VapeObsAlert(fake);bool cancelled=false;try{alert.Fire(true,fake.Cancel.Token).GetAwaiter().GetResult();}catch(OperationCanceledException){cancelled=true;}
  if(!cancelled)throw new Exception("Vape OBS cancellation not observed");var writes=fake.Calls.Where(c=>c.Type=="SetSceneItemEnabled").Select(c=>(Id:c.Data.GetProperty("sceneItemId").GetInt32(),Enabled:c.Data.GetProperty("sceneItemEnabled").GetBoolean())).ToArray();
  if(writes.Length!=4||writes[0]!=(21,true)||writes[1]!=(22,true)||writes[2]!=(21,false)||writes[3]!=(22,false))throw new Exception("Alert cancellation must hide only its two scene items");
  if(!fake.Calls.Any(c=>c.Type=="SetInputAudioMonitorType"&&c.Data.GetProperty("monitorType").GetString()=="OBS_MONITORING_TYPE_MONITOR_AND_OUTPUT"))throw new Exception("Local voice monitor missing");
  if(fake.Calls.Any(c=>c.Type.Contains("Stream")||c.Type.Contains("Camera")||c.Type=="SetCurrentProgramScene"))throw new Exception("Unrelated OBS side effect");alert.DisposeAsync().GetAwaiter().GetResult();Console.WriteLine("PASS: isolated OBS alert sources, local voice monitoring and cancellation cleanup");
 }
}
