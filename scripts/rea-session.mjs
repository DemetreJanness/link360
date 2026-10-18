import {Client} from '../.rea-tools/node_modules/@modelcontextprotocol/client/dist/index.mjs';
import {StdioClientTransport} from '../.rea-tools/node_modules/@modelcontextprotocol/client/dist/stdio.mjs';
import fs from 'node:fs';
import path from 'node:path';
import readline from 'node:readline';
const root=process.cwd();
const java=path.join(root,'.rea-tools/java/jdk-21.0.12.1+1');
const client=new Client({name:'link360-investigation',version:'0.1.0'});
const transport=new StdioClientTransport({command:process.execPath,args:[path.join(root,'.rea-tools/node_modules/rea-agents/scripts/rea.mjs'),'--mcp'],env:{...process.env,GHIDRA_INSTALL_DIR:path.join(root,'.rea-tools/ghidra/ghidra_12.1.4_PUBLIC'),JAVA_HOME:java,PATH:path.join(java,'bin')+path.delimiter+process.env.PATH,REA_GHIDRA_STARTUP_TIMEOUT_MS:'1800000'}});
try {
 await client.connect(transport);
 const catalog=await client.listTools();
 fs.writeFileSync('.rea-tools/mcp-tools.json',JSON.stringify(catalog,null,2));
 const plan=process.argv[2] ? JSON.parse(fs.readFileSync(process.argv[2],'utf8')) : [];
 let i=0;
 async function run(step) {
  console.error(`REA ${step.name}`);
  const result=await client.callTool({name:step.name,arguments:step.arguments},{timeout:1800000});
  fs.writeFileSync(`.rea-tools/mcp-${i}-${step.name}.json`,JSON.stringify(result,null,2));
  console.error(`Saved ${step.name}; isError=${result.isError??false}`);
  i++;
 }
 for (const step of plan) await run(step);
 if (process.argv.includes('--interactive')) {
  console.error('Ready for JSON tool calls; send exit to close.');
  for await (const line of readline.createInterface({input:process.stdin})) {
   if (line.trim()==='exit') break;
   try { await run(JSON.parse(line)); } catch(e) { console.error(e.message); }
  }
 }
} finally { await client.close(); }
