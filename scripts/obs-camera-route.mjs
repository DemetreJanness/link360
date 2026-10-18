import fs from 'node:fs';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
const {ObsClient}=await import(pathToFileURL(path.resolve('../OBS/director/obs-client.mjs')));
const cfg=JSON.parse(fs.readFileSync(path.join(process.env.APPDATA,'obs-studio/plugin_config/obs-websocket/config.json'),'utf8'));
const obs=new ObsClient();
try {
 await obs.connect(cfg.server_port??4455,cfg.server_password??'');
 const source='Main Camera - SELECT INSTA360 LINK';
 const settings=await obs.call('GetInputSettings',{inputName:source});
 const action=process.argv[2]??'status';
 const version=await obs.call('GetVersion');
 console.log(JSON.stringify({propertyButtonRequests:version.availableRequests.filter(r=>/PropertiesButton/.test(r))}));
 console.log(JSON.stringify({inputKind:settings.inputKind,settingKeys:Object.keys(settings.inputSettings),active:settings.inputSettings.active??null,verifiedDevice:settings.inputSettings.video_device_id?.toLowerCase().includes('vid_2e1a&pid_4c01')??false,sourceState:await obs.call('GetSourceActive',{sourceName:source}),virtualCamera:await obs.call('GetVirtualCamStatus')}));
 if(action==='restart-source') {
  await obs.call('PressInputPropertiesButton',{inputName:source,propertyName:'activate'});
  await new Promise(r=>setTimeout(r,500));
  await obs.call('PressInputPropertiesButton',{inputName:source,propertyName:'activate'});
  console.log('Camera source deactivated/reactivated locally');
 }
 if(action==='start') {if(!(await obs.call('GetVirtualCamStatus')).outputActive)await obs.call('StartVirtualCam');console.log('Local virtual camera running');}
} finally {obs.socket?.close();}
