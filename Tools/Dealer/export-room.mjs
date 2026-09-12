// Faithful vector transcription of Prototypes/dealer-travel/travel.js drawRoom/drawCircularBackground.
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';
const here=path.dirname(fileURLToPath(import.meta.url));
const require=createRequire(import.meta.url);
const sharp=require(require.resolve('sharp',{paths:[process.argv[2] || 'C:/Users/anasb/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules']}));
const output=path.resolve(here,'../../Assets/VoidFall/Resources/VoidFall/Dealer');
const floor='166,507 258,355 441,308 748,308 946,355 1035,510 930,620 724,688 453,687 273,617';
const svg=(w,h,body)=>`<svg xmlns="http://www.w3.org/2000/svg" width="${w*2}" height="${h*2}" viewBox="0 0 ${w} ${h}">${body}</svg>`;
const defs=`<defs><linearGradient id="ground" x1="0" y1="300" x2="0" y2="690" gradientUnits="userSpaceOnUse"><stop stop-color="#0c1820"/><stop offset="1" stop-color="#080e16"/></linearGradient><radialGradient id="haze"><stop stop-color="#1c3a4b" stop-opacity=".145"/><stop offset="1" stop-opacity="0"/></radialGradient><clipPath id="floor"><polygon points="${floor}"/></clipPath></defs>`;
let details='';
for(let i=0;i<12;i++)details+=`<path d="M${170+i*85} 320L${80+i*110} 740" stroke="#8ec0d0" stroke-opacity=".0235"/>`;
for(const y of [423,484,552,626])details+=`<path d="M170 ${y}H1035" stroke="#8ec0d0" stroke-opacity=".0314"/>`;
for(const x of [290,910])details+=`<path d="M600 612Q${x} 610 ${x} 445" stroke="#8fb4c3" stroke-opacity=".0824" stroke-dasharray="2 10" fill="none"/>`;
let edges='';
for(const d of ['M188 504L275 362','M293 622L446 677','M773 677L922 628','M952 374L1023 501'])edges+=`<path d="${d}" stroke="#65b9c9" stroke-opacity=".2" stroke-width="2"/>`;
await fs.writeFile(path.join(here,'reference/room.svg'),svg(1200,760,defs+`<circle cx="600" cy="395" r="420" fill="url(#haze)"/><polygon transform="translate(0 18)" points="${floor}" fill="#020408" stroke="#1b2b35" stroke-opacity=".267"/><polygon points="${floor}" fill="url(#ground)" stroke="#496a79" stroke-opacity=".294" stroke-width="1.1"/><g clip-path="url(#floor)">${details}</g>${edges}`));
await sharp(path.join(here,'reference/room.svg')).png().toFile(path.join(output,'room-floor.png'));
// Precompose the mist in sRGB: Unity's linear alpha blend otherwise amplifies these very faint arcs.
let rings='<rect width="1400" height="1400" fill="#05070c"/><defs><filter id="mist" x="-30%" y="-30%" width="160%" height="160%"><feGaussianBlur stdDeviation="10"/></filter></defs>';
const arc=(r,a,b)=>`M${700+Math.cos(a)*r} ${700+Math.sin(a)*r}A${r} ${r} 0 0 1 ${700+Math.cos(b)*r} ${700+Math.sin(b)*r}`;
for(let ring=0;ring<5;ring++){
 const radius=255+ring*88,start=ring*1.7,d=arc(radius,start,start+2.15),width=8+ring*4;
 rings+=`<g fill="none"><path d="${d}" stroke="#527fa6" stroke-opacity=".09" stroke-width="${width}" filter="url(#mist)"/><path d="${d}" stroke="${ring%2?'#6685ad':'#569caf'}" stroke-opacity="${ring%2?.0392:.0471}" stroke-width="${width}"/><path d="${arc(radius+17,start+.3,start+1.8)}" stroke="#89b8cf" stroke-opacity=".0706" stroke-width=".7"/></g>`;
}
await sharp(Buffer.from(svg(1400,1400,rings))).png().toFile(path.join(output,'room-rings.png'));
await sharp(Buffer.from(svg(240,100,'<defs><filter id="blur"><feGaussianBlur stdDeviation="7"/></filter></defs><ellipse cx="120" cy="50" rx="53" ry="10" fill="#000" opacity=".45" filter="url(#blur)"/><ellipse cx="120" cy="50" rx="50" ry="9" fill="#000" opacity=".66"/>'))).png().toFile(path.join(output,'room-shadow.png'));
await sharp(Buffer.from(svg(300,300,'<defs><radialGradient id="g"><stop stop-color="#666666" stop-opacity=".125"/><stop offset="1" stop-color="#666666" stop-opacity="0"/></radialGradient></defs><circle cx="150" cy="150" r="135" fill="url(#g)"/><ellipse cx="150" cy="211" rx="76" ry="17" fill="#000" opacity=".6"/><ellipse cx="150" cy="210" rx="80" ry="21" fill="none" stroke="#666666" stroke-opacity=".208"/>'))).png().toFile(path.join(output,'room-portal-pad.png'));
console.log('Exported approved crossing floor, mist rings, hovering shadow and portal pads.');
