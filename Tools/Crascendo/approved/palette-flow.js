(function(root,f){const api=f();if(typeof module!=='undefined')module.exports=api;else root.PaletteFlow=api;})(globalThis,function(){
 function at(progress){const p=Number.isFinite(progress)?Math.max(0,Math.min(1,progress)):0;const second=p>=.5;const t=second?(p-.5)*2:p*2;return {from:second?'coral':'gold',to:second?'violet':'coral',mix:t*t*(3-2*t),progress:p};}
 function encounter(state){return state.boss||state.mode==='escape'||state.mode==='won'?1:Math.max(0,Math.min(1,state.time/Math.max(1,state.bossAt)));}
 function blend(a,b,t){const parse=x=>{const value=x.slice(1);return [0,2,4,6].map((i)=>i<value.length?parseInt(value.slice(i,i+2),16):255);};const x=parse(a),y=parse(b);return '#'+x.map((v,i)=>Math.round(v+(y[i]-v)*t).toString(16).padStart(2,'0')).join('');}
 return {at,encounter,blend};
});
