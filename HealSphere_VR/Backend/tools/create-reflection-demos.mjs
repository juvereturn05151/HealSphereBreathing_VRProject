// Original lightweight 3D illustrations for zero-provider offline play. Creates no UI.
import { mkdir, writeFile, access } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { validateGlb } from '../src/glb.js';
const output = fileURLToPath(new URL('../../Assets/StreamingAssets/ReflectionDemo/', import.meta.url));
const animals = ['fox','cat','rabbit','dog','elephant','gorilla','deer','owl'], roles = ['reflection','support','hope'];
await mkdir(output, { recursive: true });
function model(animal, role) {
  const groups = Array.from({ length: 3 }, () => ({ positions: [], normals: [], indices: [] }));
  function ellipsoid(center, scale, material = 0, rotation = 0) {
    const g = groups[material], base = g.positions.length / 3, segments = 12, rings = 8, c = Math.cos(rotation), s = Math.sin(rotation);
    for (let y = 0; y <= rings; y++) for (let x = 0; x <= segments; x++) {
      const p = y / rings * Math.PI, t = x / segments * Math.PI * 2, unit = [Math.sin(p) * Math.sin(t), Math.cos(p), Math.sin(p) * Math.cos(t)];
      const v = unit.map((n, i) => n * scale[i]), n = unit.map((n, i) => n / scale[i]);
      const normal = [n[0] * c - n[1] * s, n[0] * s + n[1] * c, n[2]], length = Math.hypot(...normal);
      g.positions.push(center[0] + v[0] * c - v[1] * s, center[1] + v[0] * s + v[1] * c, center[2] + v[2]);
      g.normals.push(...normal.map(n => n / length));
    }
    for (let y = 0; y < rings; y++) for (let x = 0; x < segments; x++) {
      const i = base + y * (segments + 1) + x, j = i + segments + 1;
      if (y !== 0) g.indices.push(i, j, i + 1);
      if (y !== rings - 1) g.indices.push(i + 1, j, j + 1);
    }
  }
  function pointedEar(x, y, z, height) {
    const g=groups[0], w=.05, d=.025;
    const points=[[x-w,y,z+d],[x+w,y,z+d],[x,y+height,z+d],[x-w,y,z-d],[x+w,y,z-d],[x,y+height,z-d]];
    for(const face of [[0,1,2],[5,4,3],[0,2,5],[0,5,3],[1,4,5],[1,5,2],[0,3,4],[0,4,1]]){
      const [a,b,c]=face.map(i=>points[i]),u=b.map((v,i)=>v-a[i]),v=c.map((v,i)=>v-a[i]);
      const n=[u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]],length=Math.hypot(...n),base=g.positions.length/3;
      for(const point of [a,b,c]){g.positions.push(...point);g.normals.push(...n.map(x=>x/length));}g.indices.push(base,base+1,base+2);
    }
  }
  const sleeping = role === 'reflection', standing = role === 'hope';
  const bulky = ['elephant','gorilla'].includes(animal), h = sleeping ? .19 : standing ? .52 : .55, z = sleeping ? .23 : standing ? .24 : .08;
  ellipsoid([0, sleeping ? .13 : standing ? .31 : .25, 0], sleeping ? [.25,.13,.27] : standing ? [.18,.17,.28] : [.18,.24,.18]);
  ellipsoid([0,h,z], bulky ? [.17,.17,.15] : [.14,.14,.13]);
  ellipsoid([0,h-.04,z+.105], [.105,.075,.06],1);
  for (const side of [-1,1]) {
    ellipsoid([side*.065,h+.005,z+.121], sleeping ? [.023,.006,.012] : [.014,.019,.012],2);
    const ear = animal === 'rabbit' ? [.045,.17,.035] : animal === 'elephant' ? [.115,.15,.04] : animal === 'dog' ? [.07,.10,.04] : [.05,.075,.04];
    if(['fox','cat'].includes(animal))pointedEar(side*.095,h+.10,z-.025,animal==='fox'?.15:.10);
    else ellipsoid([side*(animal==='elephant'?.18:.115),h+(animal==='elephant'?0:.13),z-.025], ear, 0, side*(animal==='dog'?.8:.22));
    if (standing) {
      for (const depth of [-.17,.15]) ellipsoid([side*.12,.14,depth], [.055,.14,.06],0);
    } else {
      ellipsoid([side*.14,.065,.08], [.075,.065,.10],0);
      if (!sleeping) ellipsoid([side*.10,.16,.16], [.045,.13,.05],1);
    }
  }
  ellipsoid([0,h-.055,z+.167], [.028,.018,.016],2);
  ellipsoid([0,h-.087,z+.15], [.036,.006,.01],2);
  if (animal === 'elephant') {
    for (let i=0;i<3;i++) ellipsoid([.012*i,h-.06-.07*i,z+.17+.025*i], [.038,.065,.04]);
  } else if (animal === 'gorilla') {
    for(const side of [-1,1]) ellipsoid([side*.25,sleeping?.14:.27,.08],[.09,sleeping?.07:.19,.09]);
  } else {
    const long = ['fox','cat','dog'].includes(animal);
    ellipsoid([sleeping?.18:.13,sleeping?.11:.18,-.22], long ? [.09,.09,.20] : [.065,.065,.065], animal==='fox'?1:0, -.5);
  }
  const colors = { fox:[.94,.59,.31,1],cat:[.92,.65,.42,1],rabbit:[.77,.71,.84,1],dog:[.72,.59,.48,1],elephant:[.61,.70,.80,1],gorilla:[.65,.62,.77,1] };
  colors.deer = [.73,.48,.31,1];
  colors.owl = [.57,.49,.70,1];
  if (animal === 'deer') {
    for (const side of [-1, 1]) {
      ellipsoid([side*.085,h+.22,z], [.018,.13,.02], 1, -side*.18);
      ellipsoid([side*.13,h+.26,z], [.018,.07,.02], 1, -side*.68);
      ellipsoid([side*.14,h+.315,z], [.012,.045,.016], 1, -side*.31);
    }
  }
  if (animal === 'owl') {
    groups.forEach(group => { group.positions.length = group.normals.length = group.indices.length = 0; });
    ellipsoid([0,.26,0], [.21,.26,.17]);
    ellipsoid([0,.44,.05], [.22,.16,.16]);
    ellipsoid([0,.22,.13], [.15,.17,.055], 1);
    for (const side of [-1, 1]) {
      ellipsoid([side*.086,.45,.17], [.085,.083,.05], 1);
      ellipsoid([side*.086,.45,.217], sleeping ? [.033,.007,.009] : [.026,.029,.014], 2);
      ellipsoid([side*.18,.24,.02], [.07,.17,.10], 0, -side*.18);
      ellipsoid([side*.083,.045,.105], [.055,.042,.067], 1);
      pointedEar(side*.13,.53,.03,.09);
    }
    ellipsoid([0,.41,.215], [.029,.04,.04], 2);
  }
  const buffers=[], views=[], accessors=[], primitives=[]; let offset=0;
  function add(values, type, components, target) {
    const array = type === 5126 ? new Float32Array(values) : new Uint16Array(values); const bytes=Buffer.from(array.buffer);
    const padding=(4-bytes.length%4)%4; const view=views.length;
    views.push({buffer:0,byteOffset:offset,byteLength:bytes.length,target}); buffers.push(bytes,Buffer.alloc(padding)); offset+=bytes.length+padding;
    const accessor={bufferView:view,componentType:type,count:values.length/components,type:components===3?'VEC3':'SCALAR'};
    if(target===34962 && components===3) { accessor.min=[0,1,2].map(i=>Math.min(...values.filter((_,n)=>n%3===i))); accessor.max=[0,1,2].map(i=>Math.max(...values.filter((_,n)=>n%3===i))); }
    accessors.push(accessor); return accessors.length-1;
  }
  groups.forEach((g, material)=> { if(!g.positions.length)return; const position=add(g.positions,5126,3,34962), normal=add(g.normals,5126,3,34962), indices=add(g.indices,5123,1,34963); primitives.push({attributes:{POSITION:position,NORMAL:normal},indices,material}); });
  const doc={asset:{version:'2.0',generator:'HealSphere original offline animal illustrations'},scene:0,scenes:[{nodes:[0]}],nodes:[{mesh:0,name:animal+' '+role+' offline illustration'}],meshes:[{primitives}],materials:[colors[animal],[.97,.88,.76,1],[.16,.13,.19,1]].map(baseColorFactor=>({pbrMetallicRoughness:{baseColorFactor,metallicFactor:0,roughnessFactor:.85}})),buffers:[{byteLength:offset}],bufferViews:views,accessors};
  const raw=Buffer.from(JSON.stringify(doc)), json=Buffer.concat([raw,Buffer.alloc((4-raw.length%4)%4,32)]), bin=Buffer.concat(buffers);
  const header=Buffer.alloc(20); header.writeUInt32LE(0x46546c67); header.writeUInt32LE(2,4); header.writeUInt32LE(28+json.length+bin.length,8); header.writeUInt32LE(json.length,12); header.writeUInt32LE(0x4e4f534a,16);
  const binHeader=Buffer.alloc(8);binHeader.writeUInt32LE(bin.length);binHeader.writeUInt32LE(0x004e4942,4); return Buffer.concat([header,json,binHeader,bin]);
}
let created=0;
for(const animal of animals) for(const role of roles) {
  const file=path.join(output,animal+'-'+role+'.glb'); let exists=false;
  try { await access(file); exists=true; } catch {}
  if(exists){
    if(!process.argv.includes('--refresh-generated'))continue;
    const previous=await (await import('node:fs/promises')).readFile(file);const length=previous.readUInt32LE(12);
    if(JSON.parse(previous.toString('utf8',20,20+length)).asset?.generator!=='HealSphere original offline animal illustrations')throw new Error('Refusing to replace an unrelated GLB');
  }
  const bytes=model(animal,role); await validateGlb(bytes); await writeFile(file,bytes,{flag:exists?'w':'wx'});created++;
}
console.log('Validated '+created+' new offline animal GLBs. Existing assets preserved; no provider calls.');
