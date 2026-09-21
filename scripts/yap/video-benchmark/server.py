import asyncio, sys, threading, functools
from pathlib import Path
from http.server import ThreadingHTTPServer, SimpleHTTPRequestHandler
root=Path(__file__).resolve().parents[3]
sys.path.insert(0,str(root/'artifacts/yap/bench-deps'))
from websockets.asyncio.server import serve
SimpleHTTPRequestHandler.extensions_map['.mjs']='text/javascript'
handler=functools.partial(SimpleHTTPRequestHandler,directory=str(root/'artifacts/yap/video-stalls-publish/wwwroot'))
threading.Thread(target=ThreadingHTTPServer(('127.0.0.1',8788),handler).serve_forever,daemon=True).start()
async def echo(ws):
 async for message in ws: await ws.send(message)
async def main():
 async with serve(echo,'127.0.0.1',8789,max_size=2**22,compression=None): await asyncio.Future()
asyncio.run(main())
