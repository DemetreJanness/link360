// No frame files: pass an OBS source image to local inference through stdin.
import fs from 'node:fs';import path from 'node:path';import {pathToFileURL} from 'node:url';import {spawn} from 'node:child_process';
const {ObsClient}=await import(pathToFileURL(path.resolve('../OBS/director/obs-client.mjs')));
const cfg=JSON.parse(fs.readFileSync(path.join(process.env.APPDATA,'obs-studio/plugin_config/obs-websocket/config.json'),'utf8'));
const obs=new ObsClient();
try {
 await obs.connect(cfg.server_port??4455,cfg.server_password??'');
 const frame=await obs.call('GetSourceScreenshot',{sourceName:'Main Camera - SELECT INSTA360 LINK',imageFormat:'jpg',imageWidth:320,imageHeight:180,imageCompressionQuality:80});
 const start=Date.now();
 const result=await new Promise((resolve,reject)=>{
  const p=spawn(path.resolve(process.argv[2]??'src/desktop/bin/Release/net9.0-windows/LINK360.exe'),['--detect-worker'],{windowsHide:true});let text='';
  const deadline=setTimeout(()=>{p.kill();reject(new Error('Inference timed out'));},5000);
  p.stdout.on('data',c=>text+=c);p.on('error',e=>{clearTimeout(deadline);reject(e);});p.on('close',()=>{clearTimeout(deadline);try{resolve(JSON.parse(text));}catch(e){reject(e);}});
  p.stdin.end(frame.imageData.split(',')[1]+'\n');
 });
 if(result.Error)throw new Error(result.Error);
 console.log(JSON.stringify({inferenceMs:Date.now()-start,faces:result.Faces.length,width:result.Width,height:result.Height,virtualCamera:(await obs.call('GetVirtualCamStatus')).outputActive}));
}finally{obs.socket?.close();}
