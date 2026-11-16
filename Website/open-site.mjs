import {spawn} from 'node:child_process';
import path from 'node:path';
const url='http://127.0.0.1:4173/';
const ready=async()=>{try{const response=await fetch(url,{signal:AbortSignal.timeout(1200)});return response.ok&&(await response.text()).includes('Among Flat');}catch{return false;}};
if(!await ready()){
 const child=spawn(process.execPath,[path.join(import.meta.dirname,'server.mjs')],{cwd:import.meta.dirname,detached:true,windowsHide:true,stdio:'ignore'});
 child.unref();
 let started=false;
 for(let attempt=0;attempt<20;attempt++){await new Promise(resolve=>setTimeout(resolve,250));if(await ready()){started=true;break;}}
 if(!started){console.error('Could not start Among Flat on port 4173.');process.exit(1);}
}
spawn('cmd.exe',['/d','/c','start','',url],{windowsHide:true,stdio:'ignore'}).unref();
