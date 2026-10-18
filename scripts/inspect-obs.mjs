import fs from 'node:fs';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
const root=path.resolve('../OBS/director');
const {ObsClient}=await import(pathToFileURL(path.join(root,'obs-client.mjs')));
const cfg=JSON.parse(fs.readFileSync(path.join(process.env.APPDATA,'obs-studio/plugin_config/obs-websocket/config.json'),'utf8'));
const obs=new ObsClient();
try {
 await obs.connect(cfg.server_port??4455,cfg.server_password??'');
 console.log(JSON.stringify(await obs.call('GetInputList')));
} finally { obs.socket?.close(); }
