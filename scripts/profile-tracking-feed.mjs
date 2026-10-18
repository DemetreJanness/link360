import fs from 'node:fs';import path from 'node:path';import {pathToFileURL} from 'node:url';
const {ObsClient}=await import(pathToFileURL(path.resolve('../OBS/director/obs-client.mjs')));
const cfg=JSON.parse(fs.readFileSync(path.join(process.env.APPDATA,'obs-studio/plugin_config/obs-websocket/config.json'),'utf8'));
const obs=new ObsClient();
try{await obs.connect(cfg.server_port??4455,cfg.server_password??'');
 for(const type of ['GetInputSettings','GetSourceActive']){const start=Date.now();await obs.call(type,type==='GetInputSettings'?{inputName:'Main Camera - SELECT INSTA360 LINK'}:{sourceName:'Main Camera - SELECT INSTA360 LINK'});console.log(JSON.stringify({type,ms:Date.now()-start}));}
 for(const width of [640,320,160]){const times=[];for(let i=0;i<3;i++){const start=Date.now();await obs.call('GetSourceScreenshot',{sourceName:'Main Camera - SELECT INSTA360 LINK',imageFormat:'png',imageWidth:width,imageHeight:width*9/16});times.push(Date.now()-start);}console.log(JSON.stringify({width,screenshotMs:times}));}
 const times=[];for(let i=0;i<3;i++){const start=Date.now();await obs.call('GetSourceScreenshot',{sourceName:'Main Camera - SELECT INSTA360 LINK',imageFormat:'jpg',imageWidth:320,imageHeight:180,imageCompressionQuality:80});times.push(Date.now()-start);}console.log(JSON.stringify({format:'jpg',width:320,screenshotMs:times}));
}finally{obs.socket?.close();}
