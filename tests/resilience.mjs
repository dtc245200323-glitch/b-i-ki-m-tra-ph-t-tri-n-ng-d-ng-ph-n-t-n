import assert from 'node:assert/strict';
import {execFileSync} from 'node:child_process';
import {writeFile} from 'node:fs/promises';
const base='http://localhost:5080',results=[];
async function api(path,token,method='GET',data){const r=await fetch(base+path,{method,headers:{'Content-Type':'application/json',Authorization:'Bearer '+token},body:data?JSON.stringify(data):undefined});if(!r.ok)throw Error(path+' '+r.status);return r.status===204?null:r.json();}
async function until(fn,timeout=45000){const start=Date.now();while(Date.now()-start<timeout){try{if(await fn())return;}catch{}await new Promise(r=>setTimeout(r,1000));}throw Error('Timed out waiting for recovery');}
async function mgmt(path,method='GET',body){const r=await fetch('http://localhost:15672/api/'+path,{method,headers:{Authorization:'Basic '+Buffer.from('supportly:supportly-dev').toString('base64'),'Content-Type':'application/json'},body:body?JSON.stringify(body):undefined});if(!r.ok)throw Error('Management '+r.status);return r.json();}
let stopped=false;
try{
const customer=await api('/auth/login','', 'POST',{email:'customer@supportly.local',password:'Supportly123!'}),admin=await api('/auth/login','', 'POST',{email:'admin@supportly.local',password:'Supportly123!'});
execFileSync('docker',['compose','stop','rabbitmq'],{stdio:'pipe'});stopped=true;
const conv=await api('/conversations',customer.token,'POST',{subject:'Kiểm thử phục hồi khi RabbitMQ tạm dừng'});
const before=await api('/admin/metrics',admin.token);assert(before.pendingEvents>=1);results.push({name:'TC21 Broker stopped: REST accepts conversation and retains durable outbox',status:'PASS',pendingEvents:before.pendingEvents});
execFileSync('docker',['compose','start','rabbitmq'],{stdio:'pipe'});stopped=false;
await until(async()=>{const m=await api('/admin/metrics',admin.token);return m.pendingEvents===0;});results.push({name:'TC22 Broker restarted: pending outbox drains automatically',status:'PASS'});
const id=crypto.randomUUID(),event={EventId:crypto.randomUUID(),EventType:'message.sent',Version:1,ConversationId:conv.id,Timestamp:new Date().toISOString(),Message:{Id:id,ConversationId:conv.id,SenderId:customer.user.id,SenderName:customer.user.name,Text:'Sự kiện thử khử trùng lặp',SentAt:new Date().toISOString()}};
async function publish(payload){return mgmt('exchanges/%2F/supportly.events/publish','POST',{properties:{delivery_mode:2,content_type:'application/json'},routing_key:'message.sent',payload:typeof payload==='string'?payload:JSON.stringify(payload),payload_encoding:'string'});}
await until(async()=>{await mgmt('overview');return true;});await publish(event);await publish(event);await until(async()=>(await api('/conversations/'+conv.id+'/history',customer.token)).some(m=>m.id===id));await new Promise(r=>setTimeout(r,1000));const hist=await api('/conversations/'+conv.id+'/history',customer.token);assert.equal(hist.filter(m=>m.id===id).length,1);results.push({name:'TC23 Repeated EventId archived exactly once',status:'PASS'});
const deadBefore=await mgmt('queues/%2F/supportly.dead');await publish('{bad-json');await until(async()=>{const q=await mgmt('queues/%2F/supportly.dead');return q.messages>(deadBefore.messages||0);});results.push({name:'TC24 Malformed event moved to dead-letter queue',status:'PASS'});
await api('/conversations/'+conv.id+'/close',customer.token,'POST',{});
}catch(e){results.push({name:'Resilience test',status:'FAIL',error:e.message});process.exitCode=1;}finally{if(stopped)execFileSync('docker',['compose','start','rabbitmq'],{stdio:'pipe'});await writeFile('output/evidence/resilience-results.json',JSON.stringify({executedAt:new Date().toISOString(),results},null,2));console.log(results);}
