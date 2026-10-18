// Numeric-only local person inference and GPU benchmark. No saved image data.
import fs from 'node:fs';import path from 'node:path';import {pathToFileURL} from 'node:url';import {spawn} from 'node:child_process';import readline from 'node:readline';import {createHash} from 'node:crypto';
const {ObsClient}=await import(pathToFileURL(path.resolve('../OBS/director/obs-client.mjs')));
const cfg=JSON.parse(fs.readFileSync(path.join(process.env.APPDATA,'obs-studio/plugin_config/obs-websocket/config.json'),'utf8'));
const obs=new ObsClient();const source=process.argv[2]==='backup'?'Backup - Acer Camera':'Main Camera - SELECT INSTA360 LINK';
try {
 await obs.connect(cfg.server_port??4455,cfg.server_password??'');
 const settings=await obs.call('GetInputSettings',{inputName:source});console.log(JSON.stringify({source,kind:settings.inputKind,keys:Object.keys(settings.inputSettings),deactivateWhenHidden:settings.inputSettings.deactivate_when_not_showing,deviceLabel:settings.inputSettings.video_device_id?.split(':')[0],active:settings.inputSettings.active,visibility:await obs.call('GetSourceActive',{sourceName:source})}));
 const frame=await obs.call('GetSourceScreenshot',{sourceName:source,imageFormat:'jpg',imageWidth:640,imageHeight:360,imageCompressionQuality:80});
 const hashes=new Set();for(let i=0;i<4;i++){const f=await obs.call('GetSourceScreenshot',{sourceName:source,imageFormat:'jpg',imageWidth:640,imageHeight:360,imageCompressionQuality:80});hashes.add(createHash('sha256').update(f.imageData).digest('hex'));await new Promise(r=>setTimeout(r,250));}console.log(JSON.stringify({freshFrameSamples:hashes.size,samples:4}));
 for(const gpu of [false,true]){
  const p=spawn(path.resolve(process.argv[3]??'src/desktop/bin/Release/net9.0-windows/LINK360.exe'),['--body-worker',...(gpu?['--gpu']:[]),...(process.argv.includes('--pose')?['--pose']:[]),...(process.argv.includes('--whole-body')?['--whole-body']:[])],{windowsHide:true});
  const timer=setTimeout(()=>p.kill(),20000);const lines=readline.createInterface({input:p.stdout});
  p.stdin.end((frame.imageData.split(',')[1]+'\n').repeat(3));let count=0;
  try{for await(const line of lines){const r=JSON.parse(line);if(r.Error)throw Error(r.Error);console.log(JSON.stringify({gpu,sample:++count,people:r.People.length,provider:r.Provider,inferenceMs:r.InferenceMs,armsOut:r.ArmsOut,poseMs:r.PoseMs,poseReason:r.PoseReason,width:r.Width,height:r.Height}));}if(count!==3)throw Error('Body inference worker failed to return three samples');}
  finally{clearTimeout(timer);if(p.exitCode===null)p.kill();}
 }
 console.log(JSON.stringify({virtualCamera:(await obs.call('GetVirtualCamStatus')).outputActive}));
}finally{obs.socket?.close();}
