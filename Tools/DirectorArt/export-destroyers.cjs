'use strict';
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),crypto=require('node:crypto');
const root=path.resolve(__dirname,'../..'),approved=path.join(__dirname,'approved/destroyer-art.js');
const {createCanvas,Path2D}=require(path.join(process.env.USERPROFILE,'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@napi-rs/canvas'));
const sandbox={window:{},Path2D};vm.createContext(sandbox);vm.runInContext(fs.readFileSync(approved,'utf8'),sandbox);
const art=sandbox.window.DestroyerArt,poses=[['idle0','idle',0],['idle1','idle',.4],['idle2','idle',.8],['idle3','idle',1.2],['windup','windup',0],['attack','attack',0],['recover','recover',0],['hit','idle',0]];
const manifest={sourceHash:crypto.createHash('sha256').update(fs.readFileSync(approved)).digest('hex'),pixelsPerUnit:1,nominalBodyRadius:64,tileSize:256,frames:poses.map(p=>p[0]),creatures:[]};
for(const item of art.roster){
  const id=item.id==='razorwing'?'razor':item.id;
  const folder=path.join(root,'Assets/VoidFall/Resources/VoidFall/Destroyers',id);fs.mkdirSync(folder,{recursive:true});
  for(const [name,state,t] of poses){const c=createCanvas(256,256);art.draw(c.getContext('2d'),item.id,128,128,64,t,{state,angle:0,flash:name==='hit'?1:0,reducedMotion:false});fs.writeFileSync(path.join(folder,name+'.png'),c.toBuffer('image/png'));}
  manifest.creatures.push({id:'destroyer-'+id,name:item.name,role:item.role,resourcePath:'VoidFall/Destroyers/'+id});
}
fs.writeFileSync(path.join(__dirname,'manifest.json'),JSON.stringify(manifest,null,2));
console.log('Exported approved Destroyers:5 creatures ×8 native pose frames.');
