import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const root=path.join(path.dirname(fileURLToPath(import.meta.url)),'dist');
const types={'.html':'text/html; charset=utf-8','.js':'application/javascript; charset=utf-8','.css':'text/css; charset=utf-8','.json':'application/json; charset=utf-8','.png':'image/png','.svg':'image/svg+xml','.ttf':'font/ttf','.ts':'text/plain; charset=utf-8','.tsx':'text/plain; charset=utf-8','.cs':'text/plain; charset=utf-8','.md':'text/plain; charset=utf-8'};
const server=http.createServer((req,res)=>{let file;try{file=path.resolve(root,'.'+decodeURIComponent(new URL(req.url,'http://localhost').pathname));}catch{res.writeHead(400);return res.end('Bad request');}if(!file.startsWith(root+path.sep)&&file!==root){res.writeHead(403);return res.end('Forbidden');}if(file===root||file.endsWith(path.sep))file=path.join(file,'index.html');fs.stat(file,(err,stat)=>{if(err||!stat.isFile()){res.writeHead(404);return res.end('Not found');}res.writeHead(200,{'Content-Type':new URL(req.url,'http://localhost').searchParams.has('raw')?'text/plain; charset=utf-8':types[path.extname(file)]||'application/octet-stream','Cache-Control':'no-cache','X-Content-Type-Options':'nosniff'});fs.createReadStream(file).pipe(res)})});
server.listen(4326,'127.0.0.1',()=>console.log('Voidfall Content Library: http://127.0.0.1:4326'));
