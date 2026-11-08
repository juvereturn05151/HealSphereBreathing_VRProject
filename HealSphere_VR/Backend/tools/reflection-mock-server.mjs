// Disposable mock providers only. Never loads .env or forwards requests to a real provider.
import { mkdtemp, readFile, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { createApp } from '../src/app.js';
import { neutralInterpretation } from '../src/reflection.js';
const root=fileURLToPath(new URL('../../',import.meta.url)),token='healsphere-reflection-mock-only-0123456789',dataDir=await mkdtemp(path.join(tmpdir(),'healsphere-reflection-ui-'));
let speechCalls=0,transcriptions=0;
function wav(){const rate=16000,b=Buffer.alloc(44+rate*2);b.write('RIFF');b.writeUInt32LE(b.length-8,4);b.write('WAVEfmt ',8);b.writeUInt32LE(16,16);b.writeUInt16LE(1,20);b.writeUInt16LE(1,22);b.writeUInt32LE(rate,24);b.writeUInt32LE(rate*2,28);b.writeUInt16LE(2,32);b.writeUInt16LE(16,34);b.write('data',36);b.writeUInt32LE(rate*2,40);for(let i=0;i<rate;i++)b.writeInt16LE(Math.round(Math.sin(i/rate*440*Math.PI*2)*200),44+i*2);return b;}
const backend=await createApp({token,key:'mock-tripo',openaiKey:'mock-openai',enabled:false,dataDir,reviewMode:'manual',reflectionDemoDir:path.join(root,'Assets/StreamingAssets/ReflectionDemo'),fetchFn:async(address,init)=>{
  const url=String(address);if(!url.startsWith('https://api.openai.com/v1/'))throw new Error('REAL_PROVIDER_OR_TRIPO_REQUEST_FORBIDDEN');
  if(url.endsWith('/moderations'))return Response.json({results:[{flagged:false}]});
  if(url.endsWith('/audio/speech')){speechCalls++;return new Response(wav());}
  if(url.endsWith('/audio/transcriptions')){transcriptions++;return Response.json({text:['fox','I have been thinking about my friends','I hope to spend more time outside'][(transcriptions-1)%3]});}
  const body=JSON.parse(init.body);if(body.text?.format?.name!=='animal_reflection')throw new Error('UNEXPECTED_MOCK_REQUEST');
  const animal=JSON.parse(body.input).animal;return Response.json({status:'completed',output:[{content:[{type:'output_text',text:JSON.stringify(neutralInterpretation(animal))}]}]});
}});
const owner=createHash('sha256').update(token).digest('hex');
for(const animal of ['fox','cat']) for(const [i,d] of neutralInterpretation(animal).designs.entries()){
  const description=[d.expression,d.pose,animal,'stylized low poly pastel'].join(' ');
  await backend.library.register({owner,bytes:await readFile(path.join(root,'Assets/StreamingAssets/ReflectionDemo',animal+'-'+d.role+'.glb')),originalRequest:description,prompt:description,displayName:'Mock '+animal+' '+d.role,provider:'fixture',taskId:'',source:'prebuilt',review:'reviewed_local_demo'});
}
// Production API stays unchanged; local-only test metrics are attached before app's terminal 404 via an independent tiny server endpoint is unnecessary.
const server=backend.app.listen(3347,'127.0.0.1',async error=>{
  if(error){console.error('Mock service cannot bind:',error.code);backend.close();process.exitCode=1;return;}
  await writeFile(path.join(root,'Library/HealSphereReflectionMock.connection.json'),JSON.stringify({url:'http://127.0.0.1:3347',token}));
  console.log('Reflection mock service ready on localhost:3347. All providers intercepted; Tripo forbidden.');
});
process.on('SIGINT',()=>server.close(()=>{backend.close();process.exit(0);}));
