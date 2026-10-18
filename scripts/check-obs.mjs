import fs from 'node:fs';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
const root=path.resolve('../OBS/director');
const token=fs.readFileSync(path.join(root,'.token'),'utf8').trim();
const headers={Authorization:`Bearer ${token}`,'Content-Type':'application/json'};
const base='http://127.0.0.1:4466';
const status=await fetch(base+'/status',{headers,redirect:'error',signal:AbortSignal.timeout(5000)});
if(!status.ok)throw Error('OBS companion status failed');
const data=await status.json();
console.log(JSON.stringify({companionReachable:true,statusKeys:Object.keys(data)}));
const unauthorized=await fetch(base+'/status',{redirect:'error',signal:AbortSignal.timeout(5000)});
if(unauthorized.status!==401)throw Error('OBS companion accepted unauthenticated request');
console.log('PASS: authenticated local companion status; unauthenticated requests rejected');
const {ObsClient}=await import(pathToFileURL(path.join(root,'obs-client.mjs')));
const cfg=JSON.parse(fs.readFileSync(path.join(process.env.APPDATA,'obs-studio/plugin_config/obs-websocket/config.json'),'utf8'));
const obs=new ObsClient();
try {await obs.connect(cfg.server_port??4455,cfg.server_password??'');console.log(JSON.stringify({streamActive:(await obs.call('GetStreamStatus')).outputActive,virtualCameraActive:(await obs.call('GetVirtualCamStatus')).outputActive}));}
finally {obs.socket?.close();}
