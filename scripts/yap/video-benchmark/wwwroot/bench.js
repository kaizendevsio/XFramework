window.runBench = async (asyncOnly = false) => {
 const {createVideoPipeline}=await import('/_content/Bolt.Media.Browser/bolt-media.js');
 const socket=new WebSocket('ws://127.0.0.1:8789'); socket.binaryType='arraybuffer';
 await new Promise(r=>socket.onopen=r); const waiting=[];
 socket.onmessage=e=>waiting.shift()(new Uint8Array(e.data));
 window.echoPackets=data=>new Promise(r=>{waiting.push(r);socket.send(data);});
 await DotNet.invokeMethodAsync('Bench','Init',asyncOnly);
 const pipeline=createVideoPipeline(); pipeline.useCaptureStrategy('rvfc');
 const source=document.createElement('canvas'); source.width=1920;source.height=1080;
 const output=document.createElement('canvas'); const preview=document.createElement('video');
 preview.style.width='240px'; output.style.width='480px'; document.body.append(preview,output);
 const original=navigator.mediaDevices.getUserMedia.bind(navigator.mediaDevices);
 navigator.mediaDevices.getUserMedia=async()=>source.captureStream(30);
 let tick=0,encoded=0,rendered=0,dropped=0,errors=[],busy=false;const queue=[],times=new Map(),latencies=[],cryptoTransport=[],sizes=[];
 const originalEncode=pipeline._encodeElementFrame.bind(pipeline);
 pipeline._encodeElementFrame=(video,metadata)=>{times.set(Math.round(metadata.mediaTime*1e6)>>>0,performance.now());originalEncode(video,metadata);};
 const start=performance.now(),render=pipeline._render.bind(pipeline);
 pipeline._render=(remote,frame)=>{rendered++;const t=times.get(frame.timestamp);if(t!==undefined && performance.now()-start>3000)latencies.push(performance.now()-t);render(remote,frame);};
 async function pump(){if(busy)return;busy=true;try{while(queue.length){const [data,key,id,ts]=queue.shift();const t=performance.now();try{const plain=await DotNet.invokeMethodAsync('Bench','RoundTrip',data,id,ts,key);cryptoTransport.push(performance.now()-t);pipeline.decodeFrame('loop',plain,ts,key);}catch(e){errors.push(String(e));}}}finally{busy=false;}}
 const host={invokeMethodAsync:async(name,...args)=>{if(name==='OnVideoEncoded'){encoded++;sizes.push(args[0].length);if(queue.length>=3){dropped++;pipeline.requestKeyframe();return;}queue.push(args);void pump();}if(name==='OnVideoDecodeFailed')pipeline.requestKeyframe();}};
 const timer=setInterval(()=>{const c=source.getContext('2d'); c.fillStyle='#124';c.fillRect(0,0,1920,1080);for(let i=0;i<400;i++){c.fillStyle=`hsl(${(i*17+tick)%360} 80% 50%)`;c.fillRect((i*53+tick*7)%1920,(i*97)%1080,48,48);}tick++;},1000/30);
 try{await pipeline.initEncoder('h264',1920,1080,3800,30,2);pipeline.addRemote('loop',output,'h264',host);pipeline.attachPreview(preview);await pipeline.startCapture(host,{});await new Promise(r=>setTimeout(r,12000));pipeline.stopCapture();while(busy)await new Promise(r=>setTimeout(r,10));
 const summary=a=>{a.sort((a,b)=>a-b);return {n:a.length,p50:a[Math.floor(a.length*.5)],p95:a[Math.floor(a.length*.95)]};};
 return {encoded,rendered,dropped,errors,latency:summary(latencies),cryptoAndWsAndInterop:summary(cryptoTransport),bytes:summary(sizes),softwareFallback:pipeline.remotes.get('loop').software,config:pipeline.config};
 }finally{clearInterval(timer);await pipeline.dispose();await DotNet.invokeMethodAsync('Bench','End');socket.close();navigator.mediaDevices.getUserMedia=original;}
};
