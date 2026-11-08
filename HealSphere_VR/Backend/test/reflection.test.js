import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, readdir, readFile, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { createHash } from 'node:crypto';
import { createApp } from '../src/app.js';
import { neutralInterpretation, validateReflection } from '../src/reflection.js';
import { inspectWav } from '../src/speech.js';
import { validateGlb } from '../src/glb.js';
const token = 'reflection-local-tests-0123456789-abcdef';
export function wav() {
  const rate=16000, b=Buffer.alloc(44+rate*2); b.write('RIFF');b.writeUInt32LE(b.length-8,4);b.write('WAVEfmt ',8);b.writeUInt32LE(16,16);b.writeUInt16LE(1,20);b.writeUInt16LE(1,22);b.writeUInt32LE(rate,24);b.writeUInt32LE(rate*2,28);b.writeUInt16LE(2,32);b.writeUInt16LE(16,34);b.write('data',36);b.writeUInt32LE(rate*2,40);
  for(let i=0;i<rate;i++)b.writeInt16LE(Math.round(Math.sin(i/rate*440*Math.PI*2)*3000),44+i*2);return b;
}
async function setup(t, options={}) {
  const dataDir=options.dataDir??await mkdtemp(path.join(tmpdir(),'healsphere-reflection-'));
  const backend=await createApp({token,key:'fake-tripo',enabled:false,dataDir,...options});
  const server=backend.app.listen(0,'127.0.0.1');await new Promise(r=>server.once('listening',r));
  t.after(async()=>{await backend.waitForWorker();await new Promise(r=>server.close(r));backend.close();});
  const request=(route,body,type='application/json')=>fetch('http://127.0.0.1:'+server.address().port+route,{method:body===undefined?'GET':'POST',headers:{'X-Backend-Token':token,'Content-Type':type},...(body===undefined?{}:{body:type==='application/json'?JSON.stringify(body):body})});
  return {...backend,dataDir,request};
}
test('animal picker preserves requested details and reuses saved models without provider access', async t => {
  const backend = await setup(t, { fetchFn: () => assert.fail('A disabled picker request must not call a provider') });
  const owner = createHash('sha256').update(token).digest('hex');
  const fox = await backend.library.register({
    owner, bytes: await readFile('../Assets/StreamingAssets/ReflectionDemo/fox-reflection.glb'),
    originalRequest: 'sleeping fox', prompt: 'peaceful sleeping fox', displayName: 'Sleeping Fox',
    provider: 'fixture', source: 'prebuilt', review: 'reviewed_local_demo'
  });
  for (const animal of ['cat and dog', 'a tree', 'cat in a forest', 'two foxes', 'a fierce lion']) {
    assert.equal((await backend.request('/api/animals/prepare', { animal })).status, 400);
  }
  const saved = await (await backend.request('/api/animals/prepare', { animal: 'A little sleeping fox' })).json();
  assert.equal(saved.modelEndpoint, fox.modelEndpoint);
  assert.equal((await backend.request(saved.modelEndpoint)).status, 200);
  for (const animal of ['blue sleeping fox', 'A little red panda', 'A cute baby dragon', 'Owl']) {
    const missing = await (await backend.request('/api/animals/prepare', { animal })).json();
    assert.ok(missing.proposal_id);
    const denied = await backend.request('/api/creations', { proposal_id: missing.proposal_id, mode: 'live', confirm: 'CREATE_ONE_MODEL' });
    assert.equal(denied.status, 403);
    assert.equal((await denied.json()).error, 'GENERATION_DISABLED');
  }
  assert.equal(backend.library.submissionCount(), 0);
});
test('enabled animal picker submits once and releases a validated animal automatically in review mode none', async t => {
  const fixture = await readFile('../Assets/StreamingAssets/ReflectionDemo/owl-support.glb');
  let submissions = 0;
  const response = data => Response.json({ code: 0, data });
  const backend = await setup(t, {
    enabled: true, reviewMode: 'none', pollMs: 5, taskTimeoutMs: 1000,
    fetchFn: async (address, init) => {
      const url = String(address);
      if (url.endsWith('/account/balance')) return response({ balance: 100, frozen: 0 });
      if (url.endsWith('/generation/text-to-model')) {
        submissions++;
        assert.match(JSON.parse(init.body).prompt, /seated owl/);
        return response({ task_id: 'mock-owl' });
      }
      if (url.endsWith('/tasks/mock-owl'))
        return response({ task_id: 'mock-owl', status: 'success', output: { model_url: 'https://cdn.tripo3d.ai/mock.glb' } });
      if (url === 'https://cdn.tripo3d.ai/mock.glb') return new Response(fixture);
      assert.fail('Unexpected provider call');
    }
  });
  const { proposal_id } = await (await backend.request('/api/animals/prepare', { animal: 'a seated owl' })).json();
  const submit = { proposal_id, mode: 'live', confirm: 'CREATE_ONE_MODEL' };
  const job = await (await backend.request('/api/creations', submit)).json();
  const route = '/api/creations/' + job.id;
  await backend.waitForWorker();
  assert.equal((await (await backend.request(route)).json()).status, 'ready');
  assert.equal((await backend.request(route + '/model')).status, 200);
  assert.equal((await (await backend.request('/api/creations', submit)).json()).id, job.id);
  assert.ok((await (await backend.request('/api/animals/prepare', { animal: 'seated owl' })).json()).modelEndpoint);
  assert.equal(submissions, 1);
});
test('three collected inputs yield distinct same-animal offline GLBs without paid provider calls',async t=>{
  const s=await setup(t,{fetchFn:()=>assert.fail('Offline plan must never call a provider')});
  assert.equal((await s.request('/api/reflection/plans',{animal:''})).status,400);
  assert.equal((await s.request('/api/reflection/plans',{animal:'fox',thought1:''})).status,400,'Both thought slots must be explicitly collected or skipped');
  const unicode=await s.request('/api/reflection/plans',{animal:'fox',thought1:'静'.repeat(512),thought2:'想'.repeat(512),offline_demo:true});assert.equal(unicode.status,200,'Two bounded Unicode thoughts fit the request budget');
  const response=await s.request('/api/reflection/plans',{animal:'fox',thought1:'',thought2:'',offline_demo:true});assert.equal(response.status,200);
  const p=await response.json();assert.equal(p.designs.length,3);assert.equal(new Set(p.designs.map(d=>d.pose)).size,3);assert.ok(p.designs.every(d=>d.animal==='fox'));
  const hashes=new Set();
  for(const d of p.designs){const result=await s.request(d.modelEndpoint);assert.equal(result.status,200);const b=Buffer.from(await result.arrayBuffer());assert.ok((await validateGlb(b)).triangles<20000);hashes.add(createHash('sha256').update(b).digest('hex'));}
  assert.equal(hashes.size,3);assert.equal(s.library.submissionCount(),0);
});
test('private thoughts produce bounded structured concepts and never enter model metadata or task journals',async t=>{
  const secret='private thought number 987654',publicConcepts=[];let responses=0;
  const s=await setup(t,{openaiKey:'fake-openai',fetchFn:async(url,init)=>{
    assert.ok(String(url).startsWith('https://api.openai.com/v1/'));
    if(String(url).endsWith('/moderations'))return Response.json({results:[{flagged:false}]});
    const b=JSON.parse(init.body);assert.equal(b.store,false);assert.equal(b.text.format.strict,true);assert.ok(b.input.includes(secret));responses++;
    return Response.json({status:'completed',output:[{content:[{type:'output_text',text:JSON.stringify(neutralInterpretation('fox'))}]}]});
  }});
  const plan=await(await s.request('/api/reflection/plans',{animal:'fox',thought1:secret,thought2:'I would like more time with friends'})).json();
  assert.equal(plan.source,'openai');assert.equal(responses,1);assert.equal(plan.missingModels,3);
  for(const d of plan.designs){const proposal=s.conversation.proposal(d.proposal_id);publicConcepts.push(proposal);assert.ok(!JSON.stringify(proposal).includes(secret));
    const denied=await s.request('/api/creations',{proposal_id:d.proposal_id,mode:'live',confirm:'CREATE_ONE_MODEL'});assert.equal(denied.status,403);assert.match(await denied.text(),/REFLECTION_TASK_AND_CREDIT_AUTHORIZATION_REQUIRED/);}
  assert.equal(s.library.submissionCount(),0);assert.equal((await readdir(path.join(s.dataDir,'creations'))).length,0);
  assert.ok(!(await readFile(path.join(s.dataDir,'library/creations.sqlite'))).includes(Buffer.from(secret)));
  await s.request('/api/reflection/plans/'+plan.id+'/cancel',{});assert.equal((await s.request('/api/reflection/plans/'+plan.id)).status,404);
});
test('skipped thoughts remain neutral and invalid or diagnostic designs are rejected',async t=>{
  const s=await setup(t,{openaiKey:'fake',fetchFn:()=>assert.fail('No private thought means no emotional inference request')});
  const p=await(await s.request('/api/reflection/plans',{animal:'elephant',thought1:'',thought2:''})).json();assert.equal(p.source,'neutral');
  const duplicate=neutralInterpretation('fox');duplicate.designs[2].pose=duplicate.designs[0].pose;assert.throws(()=>validateReflection(duplicate,'fox'),/DESIGNS_MUST_BE_DISTINCT/);
  const clinical=neutralInterpretation('fox');clinical.closing='You have an anxiety disorder.';assert.throws(()=>validateReflection(clinical,'fox'),/INVALID_REFLECTION_RESPONSE/);
  const unsafe=neutralInterpretation('gorilla');unsafe.designs[0].detail='curled tail';assert.throws(()=>validateReflection(unsafe,'gorilla'),/INVALID_REFLECTION_RESPONSE/);
});
test('narration caches scripted WAVs and keeps personalized audio private, bounded and cancellable',async t=>{
  let speechCalls=0;
  const s=await setup(t,{openaiKey:'fake',fetchFn:async(url,init)=>{
    assert.ok(String(url).endsWith('/audio/speech'));const b=JSON.parse(init.body);assert.equal(b.response_format,'wav');speechCalls++;return new Response(wav());
  }});
  const first=await(await s.request('/api/speech/narration',{cue:'welcome'})).json();assert.ok(first.id);assert.equal(inspectWav(Buffer.from(await(await s.request('/api/speech/audio/'+first.id)).arrayBuffer())).duration,1);
  const again=await(await s.request('/api/speech/narration',{cue:'welcome'})).json();assert.equal(again.id,first.id);assert.equal(speechCalls,1);
  const p=await(await s.request('/api/reflection/plans',{animal:'fox',thought1:'',thought2:'',offline_demo:true})).json();
  const personal=await(await s.request('/api/speech/narration',{cue:'closing',plan_id:p.id})).json();assert.ok(personal.id);
  const disk=await readdir(path.join(s.dataDir,'speech'));assert.equal(disk.length,1,'Only reusable scripted audio is stored');
  await s.request('/api/reflection/plans/'+p.id+'/cancel',{});assert.equal((await s.request('/api/speech/audio/'+personal.id)).status,404);
  assert.equal((await s.request('/api/speech/narration',{cue:'../private'})).status,400);
});
test('push-to-talk WAV transcription returns editable text, rejects silence and never stores recordings',async t=>{
  let calls=0;
  const s=await setup(t,{openaiKey:'fake',fetchFn:async(url,init)=>{
    assert.ok(String(url).endsWith('/audio/transcriptions'));assert.ok(init.body instanceof FormData);assert.equal(init.body.get('model'),'gpt-transcribe');calls++;
    return Response.json({text:['fox','I miss my friends','I look forward to tomorrow'][calls-1]});
  }});
  for(let i=0;i<3;i++){const r=await s.request('/api/speech/transcribe',wav(),'audio/wav');assert.equal(r.status,200);assert.ok((await r.json()).text);}
  const silent=wav();silent.fill(0,44);assert.equal((await s.request('/api/speech/transcribe',silent,'audio/wav')).status,400);
  assert.equal((await s.request('/api/speech/transcribe',Buffer.from('bad'),'audio/wav')).status,400);assert.equal(calls,3);
  assert.deepEqual((await readdir(s.dataDir)).sort(),['creations','library']);
});

test('separate plan-bound task and credit authorization is durable and never permits duplicate paid submissions',async t=>{
  for(const limits of [{max_tasks:1,credit_budget:100},{max_tasks:3,credit_budget:10}]){
    let posts=0;
    const s=await setup(t,{generationLimits:{maxLifetime:-1,perSession:-1},fetchFn:async address=>{
      const url=String(address);if(url.endsWith('/account/balance'))return Response.json({code:0,data:{balance:100,frozen:0}});
      if(url.endsWith('/generation/text-to-model')){posts++;return Response.json({code:0,data:{task_id:'authorized-mock-'+posts}});}
      if(url.includes('/tasks/'))return Response.json({code:0,data:{task_id:'authorized-mock-'+posts,status:'queued',progress:0}});
      assert.fail('No unplanned provider call');
    }});
    const plan=await(await s.request('/api/reflection/plans',{animal:'fox',thought1:'',thought2:''})).json();
    const grant={id:'fcd67449-e00f-4372-a2bf-787015ca6f80',owner:createHash('sha256').update(token).digest('hex'),plan_id:plan.id,
      ...limits,verified_task_credit_ceiling:10,proposals:plan.designs.map(d=>d.proposal_id),expires_at:Date.now()+300000};
    await writeFile(path.join(s.dataDir,'reflection-authorization.json'),JSON.stringify(grant));
    const body={proposal_id:plan.designs[0].proposal_id,mode:'live',confirm:'CREATE_ONE_MODEL'};
    const response=await s.request('/api/creations',body);assert.equal(response.status,202);const job=await response.json();
    await s.request('/api/creations',body);assert.equal(posts,1);
    await s.request('/api/creations/'+job.id+'/cancel',{});await s.waitForWorker();
    const blocked=await s.request('/api/creations',{...body,proposal_id:plan.designs[1].proposal_id});assert.equal(blocked.status,409);assert.match(await blocked.text(),/REFLECTION_AUTHORIZATION_BUDGET_EXHAUSTED/);
    assert.equal(posts,1);assert.equal(s.library.submissionCount(),1);
    const other=await(await s.request('/api/reflection/plans',{animal:'rabbit',thought1:'',thought2:''})).json();
    assert.equal((await s.request('/api/creations',{...body,proposal_id:other.designs[0].proposal_id})).status,403,'Grant cannot authorize another session plan');
  }
});
