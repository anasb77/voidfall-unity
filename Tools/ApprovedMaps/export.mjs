// Frozen September 20 approved study; export, never redraw the approved silhouettes.
import {createRequire} from 'node:module';
import {readFileSync,writeFileSync,mkdirSync,copyFileSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
import path from 'node:path';
import vm from 'node:vm';
const root=path.dirname(fileURLToPath(import.meta.url));
const require=createRequire(process.env.VOIDFALL_CANVAS_MODULE_ROOT || import.meta.url);
const {createCanvas,Path2D,loadImage,GlobalFonts}=require('@napi-rs/canvas');globalThis.Path2D=Path2D;
GlobalFonts.registerFromPath(path.join(root,'assets/regular.ttf'),'Chakra');
globalThis.window={};globalThis.document={createElement:()=>createCanvas(1,1)};
vm.runInThisContext(readFileSync(path.join(root,'assets/null-city-art.js'),'utf8'));
const {buildCity}=await import('./src/city.js');
const {drawCourtPiece}=await import('./src/court-art.js');
const {drawOriginalSentinel,drawOriginalFamily}=await import('./src/court-original-art.js');
const {drawCityAddition}=await import('./src/city-roster.js');
const {drawInsect,drawHive}=await import('./src/insects.js');
const {drawHydraFloor,drawLegacy}=await import('./src/hydra-art.js');
const output=path.resolve(root,'../../Assets/VoidFall/Resources/VoidFall/ApprovedMaps');mkdirSync(output,{recursive:true});
const manifest=[];
function save(name,c,width,height){writeFileSync(path.join(output,name+'.png'),c.toBuffer('image/png'));manifest.push({name,width,height,ppu:c.width/width});}
function sprite(name,width,height,draw){const c=createCanvas(Math.round(width*4),Math.round(height*4)),g=c.getContext('2d');g.scale(4,4);g.translate(width/2,height/2);draw(g);save(name,c,width,height);}
save('null-city',buildCity(),2560,1440);
const families=['pawn','rook','bishop','knight','queen','armored-knight'];
for(let type=0;type<6;type++)for(let tier=0;tier<(type===1?4:3);tier++)for(const white of [false,true]){
 sprite(`court-${families[type]}-${tier+1}-${white?'white':'black'}`,type===5?240:160,type===5?240:160,g=>drawCourtPiece(g,{x:0,y:0,type,tier,white,angle:0},0));
}
for(let variant=0;variant<3;variant++)for(const white of [false,true])sprite(`sentinel-${variant}-${white?'white':'black'}`,320,320,g=>drawOriginalSentinel(g,{x:0,y:0,white,r:90,mouth:variant,id:variant,suppressPupil:true,phase:{stage:'rest',age:10}}, {x:0,y:0},0,true));
for(const white of [false,true])sprite(`court-knight-original-${white?'white':'black'}`,160,160,g=>{g.scale(24,24);drawOriginalFamily(g,3,0,white)});
for(let i=0;i<10;i++)sprite('city-'+i,192,192,g=>drawCityAddition(g,{type:13+i,angle:0,seed:0,attack:0,charge:0},0,0,0,1));
for(let i=0;i<5;i++)sprite('insect-'+i,160,160,g=>drawInsect(g,{x:0,y:0,kind:i,phase:0,angle:-Math.PI/2},0));
for(let i=0;i<10;i++)sprite('hydra-legacy-'+i,160,160,g=>drawLegacy(g,{x:0,y:0,type:i,angle:-Math.PI/2,hit:0}));
for(let i=0;i<2;i++)sprite('guardian-'+i,384,384,g=>drawInsect(g,{x:0,y:0,kind:i+1,guardian:true,phase:0,angle:-Math.PI/2},0));
sprite('hydra-hive',280,280,g=>{drawHive(g,{x:0,y:0,phase:0,angle:0,id:0,clock:3,hp:180,maxHp:180,suppressUI:true},0);g.font='6px Chakra';g.textAlign='center';g.fillStyle='#a5b98d';g.fillText('HYDRA HIVE',0,-125)});
for(let frame=1;frame<4;frame++){
 const time=frame*.12;
 for(let tier=0;tier<3;tier++)for(const white of [false,true])sprite(`court-armored-knight-${tier+1}-${white?'white':'black'}-frame${frame}`,240,240,g=>drawCourtPiece(g,{x:0,y:0,type:5,tier,white,angle:0},time));
 for(let i=0;i<10;i++)sprite(`city-${i}-frame${frame}`,192,192,g=>drawCityAddition(g,{type:13+i,angle:0,seed:0,attack:0,charge:0},0,0,time,1));
 for(let i=0;i<5;i++)sprite(`insect-${i}-frame${frame}`,160,160,g=>drawInsect(g,{x:0,y:0,kind:i,phase:0,angle:-Math.PI/2},time));
 for(let i=0;i<2;i++)sprite(`guardian-${i}-frame${frame}`,384,384,g=>drawInsect(g,{x:0,y:0,kind:i+1,guardian:true,phase:0,angle:-Math.PI/2},time));
}
const hydra=await loadImage(path.join(root,'assets/hydra-base.png'));Object.defineProperty(hydra,'naturalWidth',{value:hydra.width});Object.defineProperty(hydra,'complete',{value:true});
const floor=createCanvas(3840,2160),floorContext=floor.getContext('2d');floorContext.scale(2.4,2.4);
drawHydraFloor(floorContext,window.NullCityArt,{hydra},0,{left:0,top:0,right:1600,bottom:900});save('hydra-base',floor,1600,900);
writeFileSync(path.join(root,'manifest.json'),JSON.stringify(manifest,null,2));
console.log(`Exported ${manifest.length} approved assets.`);
