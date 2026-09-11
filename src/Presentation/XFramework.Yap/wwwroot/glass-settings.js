(() => {
    const defaults={blur:2,refraction:64,tintEnabled:false,color:'#d5f879',strength:35,lightness:0};
    const limits={blur:[0,12],refraction:[0,120],strength:[0,80],lightness:[-100,100]};
    let settings={...defaults};
    function validate(value){
        const result={...defaults};
        for(const [key,[min,max]] of Object.entries(limits)) if(Number.isFinite(value?.[key])) result[key]=Math.max(min,Math.min(max,value[key]));
        if(/^#[0-9a-f]{6}$/i.test(value?.color))result.color=value.color;
        result.tintEnabled=value?.tintEnabled===true;
        return result;
    }
    try{settings=validate(JSON.parse(localStorage.getItem('yap-glass')||'null'));}catch{}
    function apply(){
        const style=document.documentElement.style;
        style.setProperty('--glass-blur',`${settings.blur}px`);
        if(settings.tintEnabled){
            const rgb=settings.color.slice(1).match(/../g).map(n=>parseInt(n,16));
            const amount=Math.abs(settings.lightness)/100;
            const tint=rgb.map(n=>Math.round(n+(settings.lightness<0?-n:255-n)*amount));
            style.setProperty('--glass-custom-tint',`rgb(${tint.join(' ')} / ${settings.strength/100})`);
        }else style.removeProperty('--glass-custom-tint');
        document.querySelectorAll('filter[id^="yap-glass-"] feGaussianBlur').forEach(e=>e.setAttribute('stdDeviation',settings.blur));
        document.querySelectorAll('filter[id^="yap-glass-"] feDisplacementMap').forEach(e=>e.setAttribute('scale',settings.refraction));
    }
    function reflect(){
        document.querySelectorAll('[data-glass-setting]').forEach(input=>{
            const key=input.dataset.glassSetting;
            if(input.type==='checkbox')input.checked=settings[key];else input.value=settings[key];
        });
        document.querySelectorAll('[data-glass-output]').forEach(output=>{
            const key=output.dataset.glassOutput;
            const text=settings[key]+(key==='blur'?' px':key==='refraction'?'':'%');
            if(output.textContent!==text)output.textContent=text;
        });
    }
    function save(){apply();reflect();try{localStorage.setItem('yap-glass',JSON.stringify(settings));}catch{}}
    let presets=[];
    try{const saved=JSON.parse(localStorage.getItem('yap-glass-presets')||'[]');if(Array.isArray(saved))presets=saved.filter(p=>typeof p?.name==='string'&&p.name.trim().length>0&&p.name.length<=40).map(p=>({name:p.name,settings:validate(p.settings)}));}catch{}
    function persistPresets(next){try{localStorage.setItem('yap-glass-presets',JSON.stringify(next));presets=next;return true;}catch{return false;}}
    window.yap.glassPresets={
        names:()=>presets.map(p=>p.name),
        save(name){if(typeof name!=='string'||!name.trim()||name.length>40||presets.some(p=>p.name===name))return false;return persistPresets([...presets,{name,settings:{...settings}}]);},
        apply(name){const preset=presets.find(p=>p.name===name);if(preset){settings=validate(preset.settings);save();}},
        remove:name=>persistPresets(presets.filter(p=>p.name!==name))
    };
    window.yap.glassSettings={get:()=>({...settings}),validate};
    apply();
    document.addEventListener('input',event=>{
        const input=event.target,key=input.dataset?.glassSetting;
        if(!Object.hasOwn(defaults,key))return;
        settings=validate({...settings,[key]:input.type==='checkbox'?input.checked:key==='color'?input.value:Number(input.value)});
        if(['color','strength','lightness'].includes(key))settings.tintEnabled=true;
        save();
    });
    document.addEventListener('click',event=>{if(event.target.closest('[data-glass-reset]')){settings={...defaults};save();}});
    document.addEventListener('DOMContentLoaded',()=>{
        new MutationObserver(reflect).observe(document.getElementById('app'),{childList:true,subtree:true});
        reflect();
    });
})();
