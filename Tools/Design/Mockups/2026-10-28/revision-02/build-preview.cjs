const fs = require('node:fs/promises');
const path = require('node:path');
const sharp = require('C:/Users/giorg/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const project = 'C:/Projects/democracy-way';
const destination = 'C:/Users/giorg/.codex/visualizations/2026/10/28/01a1807e-85d5-7cb0-be5a-c89d3fa244fe/democracy-way-project-mockups.html';
const assets = {
  REGULAR: ['Assets/Fonts/IFKargoSans/IFKargoSans-Regular.ttf', 'font/ttf'],
  SEMIBOLD: ['Assets/Fonts/IFKargoSans/IFKargoSans-Semibold.ttf', 'font/ttf'],
  PAPER: ['Assets/Art/UI/Shared/Frames/FrameParchment.png', 'image/webp', 768],
  CHOICE: ['Assets/Art/UI/Shared/Controls/ChoiceFrame.png', 'image/webp', 1000],
  BANNER: ['Assets/Art/UI/Shared/Banners/BannerTeal.png', 'image/webp', 1000],
  ASSEMBLY: ['Assets/Art/Backgrounds/BackgroundAssembly.png', 'image/webp', 1440],
  MENU: ['Assets/Art/Backgrounds/BackgroundMainMenu.png', 'image/webp', 1440],
  LOGO: ['Assets/Art/UI/Logo.png', 'image/webp', 900],
  DIAMOND: ['Assets/Art/UI/MenuAndBio/MenuLeadingDiamond.png', 'image/webp', 64],
  PAUSE: ['Assets/Art/UI/Icons/Pause.png', 'image/webp', 96],
  LEXICON: ['Assets/Art/UI/Icons/Lexicon.png', 'image/webp', 96],
  PORTRAITS: ['Assets/Art/Portraits/Officeholders.png', 'image/webp', 1000],
};
async function main() {
  let html = await fs.readFile(path.join(__dirname, 'mockups.template.html'), 'utf8');
  for (const [key, [relative, mime, width]] of Object.entries(assets)) {
    const source = path.join(project, relative);
    const data = width ? await sharp(source).resize({width, withoutEnlargement:true}).webp({quality:86, alphaQuality:100}).toBuffer() : await fs.readFile(source);
    const uri = `data:${mime};base64,${data.toString('base64')}`;
    const css = ['PAPER','CHOICE','BANNER','ASSEMBLY','MENU','PORTRAITS'].includes(key);
    html = html.replaceAll(`__${key}__`, css ? `url('${uri}')` : uri);
  }
  await fs.writeFile(destination, html);
  console.log(JSON.stringify({destination, bytes:Buffer.byteLength(html), unresolved:/__[A-Z]+__/.test(html)}));
}
main().catch(error=>{console.error(error);process.exitCode=1;});
