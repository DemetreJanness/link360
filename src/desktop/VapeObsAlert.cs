namespace Link360Desktop;
// Dedicated names and current-scene items only. No camera, scene-switch or tracking writes.
internal sealed class VapeObsAlert:IAsyncDisposable {
 readonly IObsRpc rpc;readonly SemaphoreSlim gate=new(1);string? scene;int textId=-1,audioId=-1;
 const string TextName="LINK360 Community - VAPE ALERT",AudioName="LINK360 Community - Vape Voice";
 public VapeObsAlert(IObsRpc? rpc=null){this.rpc=rpc??new LocalObsRpc();}
 async Task<int> Item(string target,string name,string kind,object settings,CancellationToken ct){
  var inputs=await rpc.Call("GetInputList",null,ct);var existing=inputs.GetProperty("inputs").EnumerateArray().FirstOrDefault(i=>i.GetProperty("inputName").GetString()==name);
  if(existing.ValueKind==System.Text.Json.JsonValueKind.Undefined){var created=await rpc.Call("CreateInput",new{sceneName=target,inputName=name,inputKind=kind,inputSettings=settings,sceneItemEnabled=false},ct);return created.GetProperty("sceneItemId").GetInt32();}
  if(existing.GetProperty("inputKind").GetString()!=kind)throw new IOException("Alert source name is already used by another input kind.");
  await rpc.Call("SetInputSettings",new{inputName=name,inputSettings=settings,overlay=true},ct);
  var items=await rpc.Call("GetSceneItemList",new{sceneName=target},ct);var item=items.GetProperty("sceneItems").EnumerateArray().FirstOrDefault(i=>i.GetProperty("sourceName").GetString()==name);
  return item.ValueKind==System.Text.Json.JsonValueKind.Undefined?(await rpc.Call("CreateSceneItem",new{sceneName=target,sourceName=name,sceneItemEnabled=false},ct)).GetProperty("sceneItemId").GetInt32():item.GetProperty("sceneItemId").GetInt32();
 }
 public async Task Fire(bool voice,CancellationToken ct){await gate.WaitAsync(ct);try{
  scene=(await rpc.Call("GetCurrentProgramScene",null,ct)).GetProperty("currentProgramSceneName").GetString()!;
  textId=audioId=-1;textId=await Item(scene,TextName,"text_gdiplus_v3",new{text="VAPE ALERT!",font=new{face="Segoe UI",size=64,style="Bold",flags=1},color=0xFF70FF,outline=true,outline_size=4,outline_color=0x101010},ct);
  await rpc.Call("SetSceneItemTransform",new{sceneName=scene,sceneItemId=textId,sceneItemTransform=new{positionX=48,positionY=48}},ct);
  if(voice){var file=Path.Combine(AppContext.BaseDirectory,"audio","vape-alert.wav");if(!File.Exists(file))throw new IOException("Missing local vape voice asset.");audioId=await Item(scene,AudioName,"ffmpeg_source",new{is_local_file=true,local_file=file,looping=false,restart_on_activate=true,close_when_inactive=true},ct);await rpc.Call("SetInputVolume",new{inputName=AudioName,inputVolumeDb=-8},ct);await rpc.Call("SetInputAudioMonitorType",new{inputName=AudioName,monitorType="OBS_MONITORING_TYPE_MONITOR_AND_OUTPUT"},ct);}
  await Enable(textId,true,ct);if(voice){await Enable(audioId,true,ct);await rpc.Call("TriggerMediaInputAction",new{inputName=AudioName,mediaAction="OBS_WEBSOCKET_MEDIA_INPUT_ACTION_RESTART"},ct);}await Task.Delay(5000,ct);
 }finally{try{using var cleanup=new CancellationTokenSource(12000);await Hide(cleanup.Token);}finally{gate.Release();}}}
 Task Enable(int id,bool enabled,CancellationToken ct)=>rpc.Call("SetSceneItemEnabled",new{sceneName=scene,sceneItemId=id,sceneItemEnabled=enabled},ct);
 public async Task Hide(CancellationToken ct){if(scene==null)return;if(textId>=0)await Enable(textId,false,ct);if(audioId>=0)await Enable(audioId,false,ct);}
 public ValueTask DisposeAsync()=>rpc.DisposeAsync();
}
