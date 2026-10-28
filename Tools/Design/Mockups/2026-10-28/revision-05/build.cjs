const fs = require('node:fs/promises');
const path = require('node:path');
const sharp = require('C:/Users/giorg/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const project = 'C:/Projects/democracy-way';
const out = 'C:/Users/giorg/.codex/visualizations/2026/10/28/01a1807e-85d5-7cb0-be5a-c89d3fa244fe/democracy-way-attic-ui.html';
const assets = {
 REGULAR:['Assets/Fonts/IFKargoSans/IFKargoSans-Regular.ttf','font/ttf'],
 SEMIBOLD:['Assets/Fonts/IFKargoSans/IFKargoSans-Semibold.ttf','font/ttf'],
 TABLET:['Tools/Design/Mockups/2026-10-28/revision-05/assets/attic-panel.png','image/webp',1050],
 BUTTON:['Tools/Design/Mockups/2026-10-28/revision-05/assets/olive-bronze-button.png','image/webp',700],
 ICONS:['Tools/Design/Mockups/2026-10-28/revision-05/assets/attic-icons.png','image/webp',960],
 ASSEMBLY:['Assets/Art/Backgrounds/BackgroundAssembly.png','image/webp',1280],
 MENU:['Assets/Art/Backgrounds/BackgroundMainMenu.png','image/webp',1280],
 PORTRAITS:['Assets/Art/Portraits/Officeholders.png','image/webp',700],
 LOGO:['Assets/Art/UI/Logo.png','image/webp',600]
};
(async()=>{
 let html=await fs.readFile(path.join(__dirname,'redesign.template.html'),'utf8');
 html=html.replace(/<aside class="ks-cloth-nav"[\s\S]*?<\/aside>/g,'');
 html=html.replace(/<div class="ks-seal" aria-hidden="true"><\/div>/g,'');
 html=html.replace('--seal:__SEAL__;','');
 html=html.replace('<div class="ks-eyebrow">Η υπόθεση της ημέρας</div>','<div class="ks-eyebrow"><span class="ks-current">Λόγοι</span><span>Ψήφος</span><span>Απόφαση</span></div>');
 html=html.replace('<div class="ks-eyebrow">Ώρα να πάρεις θέση</div>','<div class="ks-eyebrow"><span>Λόγοι</span><span class="ks-current">Ψήφος</span><span>Απόφαση</span></div>');
 html=html.replace('Νέο UI — Kairos και low-poly Αθήνα','Οδός Δημοκρατίας — διακριτικό UI');
 html=html.replace('Οδός Δημοκρατίας — διακριτικό UI','Οδός Δημοκρατίας — αττική διακόσμηση');
 html=html.replaceAll('<header class="ks-location"><div class="ks-location-text">','<header class="ks-location"><span class="attic-icon attic-owl" aria-hidden="true"></span><div class="ks-location-text">');
 html=html.replaceAll('<i data-lucide="pause" aria-hidden="true"></i>','<span class="attic-pause" aria-hidden="true">II</span>');
 const motif=(name)=>'<span class="attic-icon attic-'+name+'" aria-hidden="true"></span>';
 for(const [old,name] of [['book-open','scroll'],['folder-open','scroll'],['settings-2','rosette']]){
  html=html.replaceAll('<span class="ks-round"><i data-lucide="'+old+'" aria-hidden="true"></i></span>',motif(name));
 }
 html=html.replaceAll('>Πρόταση Α</button>','>'+motif('scroll')+'Πρόταση Α</button>');
 html=html.replaceAll('>Πρόταση Β</button>','>'+motif('scroll')+'Πρόταση Β</button>');
 html=html.replace('<p class="ks-question">Ποια πρόταση υποστηρίζεις;</p>','<p class="ks-question">'+motif('hand')+'Ποια πρόταση υποστηρίζεις;</p>');
 const lastSection=html.lastIndexOf('</section>');
 if(lastSection<0)throw new Error('Missing carousel section');
 html=html.slice(0,lastSection+10)+'\n'+(await fs.readFile(path.join(__dirname,'additional-screens.html'),'utf8'))+html.slice(lastSection+10);
 html+='\n<script>\n'+(await fs.readFile(path.join(__dirname,'additional.js'),'utf8'))+'\n</script>';
 let style=(await fs.readFile(path.join(__dirname,'refinements.css'),'utf8'))+'\n'+(await fs.readFile(path.join(__dirname,'attic.css'),'utf8'))+'\n'+(await fs.readFile(path.join(__dirname,'additional.css'),'utf8'));
 const {data,info}=await sharp(path.join(project,assets.ICONS[0])).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 const dense=[];
 for(let x=0;x<info.width;x++){
  let count=0;for(let y=0;y<info.height;y++)if(data[(y*info.width+x)*4+3]>140)count++;
  if(count>12)dense.push(x);
 }
 const regions=[];
 for(const x of dense){const last=regions[regions.length-1];if(last&&x-last[1]<18)last[1]=x;else regions.push([x,x]);}
 const glyphs=regions.filter(r=>r[1]-r[0]>info.width*.1);
 if(glyphs.length!==4)throw new Error('Expected 4 separated glyphs, got '+JSON.stringify(glyphs));
 const names=['owl','scroll','hand','rosette'];
 const boxes=[];
 glyphs.forEach(([start,end],index)=>{
  let top=info.height,bottom=0;
  for(let x=start;x<=end;x++)for(let y=0;y<info.height;y++)if(data[(y*info.width+x)*4+3]>140){top=Math.min(top,y);bottom=Math.max(bottom,y);}
  const box={x:Math.max(0,start-6),y:Math.max(0,top-6),w:end-start+13,h:bottom-top+13};
  const center={x:box.x+box.w/2,y:box.y+box.h/2};
  if(box.w/box.h<.8)box.w=box.h*.8;else box.h=box.w/.8;
  box.x=center.x-box.w/2;box.y=center.y-box.h/2;
  boxes.push({name:names[index],...box});
  style+='\n#dw-kairos-new .attic-'+names[index]+'{background-size:'+(info.width/box.w*100).toFixed(4)+'% '+(info.height/box.h*100).toFixed(4)+'%;background-position:'+(box.x/(info.width-box.w)*100).toFixed(4)+'% '+(box.y/(info.height-box.h)*100).toFixed(4)+'%;}';
 });
 html=html.replace('</style>',style+'\n</style>');
 const sizes={};
 for(const [key,[relative,mime,width]] of Object.entries(assets)){
  const buffer=width?await sharp(path.join(project,relative)).resize({width,withoutEnlargement:true}).webp({quality:84,alphaQuality:95}).toBuffer():await fs.readFile(path.join(project,relative));
  const uri=`data:${mime};base64,${buffer.toString('base64')}`;
  const css=['TABLET','BUTTON','ICONS','ASSEMBLY','MENU','PORTRAITS'].includes(key);
  html=html.replaceAll(`__${key}__`,css?`url('${uri}')`:uri);
  sizes[key]=buffer.length;
 }
 if(/__[A-Z]+__/.test(html))throw new Error('Unresolved asset');
 if(/<aside|class="ks-seal"/.test(html))throw new Error('Unexpected banner or medallion');
 if(/data-lucide=/.test(html))throw new Error('Unexpected generic icon');
 if(Buffer.byteLength(html)>1000000)throw new Error('Preview too large: '+Buffer.byteLength(html));
 await fs.writeFile(out,html);
 console.log(JSON.stringify({out,bytes:Buffer.byteLength(html),assets:sizes,glyphs:boxes}));
})().catch(e=>{console.error(e);process.exitCode=1;});
