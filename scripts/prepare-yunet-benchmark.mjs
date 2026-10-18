// Offline, narrowly scoped ONNX shape metadata adaptation. No weights or nodes
// change. Field numbers follow https://github.com/onnx/onnx/blob/main/onnx/onnx.proto.
import fs from 'node:fs';
import crypto from 'node:crypto';
const source=fs.readFileSync('models/face_detection_yunet_2023mar.onnx');
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
if(hash(source)!=='8f2383e4dd3cfbb4553ea8718107fc0423210dc964f9f4280604804ed2552fa4')throw Error('Unexpected source model');
function varint(b,state){let n=0n,s=0n;for(let i=0;i<10;i++){if(state.p>=b.length)throw Error('Truncated protobuf');const v=b[state.p++];n|=BigInt(v&127)<<s;if(!(v&128))return n;s+=7n;}throw Error('Invalid varint');}
function encode(n){n=BigInt(n);const a=[];do{let v=Number(n&127n);n>>=7n;a.push(v|(n?128:0));}while(n);return Buffer.from(a);}
function fields(b){let state={p:0},out=[];while(state.p<b.length){const start=state.p,tag=Number(varint(b,state)),wire=tag&7,id=tag>>>3;let payload;if(wire===2){const len=Number(varint(b,state));payload=b.subarray(state.p,state.p+len);state.p+=len;}else if(wire===0)varint(b,state);else if(wire===1)state.p+=8;else if(wire===5)state.p+=4;else throw Error('Unsupported wire type');if(state.p>b.length)throw Error('Invalid field length');out.push({id,wire,payload,raw:b.subarray(start,state.p)});}return out;}
const bytes=(id,b)=>Buffer.concat([encode(id*8+2),encode(b.length),b]);
function replace(b,id,fn){let count=0;const out=fields(b).map(f=>{if(f.id!==id)return f.raw;if(f.wire!==2)throw Error('Wrong metadata wire');count++;return bytes(id,fn(f.payload));});if(count!==1)throw Error(`Expected one field ${id}, got ${count}`);return Buffer.concat(out);}
function shape(info,dims){return replace(info,2,type=>replace(type,1,tensor=>replace(tensor,2,()=>Buffer.concat(dims.map(n=>bytes(1,Buffer.concat([encode(8),encode(n)])))))));}
const root=fields(source),graph=root.filter(f=>f.id===7);if(graph.length!==1)throw Error('Expected one graph');
let inputs=0,outputs=0;
const patchedGraph=Buffer.concat(fields(graph[0].payload).map(f=>{
 // Intermediate value_info contains stale fixed 640x640 shapes too. Omit
 // those optional annotations so ONNX Runtime infers them from the new input.
 if(f.id===13)return Buffer.alloc(0);
 if(f.id!==11&&f.id!==12)return f.raw;
 const name=fields(f.payload).find(x=>x.id===1)?.payload?.toString();
 if(f.id===11){if(name!=='input')throw Error('Unexpected input');inputs++;return bytes(11,shape(f.payload,[1,3,192,320]));}
 const m=/^(cls|obj|bbox|kps)_(8|16|32)$/.exec(name??'');if(!m)throw Error('Unexpected output');outputs++;const stride=Number(m[2]),channels=m[1]==='bbox'?4:m[1]==='kps'?10:1;return bytes(12,shape(f.payload,[1,(192/stride)*(320/stride),channels]));
}));
if(inputs!==1||outputs!==12)throw Error('Unexpected model interface');
const unchanged=b=>Buffer.concat(fields(b).filter(f=>![11,12,13].includes(f.id)).map(f=>f.raw));
if(!unchanged(graph[0].payload).equals(unchanged(patchedGraph)))throw Error('Computational graph changed');
const output=replace(source,7,()=>patchedGraph),runtime=process.argv.includes('--runtime-model');
if(hash(output)!=='7c588045dc2c5fadfca1e52a97097f1bdf188c9c5d2370746c089fb0907f4f2b')throw Error('Derived model differs from validated build');
const path=runtime?'models/face_detection_yunet_link360_320x192.onnx':'.rea-tools/yunet-320x192-benchmark.onnx';fs.writeFileSync(path,output);
console.log(JSON.stringify({path,sourceSha256:hash(source),sha256:hash(output),inputs,outputs,graphAndWeightsUnchanged:true,scope:runtime?'Validated CPU runtime model':'Offline benchmark only'}));
