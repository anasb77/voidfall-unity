'use strict';
const fs=require('fs'),path=require('path'),vm=require('vm');
const root=path.resolve(__dirname,'../..'),source=path.resolve(root,'../Prototypes/destroyers-domain'),out=path.join(root,'Assets/VoidFall/Art/Crascendo');
const {createCanvas}=require(path.join(process.env.USERPROFILE,'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@napi-rs/canvas'));
fs.mkdirSync(out,{recursive:true});for(const f of ['art.js','palette-flow.js'])fs.copyFileSync(path.join(source,f),path.join(__dirname,'approved',f));
const sandbox={Math,window:{},document:{createElement:()=>createCanvas(1,1)}};vm.createContext(sandbox);
vm.runInContext(fs.readFileSync(path.join(__dirname,'approved/art.js'),'utf8').replace('colors,palettes});','colors,palettes,exportAssets:()=>materials});'),sandbox);
sandbox.window.DomainArt.init();const materials=sandbox.window.DomainArt.exportAssets();
for(const [i,id] of ['gold','coral','violet'].entries())fs.writeFileSync(path.join(out,'Ground'+i+'.png'),materials.get(id).floor.toBuffer('image/png'));
for(const [i,id] of ['gold','coral','violet'].entries()){const p=sandbox.window.DomainArt.palettes.find(p=>p.id===id),wash=createCanvas(1600,900),wc=wash.getContext('2d'),grad=wc.createLinearGradient(0,0,1600,900);grad.addColorStop(0,p.washA);grad.addColorStop(.48,'#0d0b1000');grad.addColorStop(1,p.washB);wc.fillStyle=grad;wc.fillRect(0,0,1600,900);fs.writeFileSync(path.join(out,'Wash'+i+'.png'),wash.toBuffer('image/png'));}
const c=createCanvas(3840,2160),g=c.getContext('2d');g.fillStyle=g.createPattern(materials.get('gold').floor,'repeat');g.fillRect(0,0,3840,2160);fs.writeFileSync(path.join(out,'Base.png'),c.toBuffer('image/png'));fs.writeFileSync(path.join(out,'Details.png'),createCanvas(2560,1440).toBuffer('image/png'));
fs.writeFileSync(path.join(out,'Tears.json'),JSON.stringify({tears:materials.get('gold').tears},null,2));console.log('Exported three approved Crascendo materials and plate.');
