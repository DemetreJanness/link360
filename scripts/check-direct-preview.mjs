// Direct camera smoke test. Frames stay in process memory; numeric output only.
import {spawn} from 'node:child_process';
import readline from 'node:readline';
import path from 'node:path';
const binary=path.resolve(process.argv[2]??'src/desktop/bin/Release/net9.0-windows/LINK360.exe');
async function check(role){
 const child=spawn(binary,['--video-worker',role],{windowsHide:true,stdio:['pipe','pipe','ignore']});
 const lines=readline.createInterface({input:child.stdout});const timer=setTimeout(()=>child.kill(),12000);let count=0,last=0;
 try{child.stdin.write('frame\n');for await(const line of lines){const frame=JSON.parse(line);if(frame.Error){console.log(JSON.stringify({role,error:frame.Error}));break;}const bytes=Buffer.from(frame.Jpeg,'base64');console.log(JSON.stringify({role,sample:++count,bytes:bytes.length,jpeg:bytes[0]===255&&bytes[1]===216,sequence:frame.Sequence,advanced:frame.Sequence>last}));last=frame.Sequence;if(count===3)break;await new Promise(r=>setTimeout(r,200));child.stdin.write('frame\n');}if(!count)console.log(JSON.stringify({role,frames:0}));}
 finally{clearTimeout(timer);child.kill();lines.close();}
}
await Promise.all([check('room'),check('link')]);
