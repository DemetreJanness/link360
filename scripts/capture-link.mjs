import fs from 'node:fs';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
const {ObsClient}=await import(pathToFileURL(path.resolve('../OBS/director/obs-client.mjs')));
const cfg=JSON.parse(fs.readFileSync(path.join(process.env.APPDATA,'obs-studio/plugin_config/obs-websocket/config.json'),'utf8'));
const obs=new ObsClient();
try {
 await obs.connect(cfg.server_port??4455,cfg.server_password??'');
 const r=await obs.call('GetSourceScreenshot',{sourceName:'Main Camera - SELECT INSTA360 LINK',imageFormat:'png',imageWidth:960,imageHeight:540});
 const output=path.resolve(process.argv[2]??'.rea-tools/baseline.png');
 fs.writeFileSync(output,Buffer.from(r.imageData.split(',')[1],'base64'));
 console.log('Local preview saved');
} finally {obs.socket?.close();}
