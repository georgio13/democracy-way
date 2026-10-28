const fs = require('node:fs/promises');
const path = require('node:path');
const sharp = require('C:/Users/giorg/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const project = 'C:/Projects/democracy-way';
const out = 'C:/Users/giorg/.codex/visualizations/2026/10/28/01a1807e-85d5-7cb0-be5a-c89d3fa244fe/kairos-lowpoly-redesign.html';
const assets = {
 REGULAR:['Assets/Fonts/IFKargoSans/IFKargoSans-Regular.ttf','font/ttf'],
 SEMIBOLD:['Assets/Fonts/IFKargoSans/IFKargoSans-Semibold.ttf','font/ttf'],
 TABLET:['Tools/Design/Mockups/2026-10-28/revision-03/assets/ivory-tablet.png','image/webp',1050],
 BUTTON:['Tools/Design/Mockups/2026-10-28/revision-03/assets/bronze-action.png','image/webp',700],
 SEAL:['Tools/Design/Mockups/2026-10-28/revision-03/assets/assembly-medallion.png','image/webp',160],
 ASSEMBLY:['Assets/Art/Backgrounds/BackgroundAssembly.png','image/webp',1280],
 MENU:['Assets/Art/Backgrounds/BackgroundMainMenu.png','image/webp',1280],
 PORTRAITS:['Assets/Art/Portraits/Officeholders.png','image/webp',800],
 LOGO:['Assets/Art/UI/Logo.png','image/webp',600]
};
(async()=>{
 let html=await fs.readFile(path.join(__dirname,'redesign.template.html'),'utf8');
 const sizes={};
 for(const [key,[relative,mime,width]] of Object.entries(assets)){
  const buffer=width?await sharp(path.join(project,relative)).resize({width,withoutEnlargement:true}).webp({quality:84,alphaQuality:95}).toBuffer():await fs.readFile(path.join(project,relative));
  const uri=`data:${mime};base64,${buffer.toString('base64')}`;
  const css=['TABLET','BUTTON','SEAL','ASSEMBLY','MENU','PORTRAITS'].includes(key);
  html=html.replaceAll(`__${key}__`,css?`url('${uri}')`:uri);
  sizes[key]=buffer.length;
 }
 if(/__[A-Z]+__/.test(html))throw new Error('Unresolved asset');
 if(Buffer.byteLength(html)>1000000)throw new Error('Preview too large: '+Buffer.byteLength(html));
 await fs.writeFile(out,html);
 console.log(JSON.stringify({out,bytes:Buffer.byteLength(html),assets:sizes}));
})().catch(e=>{console.error(e);process.exitCode=1;});
