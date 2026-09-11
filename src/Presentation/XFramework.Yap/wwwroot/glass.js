// Rounded-bezel refraction inspired by https://kube.io/blog/liquid-glass-css-svg/.
// This approximates a convex surface and Snell refraction, rather than using noise.
(() => {
    const NS = 'http://www.w3.org/2000/svg';
    const media = matchMedia('(prefers-reduced-transparency: reduce)');
    function field(x,y,width,height,radius,bezel) {
        const px=x-width/2, py=y-height/2;
        const qx=Math.abs(px)-(width/2-radius), qy=Math.abs(py)-(height/2-radius);
        const ax=Math.max(qx,0), ay=Math.max(qy,0), length=Math.hypot(ax,ay);
        const distance=radius-length-Math.min(Math.max(qx,qy),0);
        if(distance<0 || distance>=bezel)return [0,0];
        const t=Math.max(.015,distance/bezel);
        const slope=(1-t)/Math.sqrt(1-(1-t)**2);
        const normalAngle=Math.atan(slope);
        const rayAngle=normalAngle-Math.asin(Math.sin(normalAngle)/1.45);
        const magnitude=Math.min(1,Math.tan(rayAngle)*.7);
        const nx=length>0?ax/length:qx>qy?1:0;
        const ny=length>0?ay/length:qx>qy?0:1;
        // Sample inward through the convex rim; SVG offsets are source coordinates.
        return [-Math.sign(px)*nx*magnitude,-Math.sign(py)*ny*magnitude];
    }
    window.yap.glass = {field};
    function init(){
        const root=document.getElementById('app');
        if(!root)return;
        // SVG backdrop filters remain an enhancement, not a CSS-support guarantee.
        const chromium=/Chrome|Chromium|Edg\//.test(navigator.userAgent)&&! /CriOS/.test(navigator.userAgent);
        if(!chromium || !CSS.supports('backdrop-filter','url("#glass")') || typeof ResizeObserver==='undefined')return;
        const svg=document.createElementNS(NS,'svg');
        svg.setAttribute('width','0');svg.setAttribute('height','0');svg.setAttribute('aria-hidden','true');
        svg.style.position='absolute';svg.style.pointerEvents='none';
        const defs=document.createElementNS(NS,'defs');svg.appendChild(defs);document.body.appendChild(svg);
        const entries=new Map();let sequence=0,scheduled=false;
        const make=(tag,attributes)=>{const el=document.createElementNS(NS,tag);for(const [k,v]of Object.entries(attributes))el.setAttribute(k,String(v));return el;};
        function resize(element){
            const entry=entries.get(element);if(!entry)return;
            if(media.matches){element.removeAttribute('data-refracted');return;}
            const width=Math.round(element.clientWidth),height=Math.round(element.clientHeight);
            if(width<8||height<8)return;
            const radius=Math.min(parseFloat(getComputedStyle(element).borderTopLeftRadius)||18,width/2,height/2);
            const key=`${width}:${height}:${radius}`;
            if(entry.key===key){element.setAttribute('data-refracted','');return;}
            const canvas=document.createElement('canvas');
            const ratio=Math.min(1,512/width,256/height);
            canvas.width=Math.max(1,Math.round(width*ratio));canvas.height=Math.max(1,Math.round(height*ratio));
            const ctx=canvas.getContext('2d');if(!ctx)return;
            const image=ctx.createImageData(canvas.width,canvas.height), bezel=Math.min(24,radius*.95);
            for(let y=0;y<canvas.height;y++)for(let x=0;x<canvas.width;x++){
                const [dx,dy]=field((x+.5)/ratio,(y+.5)/ratio,width,height,radius,bezel),i=(y*canvas.width+x)*4;
                image.data[i]=Math.round(128+dx*127);image.data[i+1]=Math.round(128+dy*127);image.data[i+2]=128;image.data[i+3]=255;
            }
            ctx.putImageData(image,0,0);
            const filter=make('filter',{id:entry.id,x:0,y:0,width,height,filterUnits:'userSpaceOnUse','color-interpolation-filters':'sRGB'});
            filter.appendChild(make('feImage',{href:canvas.toDataURL(),x:0,y:0,width,height,preserveAspectRatio:'none',result:'bezel'}));
            // Keep both stages in one SVG graph so the lens consumes the blurred backdrop.
            filter.appendChild(make('feGaussianBlur',{in:'SourceGraphic',stdDeviation:window.yap.glassSettings?.get().blur ?? 2,result:'softBackdrop'}));
            filter.appendChild(make('feDisplacementMap',{in:'softBackdrop',in2:'bezel',scale:window.yap.glassSettings?.get().refraction ?? 64,xChannelSelector:'R',yChannelSelector:'G'}));
            if(entry.filter)entry.filter.replaceWith(filter);else defs.appendChild(filter);
            entry.filter=filter;entry.key=key;
            element.style.setProperty('--glass-filter',`url("#${entry.id}")`);
            element.setAttribute('data-refracted','');
        }
        const observer=new ResizeObserver(items=>{for(const item of items)resize(item.target);});
        function sync(){
            scheduled=false;
            for(const [element,entry]of entries)if(!element.isConnected){observer.unobserve(element);entry.filter?.remove();entries.delete(element);}
            for(const element of root.querySelectorAll('[data-liquid-glass]'))if(!entries.has(element)){
                entries.set(element,{id:`yap-glass-${++sequence}`,filter:null,key:null});observer.observe(element);
            }
        }
        new MutationObserver(()=>{if(!scheduled){scheduled=true;requestAnimationFrame(sync);}}).observe(root,{childList:true,subtree:true});
        media.addEventListener('change',()=>{for(const element of entries.keys())resize(element);});
        sync();
    }
    if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',init,{once:true});else init();
})();
