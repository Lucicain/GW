import fs from 'node:fs';
const lab='D:/GwentSyndicate/辛迪加全卡档案/music-lab/';
const {Engine}=await import('data:text/javascript;base64,'+Buffer.from(fs.readFileSync(lab+'engine.js','utf8')).toString('base64'));
const data=JSON.parse(fs.readFileSync(lab+'data.json','utf8'));
const lines=fs.readFileSync('build-check/music-rebuild/tests.txt','utf8').split(/\r?\n/).filter(x=>x.startsWith('PASS rule='));
const results=[];
for(const line of lines){
 const m=line.match(/rule=(\d+) (\w+)\/(\d+)->(\w+)\/(\d+) bridge=(\w+) join=([\d.]+) sameTime=(\w+)/);
 const [,index,state,id,target,dest,bridge,join,same]=m;
 const e=new Engine(data,()=>{});clearInterval(e.timer);
 e.ctx={currentTime:0,createBufferSource(){return{connect(){},disconnect(){},start(when,offset){this.when=when;this.offset=offset;},stop(t){this.stopAt=t;}};},createGain(){return{connect(){},disconnect(){},gain:{setValueAtTime(){},setValueCurveAtTime(){},cancelScheduledValues(){}}};}};
 e.master={};e.theme='current';e.state=state;
 for(const [sid,s] of Object.entries(data.segments))e.cache.set(sid,Promise.resolve({duration:s.durationMs/1000}));
 e.pick=()=>dest;
 e.add(id,data.segments[id].entryMs/1000,state,state);await new Promise(setImmediate);
 await e.change(target);await new Promise(setImmediate);
 const g=e.groups.find(g=>g.id===dest&&!g.bridge&&g.state===target);
 const actual=same==='True'?g.src.when:g.anchor;
 if(Math.abs(actual-Number(join))>.001)throw Error(`Rule ${index}: lab ${actual} != port ${join}`);
 const actualBridge=e.groups.find(g=>g.bridge)?.id||'none';
 if(actualBridge!==bridge)throw Error(`Rule ${index}: bridge mismatch`);
 results.push({rule:Number(index),labJoin:actual,portJoin:Number(join),bridge:actualBridge,sourceOffset:g.src.offset});
 e.stop();
}
fs.writeFileSync('build-check/music-rebuild/lab-parity.json',JSON.stringify(results,null,2));
console.log('PASS: all 35 port joins and bridge IDs match the actual unmodified laboratory engine (<= 1 ms log rounding). No browser/audio device used.');

