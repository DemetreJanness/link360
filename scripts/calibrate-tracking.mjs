// Local developer calibration. Saves numeric deltas only and restores framing.
import fs from 'node:fs';import path from 'node:path';import {pathToFileURL} from 'node:url';import {spawn} from 'node:child_process';
const {ObsClient}=await import(pathToFileURL(path.resolve('../OBS/director/obs-client.mjs')));
const cfg=JSON.parse(fs.readFileSync(path.join(process.env.APPDATA,'obs-studio/plugin_config/obs-websocket/config.json'),'utf8'));
const obs=new ObsClient();const binary=path.resolve('src/desktop/bin/Release/net9.0-windows/LINK360.exe');
function worker(flag,input){return new Promise((resolve,reject)=>{const p=spawn(binary,[flag],{windowsHide:true});let text='';const timer=setTimeout(()=>{p.kill();reject(Error('Worker timeout'));},9000);p.stdout.on('data',c=>text+=c);p.on('error',reject);p.on('close',()=>{clearTimeout(timer);try{const r=JSON.parse(text);if(r.Error)throw Error(r.Error);resolve(r);}catch(e){reject(e);}});p.stdin.end(input+'\n');});}
async function center(){const points=[];for(let i=0;i<3;i++){const frame=await obs.call('GetSourceScreenshot',{sourceName:'Main Camera - SELECT INSTA360 LINK',imageFormat:'jpg',imageWidth:320,imageHeight:180,imageCompressionQuality:80});const r=await worker('--detect-worker',frame.imageData.split(',')[1]);if(r.Faces.length!==1)throw Error('Calibration needs exactly one visible face');const f=r.Faces[0];points.push({x:(f.X+f.Width/2)/r.Width,y:(f.Y+f.Height/2)/r.Height});}const median=k=>points.map(p=>p[k]).sort((a,b)=>a-b)[1];return{x:median('x'),y:median('y')};}
let pose;
try {
 await obs.connect(cfg.server_port??4455,cfg.server_password??'');
 const start=await worker('--worker','{}');const get=n=>start.Controls.find(c=>c.Name===n);pose={Pan:get('Pan').Value,Tilt:get('Tilt').Value,Zoom:get('Zoom').Value};
 const move=async target=>{await worker('--worker',JSON.stringify({Target:target,DurationMs:700}));await new Promise(r=>setTimeout(r,500));};
 const before=await center();const panDelta=pose.Pan+8<=get('Pan').Max?8:-8;
 await move({...pose,Pan:pose.Pan+panDelta});const pan=await center();await move(pose);
 const tiltBefore=await center();const tiltDelta=pose.Tilt+6<=get('Tilt').Max?6:-6;
 await move({...pose,Tilt:pose.Tilt+tiltDelta});const tilt=await center();
 const result={panPixelsPerUnit:(pan.x-before.x)*640/panDelta,tiltPixelsPerUnit:(tilt.y-tiltBefore.y)*360/tiltDelta};
 result.reversePan=result.panPixelsPerUnit>0;result.reverseTilt=result.tiltPixelsPerUnit<0;
 result.conclusive=Math.abs(result.panPixelsPerUnit)>1&&Math.abs(result.tiltPixelsPerUnit)>1;
 console.log(JSON.stringify(result));fs.writeFileSync('.rea-tools/tracking-calibration.json',JSON.stringify(result,null,2));
}finally{if(pose)await worker('--worker',JSON.stringify({Target:pose,DurationMs:1400}));obs.socket?.close();}
