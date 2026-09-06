'use strict';
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const root=path.resolve(__dirname,'../..'),prototype=path.resolve(root,'../Prototypes/eon-sea');
const {createCanvas,loadImage}=require(path.join(process.env.USERPROFILE,'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@napi-rs/canvas'));
const out=path.join(root,'Assets/VoidFall/Art/EonSea'),rosterOut=path.join(root,'Assets/VoidFall/Art/RosterProgression');
for(const dir of [out,rosterOut,path.join(__dirname,'approved')])fs.mkdirSync(dir,{recursive:true});
for(const file of ['glacier.js','art.js','roster.js','elite-roster.js'])fs.copyFileSync(path.join(prototype,file),path.join(__dirname,'approved',file));
const G=require('./approved/glacier.js'),R=require('./approved/roster.js'),ER=require('./approved/elite-roster.js');
const sandbox={console,Math,Glacier:G,window:{},document:{createElement:()=>createCanvas(1,1)}};vm.createContext(sandbox);
const artText=fs.readFileSync(path.join(__dirname,'approved/art.js'),'utf8').replace('return {init,render,screenToWorld};','return {init,render,screenToWorld,exportAssets:()=>({tiles,stamps})};');vm.runInContext(artText,sandbox);sandbox.window.EonArt.init();
const {tiles,stamps}=sandbox.window.EonArt.exportAssets();
function write(canvas,file){fs.writeFileSync(file,canvas.toBuffer('image/png'));}
tiles.forEach((c,i)=>write(c,path.join(out,'Ground'+i+'.png')));
for(const [key,c] of stamps)write(c,path.join(out,'Ice-'+key+'.png'));
const base=createCanvas(3840,2160),bc=base.getContext('2d');for(let y=0;y<2160;y+=1024)for(let x=0;x<3840;x+=1024)bc.drawImage(tiles[0],x,y);write(base,path.join(out,'Base.png'));write(createCanvas(2560,1440),path.join(out,'Details.png'));
const slip=createCanvas(512,320),sc=slip.getContext('2d');sc.translate(256,160);sc.beginPath();sc.ellipse(0,0,245,145,0,0,Math.PI*2);sc.clip();const grad=sc.createRadialGradient(0,0,30,0,0,255);grad.addColorStop(0,'#b8eaff21');grad.addColorStop(1,'#669cce08');sc.fillStyle=grad;sc.fillRect(-256,-160,512,320);for(let i=0;i<20;i++){sc.strokeStyle='#c2e8ff35';sc.lineWidth=1.1;sc.beginPath();sc.moveTo(-250,-150+i*17);sc.lineTo(60,-158+i*17);sc.lineTo(250,-170+i*17);sc.stroke();}write(slip,path.join(out,'Slippery.png'));
(async()=>{const catalogue=JSON.parse(fs.readFileSync(path.join(prototype,'catalogue.json'))),images={};for(const [key,file] of Object.entries({...catalogue.sprites,...R.extraSprites,...ER.extraSprites}))images[key]=await loadImage(path.join(prototype,file));let count=0;
 for(const elite of [false,true]){const module=elite?ER:R;for(const id of module.types)for(let tier=1;tier<=4;tier++){const c=createCanvas(256,256),g=c.getContext('2d');module.draw(g,id,tier,128,128,256,0,images);write(c,path.join(rosterOut,(elite?'elite-':'shared-')+id+'-'+tier+'.png'));count++;}}
 console.log('Exported '+tiles.length+' ground tiles, '+stamps.size+' glacier forms, plate/detail/slippery, and '+count+' corrected roster sprites.');
})().catch(e=>{console.error(e);process.exitCode=1;});
