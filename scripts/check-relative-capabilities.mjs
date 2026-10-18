// Read-only documented Windows relative-property support probe; no motion.
import {spawn} from 'node:child_process';import path from 'node:path';
const binary=path.resolve(process.argv[2]??'.rea-tools/jitter-tests/LINK360.exe');const p=spawn(binary,['--worker'],{windowsHide:true,stdio:['pipe','pipe','ignore']});let out='';p.stdout.on('data',d=>out+=d);p.stdin.end(JSON.stringify({ReadRelativeCapabilities:true}));const timer=setTimeout(()=>p.kill(),8000);
try{await new Promise(r=>p.on('close',r));const value=JSON.parse(out);if(value.Error)throw Error(value.Error);console.log(JSON.stringify(value.Controls.filter(c=>c.Name.startsWith('Relative'))));}finally{clearTimeout(timer);}
