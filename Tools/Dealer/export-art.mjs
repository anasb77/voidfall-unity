import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';
import { fragmentSvg } from './reference/fragments.mjs';
const here=path.dirname(fileURLToPath(import.meta.url));
const project=path.resolve(here,'../..');
const require=createRequire(import.meta.url);
const modules=process.argv[2] || 'C:/Users/anasb/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules';
const sharp=require(require.resolve('sharp',{paths:[modules]}));
const target=path.join(project,'Assets/VoidFall/Resources/VoidFall/Dealer');
await fs.mkdir(target,{recursive:true});
for(const id of ['sound','beam']) {
  for(const part of [null,0,1,2]) {
    const svg=fragmentSvg(id,part,{prefix:'unity-'+id+'-'+part,thumbnail:part!==null,title:false});
    await sharp(Buffer.from(svg)).resize(part===null?600:260,260,{fit:'contain',background:'#00000000'}).png().toFile(path.join(target,id+'-'+(part===null?'complete':part)+'.png'));
  }
}
const portrait=JSON.parse(await fs.readFile(path.join(here,'reference/portrait.json'),'utf8'));
const glyphs={
  shield:'<path d="M32 16 45 21v10c0 9-6 15-13 19-7-4-13-10-13-19V21Z" fill="currentColor" fill-opacity=".1"/><path d="M32 25v14m-7-7h14"/>',
  delay:'<path d="M22 17h20M22 47h20M24 18v7l16 14v7M40 18v7L24 39v7"/><path d="m27 23 5 5 5-5m-10 19 5-5 5 5"/>',
  projectile:'<path d="M19 42V27l-5 6m5-6 5 6M32 46V18l-6 7m6-7 6 7M45 42V27l-5 6m5-6 5 6"/>'
};
for(const [name,glyph] of Object.entries(glyphs)){
  const color=name==='delay'?'#fb7185':'#6ee7b7';
  const svg='<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" fill="none" color="'+color+'"><circle cx="32" cy="32" r="28" fill="currentColor" fill-opacity=".11" stroke="currentColor" stroke-opacity=".44"/><g stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">'+glyph+'</g></svg>';
  await sharp(Buffer.from(svg)).resize(128,128).png().toFile(path.join(target,name+'.png'));
}
await fs.writeFile(path.join(target,'portrait.json'),JSON.stringify(portrait));
console.log('Exported eight approved legendary images and the shared portrait data.');
