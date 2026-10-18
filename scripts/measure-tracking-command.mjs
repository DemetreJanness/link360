// Small PTZ timing benchmark; restores the starting pose and stores no frames.
import {spawn} from 'node:child_process';import readline from 'node:readline';import path from 'node:path';
const binary=path.resolve(process.argv[2]??'src/desktop/bin/Release/net9.0-windows/LINK360.exe');
const child=spawn(binary,['--camera-session'],{windowsHide:true,stdio:['pipe','pipe','ignore']});const lines=readline.createInterface({input:child.stdout});const iterator=lines[Symbol.asyncIterator]();
async function exchange(request){const timer=setTimeout(()=>child.kill(),4000);try{child.stdin.write(JSON.stringify(request)+'\n');const next=await iterator.next();if(next.done)throw Error('Camera session exited');const r=JSON.parse(next.value);if(r.Error)throw Error(r.Error);return r;}finally{clearTimeout(timer);}}
let original;
try{const read=await exchange({});const controls=Object.fromEntries(read.Controls.map(c=>[c.Name,c]));original={Pan:controls.Pan.Value,Tilt:controls.Tilt.Value,Zoom:controls.Zoom.Value};const target={...original,Pan:original.Pan+3<=controls.Pan.Max?original.Pan+3:original.Pan-3};const samples=[];
 for(let i=0;i<3;i++){const began=performance.now();const r=await exchange({Target:target,DurationMs:75});samples.push(Math.round(performance.now()-began));if(r.Controls.find(c=>c.Name==='Zoom').Value!==original.Zoom)throw Error('Zoom changed');await exchange({Target:original,DurationMs:75});}
 console.log(JSON.stringify({threeUnitPanRoundtripMs:samples,meanMs:Math.round(samples.reduce((a,b)=>a+b,0)/samples.length),zoomPreserved:true}));
}finally{if(original&&child.exitCode===null){try{await exchange({Target:original,DurationMs:500});}catch{console.log(JSON.stringify({restoreFailed:true}));}}child.stdin.end();child.kill();lines.close();}
