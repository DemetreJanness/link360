// Development-only USB capture: verified Link address, endpoint zero, UVC class
// requests and configuration descriptors only. Video/audio/other devices never
// reach the output file. No network or camera images are recorded.
import fs from 'node:fs';import path from 'node:path';import net from 'node:net';import {spawn,spawnSync} from 'node:child_process';import readline from 'node:readline';
const usb='C:/Program Files/Wireshark/extcap/USBPcapCMD.exe',hub='\\\\.\\USBPcap2';
const enumeration=spawnSync(usb,['--extcap-interface',hub,'--extcap-config'],{encoding:'utf8',windowsHide:true});
if(enumeration.status!==0)throw Error('USBPcap enumeration failed');
const addresses=[...new Set([...enumeration.stdout.matchAll(/value \{arg=99\}\{value=(\d+)_\d+\}\{display=Insta360 Link\}[^\n]*\{parent=(\d+)\}/g)].map(m=>Number(m[2])))];
if(addresses.length!==1)throw Error('Expected exactly one original Link capture address');
const address=addresses[0],seconds=Math.min(120,Math.max(5,Number(process.argv[2]??90)));
const root=path.resolve('.rea-tools/native-usb');fs.mkdirSync(root,{recursive:true});const stem='tracking-'+Date.now(),file=path.join(root,stem+'.pcap');const output=fs.openSync(file,'wx');let savedBytes=0;
let buffer=Buffer.alloc(0),header=false,kept=0,dropped=0,stopping=false;const pending=new Set();const markers=[];const begun=Date.now();
const pipe='\\\\.\\pipe\\link360-controls-'+process.pid;const server=net.createServer(socket=>socket.on('data',parse));
await new Promise((resolve,reject)=>{server.once('error',reject);server.listen(pipe,resolve);});
const child=spawn(usb,['-d',hub,'--devices',String(address),'--inject-descriptors','-s','4096','-o',pipe],{windowsHide:true,stdio:['ignore','pipe','pipe']});
child.stderr.on('data',d=>{const text=d.toString();if(/error|failed|denied/i.test(text))process.stderr.write(text);});
function stop(){if(stopping)return;stopping=true;child.kill();}
const timer=setTimeout(stop,seconds*1000);
const input=readline.createInterface({input:process.stdin});input.on('line',line=>{if(line==='stop')stop();else if(line.startsWith('mark ')){markers.push({elapsedMs:Date.now()-begun,label:line.slice(5)});console.log(JSON.stringify({marker:line.slice(5),elapsedMs:Date.now()-begun}));}});
function parse(chunk){buffer=Buffer.concat([buffer,chunk]);if(buffer.length>4_000_000){stop();throw Error('Capture parser buffer limit exceeded');}
 if(!header){if(buffer.length<24)return;if(buffer.readUInt32LE(0)!==0xa1b2c3d4||buffer.readUInt32LE(20)!==249){stop();throw Error('Unexpected USBPcap file format');}fs.writeSync(output,buffer.subarray(0,24));savedBytes=24;buffer=buffer.subarray(24);header=true;console.log(JSON.stringify({ready:true,hub:'USBPcap2',linkAddress:address,seconds,file}));}
 while(buffer.length>=16){const count=buffer.readUInt32LE(8);if(count>65536){stop();throw Error('Unexpected capture packet size');}if(buffer.length<16+count)break;const record=buffer.subarray(0,16+count),packet=record.subarray(16);buffer=buffer.subarray(16+count);let allow=false;
  if(packet.length>=28&&packet.readUInt16LE(19)===address&&packet[22]===2&&(packet[21]&0x7f)===0){const length=packet.readUInt16LE(0),id=packet.subarray(2,10).toString('hex'),stage=packet[27];
   if(stage===0&&packet.length>=length+8){const type=packet[length],request=packet[length+1],descriptorType=packet[length+3];allow=type===0x21||type===0xa1||(type===0x80&&request===6&&descriptorType===2);if(allow)pending.add(id);}
   else if(pending.has(id)){allow=true;if(stage===3||stage===2)pending.delete(id);}
  }if(allow){if(savedBytes+record.length>2_000_000){stop();return;}fs.writeSync(output,record);savedBytes+=record.length;kept++;}else dropped++;
 }
}
child.on('close',code=>{clearTimeout(timer);input.close();server.close();fs.closeSync(output);fs.writeFileSync(path.join(root,stem+'.markers.json'),JSON.stringify({hub:'USBPcap2',linkAddress:address,seconds,keptControlPackets:kept,droppedNonControlPackets:dropped,markers,exitCode:code},null,2));console.log(JSON.stringify({completed:true,keptControlPackets:kept,droppedNonControlPackets:dropped,file}));process.stdin.pause();});
process.on('SIGINT',stop);
