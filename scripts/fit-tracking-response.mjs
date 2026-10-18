import fs from 'node:fs';
const [path,startArg='43000',endArg='60000']=process.argv.slice(2);
const rows=fs.readFileSync(path,'utf8').trim().split(/\r?\n/).map(JSON.parse).filter(r=>r.type==='sample').map(r=>r.data);
const start=Number(startArg),end=Number(endArg);
const observations=rows.filter(r=>r.ElapsedMs>=start&&r.ElapsedMs<end&&r.Anchor==='head');
const results={};
for(const [axis,error] of [['Pan','LinkErrorX'],['Tilt','LinkErrorY']]) {
 const trials=[];
 for(let lag=0;lag<=700;lag+=10){
  const pairs=[];let j=0;
  for(const r of observations){while(j+1<rows.length&&rows[j+1].ElapsedMs<=r.ElapsedMs-lag)j++;
   if(rows[j].ElapsedMs>r.ElapsedMs-lag||r[error]==null)continue;
   const x=rows[j].Commanded?.[axis]??rows[j].After?.[axis];if(x!=null)pairs.push([x,r[error]]);
  }
  const n=pairs.length,mx=pairs.reduce((s,p)=>s+p[0],0)/n,my=pairs.reduce((s,p)=>s+p[1],0)/n;
  const xx=pairs.reduce((s,p)=>s+(p[0]-mx)**2,0),xy=pairs.reduce((s,p)=>s+(p[0]-mx)*(p[1]-my),0);
  if(xx<1)continue;const slope=xy/xx,intercept=my-slope*mx;
  const rms=Math.sqrt(pairs.reduce((s,p)=>s+(p[1]-intercept-slope*p[0])**2,0)/n);
  trials.push({lagMs:lag,slope,intercept,rms,n});
 }
 results[axis]=trials.sort((a,b)=>a.rms-b.rms).slice(0,4);
}
console.log(JSON.stringify({start,end,meaning:'Exploratory command-to-image fit, assuming a stationary subject. Capture-read timestamps are not exposure timestamps. Subject movement can bias this estimate; not a camera calibration.',results},null,2));
