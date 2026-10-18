// Development validation: numeric state and ephemeral frame hashes only.
import fs from 'node:fs';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
import {spawn} from 'node:child_process';
import {createHash} from 'node:crypto';
function readCamera(binary){return new Promise((resolve,reject)=>{
 const p=spawn(binary,['--worker'],{windowsHide:true});let output='';
 const timer=setTimeout(()=>{p.kill();reject(new Error('timeout'));},9000);
 p.stdout.on('data',chunk=>output+=chunk);p.on('error',e=>{clearTimeout(timer);reject(e);});
 p.on('close',()=>{clearTimeout(timer);try{resolve(JSON.parse(output));}catch(e){reject(e);}});
 p.stdin.end('{}\n');
});}
const {ObsClient}=await import(pathToFileURL(path.resolve('../OBS/director/obs-client.mjs')));
const cfg=JSON.parse(fs.readFileSync(path.join(process.env.APPDATA,'obs-studio/plugin_config/obs-websocket/config.json'),'utf8'));
const obs=new ObsClient();const records=[];
const binary=path.resolve('src/desktop/bin/Release/net9.0-windows/LINK360.exe');
try {
 await obs.connect(cfg.server_port??4455,cfg.server_password??'');
 console.log('Reconnect monitor armed for 120 seconds; no movement commands.');
 const until=Date.now()+120000;
 while(Date.now()<until) {
  const entry={time:new Date().toISOString()};
  try {const r=await readCamera(binary);entry.connected=!r.Error;entry.errorKind=r.ErrorKind??null;entry.controls=r.Controls?.filter(x=>['Pan','Tilt','Zoom'].includes(x.Name)).map(x=>({name:x.Name,value:x.Value}));}
  catch {entry.connected=false;entry.readFailed=true;}
  try {const frame=await obs.call('GetSourceScreenshot',{sourceName:'Main Camera - SELECT INSTA360 LINK',imageFormat:'png',imageWidth:160,imageHeight:90});entry.frameHash=createHash('sha256').update(frame.imageData).digest('hex');entry.virtualCamera=(await obs.call('GetVirtualCamStatus')).outputActive;}
  catch {entry.obsReadFailed=true;}
  records.push(entry);console.log(JSON.stringify(entry));
  await new Promise(r=>setTimeout(r,2000));
 }
} finally {obs.socket?.close();fs.writeFileSync('.rea-tools/reconnect-monitor.json',JSON.stringify(records,null,2));}
