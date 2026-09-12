// Art-only fragment study. Candidate names do not imply an approved unlock.
export const designs = [
  { id:'sound', name:'Sound Blade', color:'#e391ff', secondary:'#ff94d0', candidate:false,
    parts:[['First pulse','The waveform begins.'],['Harmonic','The sound takes shape.'],['Final echo','The last edge of the wave.']] },
  { id:'beam', name:'Charged Rifle', color:'#baa0ff', secondary:'#f0ccff', candidate:false,
    parts:[['Charge chamber','Holds the gathering charge.'],['Focusing rails','Brings the charge into line.'],['Muzzle array','The point of release.']] },
  { id:'gravity', name:'Gravity Knot', color:'#9d8aff', secondary:'#d9c5ff', candidate:true,
    parts:[['Falling arc','The beginning of the orbit.'],['Singularity','The emptiness at the center.'],['Returning arc','Closes the impossible circle.']] },
  { id:'thread', name:'Rift Thread', color:'#75f7d2', secondary:'#d8fff1', candidate:true,
    parts:[['Near anchor','One end of the connection.'],['Filament','A line stretched through the Void.'],['Far anchor','The other end waits.']] },
  { id:'phase', name:'Phase Fang', color:'#ffb777', secondary:'#ffe1a7', candidate:true,
    parts:[['Afterimage','The shape left behind.'],['Split fang','The cut between two places.'],['Forward edge','The place you are about to be.']] }
];

export class FragmentCollection {
  constructor() { this.projects = new Map(designs.map(d => [d.id,new Set()])); }
  collect(id,part) {
    const project=this.projects.get(id);
    if(!project||!Number.isInteger(part)||part<0||part>2||project.has(part))return false;
    project.add(part);return true;
  }
  count(id) { return this.projects.get(id)?.size || 0; }
  reset(id) { this.projects.get(id)?.clear(); }
}

// Shared fracture boundaries tile the complete artwork without overlap or gaps.
const leftEdge='L224 55 L203 92 L220 126 L203 162 L220 206 L210 260';
const rightEdge='L376 55 L397 92 L380 126 L397 162 L380 206 L390 260';
export const fractures=[
  `M0 0 H210 ${leftEdge} H0 Z`,
  `M210 0 H390 ${rightEdge} H210 L220 206 L203 162 L220 126 L203 92 L224 55 Z`,
  `M390 0 H600 V260 H390 L380 206 L397 162 L380 126 L397 92 L376 55 Z`
];
const escape = value => String(value).replace(/[&<>"']/g,ch=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&apos;'}[ch]));

function artwork(id,prefix) {
  const paint=`url(#paint-${prefix})`;
  if(id==='sound'){
    const points=Array.from({length:97},(_,i)=>{
      const t=i/96,x=55+t*490;
      const rhythm=Math.abs(Math.sin(t*23+.7)*.6+Math.sin(t*61)*.22+Math.sin(t*109)*.15);
      const height=(12+rhythm*66)*Math.pow(Math.sin(t*Math.PI),.65);
      return {x,height};
    });
    const contour=points.map((p,i)=>`${i?'L':'M'}${p.x.toFixed(2)} ${(130-p.height).toFixed(2)}`).join(' ')+
      [...points].reverse().map(p=>` L${p.x.toFixed(2)} ${(130+p.height).toFixed(2)}`).join(' ')+' Z';
    const bands=points.map(p=>`<path d="M${p.x} ${130-p.height} V${130+p.height}"/>`).join('');
    return `<path d="${contour}" fill="#ad50dc22" stroke="${paint}" stroke-width="2"/><g stroke="${paint}" stroke-width="1.7">${bands}</g>`;
  }
  if(id==='beam')return `<g stroke="${paint}" stroke-linejoin="round">
    <path d="M65 100 L190 77 L423 88 L535 109 L535 151 L423 172 L190 183 L65 160 L43 130 Z" fill="#1c1830" stroke-width="3"/>
    <path d="M174 98 L452 103 L483 117 L483 143 L452 157 L174 162 L143 130 Z" fill="#59398066" stroke-width="2"/>
    <path d="M94 102 L130 94 L149 130 L130 166 L94 158 L77 130 Z" fill="#9572e655" stroke-width="2"/>
    <path d="M183 130 H530" fill="none" stroke-width="10"/>
    <path d="M208 103 V157 M240 104 V156 M274 104 V156 M308 104 V156 M342 104 V156" opacity=".6"/>
    <path d="M400 73 H435 M452 79 H476 M493 87 H515 M400 187 H435 M452 181 H476 M493 173 H515" stroke-width="3"/>
    <path d="M511 110 V150 M528 113 V147" stroke="#f2dfff" stroke-width="4"/>
  </g>`;
  if(id==='gravity')return `<g fill="none" stroke="${paint}">
    <ellipse cx="300" cy="130" rx="190" ry="57" transform="rotate(-23 300 130)" stroke-width="2.5"/>
    <ellipse cx="300" cy="130" rx="190" ry="57" transform="rotate(23 300 130)" stroke-width="2.5"/>
    <ellipse cx="300" cy="130" rx="161" ry="103" stroke-width="1.5" opacity=".6"/>
    <path d="M93 127 L110 116 L127 130 L110 144 Z M473 130 L490 116 L507 127 L490 144 Z" fill="#5c418855" stroke-width="2"/>
    <circle cx="300" cy="130" r="48" fill="#05060f" stroke="#e6d7ff" stroke-width="2.2"/>
    <circle cx="300" cy="130" r="62" stroke-dasharray="11 15" stroke-width="2"/>
    <path d="M283 130 H290 M310 130 H317 M300 113 V120 M300 140 V147" stroke-width="2"/>
  </g>`;
  if(id==='thread'){
    const wave=Array.from({length:81},(_,i)=>{const t=i/80;return `${i?'L':'M'}${110+t*380} ${130+Math.sin(t*43)*Math.sin(t*Math.PI)*13}`;}).join(' ');
    return `<g stroke="${paint}" stroke-linejoin="round">
      <path d="${wave}" fill="none" stroke-width="2.8"/>
      <path d="M110 57 L138 130 L110 203 L82 130 Z M490 57 L518 130 L490 203 L462 130 Z" fill="#154b3c88" stroke-width="2.5"/>
      <path d="M110 89 L124 130 L110 171 L96 130 Z M490 89 L504 130 L490 171 L476 130 Z" fill="#b9ffe766" stroke-width="1.5"/>
      <path d="M65 97 L51 130 L65 163 M535 97 L549 130 L535 163" fill="none" stroke-width="2"/>
      <circle cx="110" cy="130" r="5" fill="#e3fff5"/><circle cx="490" cy="130" r="5" fill="#e3fff5"/>
    </g>`;
  }
  return `<g stroke="${paint}" stroke-linejoin="round">
    <path d="M64 118 L162 69 L193 96 L123 122 Z M64 142 L162 191 L193 164 L123 138 Z" fill="#7c472733" opacity=".55" stroke-width="1.5"/>
    <path d="M177 109 L366 46 L545 87 L300 121 Z M177 151 L366 214 L545 173 L300 139 Z" fill="#8b492d55" stroke-width="2.5"/>
    <path d="M230 105 L372 63 L474 85 L302 110 Z M230 155 L372 197 L474 175 L302 150 Z" fill="#ffbd7544" stroke-width="1.5"/>
    <path d="M303 96 L315 116 M345 83 L357 110 M303 164 L315 144 M345 177 L357 150" stroke-width="2"/>
    <path d="M413 104 L485 93 M413 156 L485 167" stroke="#ffeac7" stroke-width="3"/>
  </g>`;
}

export function fragmentSvg(id,part=null,{prefix='asset',thumbnail=false,title=true}={}) {
  const design=designs.find(d=>d.id===id);
  if(!design||!(part===null||Number.isInteger(part)&&part>=0&&part<3))throw new Error('Unknown fragment');
  const view=thumbnail&&part!==null?['30 15 210 230','190 15 220 230','360 15 210 230'][part]:'0 0 600 260';
  const name=part===null?design.name:design.name+' — '+design.parts[part][0];
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="${view}" fill="none" ${title?'role="img"':'aria-hidden="true"'}>
    ${title?`<title>${escape(name)}</title>`:''}
    <defs>
      <linearGradient id="paint-${prefix}" x1="50" y1="45" x2="550" y2="210" gradientUnits="userSpaceOnUse"><stop stop-color="${design.color}"/><stop offset=".65" stop-color="${design.secondary}"/><stop offset="1" stop-color="${design.color}"/></linearGradient>
      <filter id="glow-${prefix}" x="-50%" y="-80%" width="200%" height="260%"><feGaussianBlur stdDeviation="3" result="halo"/><feMerge><feMergeNode in="halo"/><feMergeNode in="SourceGraphic"/></feMerge></filter>
      ${part===null?'':`<clipPath id="cut-${prefix}"><path d="${fractures[part]}"/></clipPath>`}
    </defs>
    <g ${part===null?'':`clip-path="url(#cut-${prefix})"`}><g filter="url(#glow-${prefix})">${artwork(id,prefix)}</g></g>
  </svg>`;
}
