const fs = require('fs');
const path = require('path');
const vm = require('vm');

const root = path.resolve(__dirname, '..');
const dataRoot = path.join(root, 'data');
const cachePath = path.join(__dirname, 'nativePhrasalTurkish.json');
const cache = fs.existsSync(cachePath) ? JSON.parse(fs.readFileSync(cachePath, 'utf8')) : {};
const files = ['es', 'fr', 'it', 'nl', 'pt'];

function delay(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function translate(text, language) {
  const key = `${language}|${text}`;
  if (cache[key]) return cache[key];
  const url = new URL('https://api.mymemory.translated.net/get');
  url.searchParams.set('q', text);
  url.searchParams.set('langpair', `${language}|tr`);
  const response = await fetch(url);
  if (!response.ok) throw new Error(`${response.status} translating ${key}`);
  const payload = await response.json();
  const result = payload?.responseData?.translatedText;
  if (!result) throw new Error(`No translation returned for ${key}`);
  cache[key] = result;
  await delay(20);
  return result;
}

async function main() {
  for (const language of files) {
    const file = `phrasalverbs${language}.js`;
    const context = { window: {} };
    vm.createContext(context);
    vm.runInContext(fs.readFileSync(path.join(dataRoot, file), 'utf8'), context);
    const dataset = Object.values(context.window)[0];
    for (const entry of dataset) {
      const parts = entry.example.split(' - ');
      const nativeExample = parts[0];
      const existingTurkish = parts.slice(1).join(' - ');
      const turkish = existingTurkish && !existingTurkish.includes('ifadesi') && !existingTurkish.includes('Türkçe:')
        ? existingTurkish
        : await translate(nativeExample, language);
      cache[`${language}|${nativeExample}`] = turkish;
      entry.example = `${nativeExample} - ${turkish}`;
    }
    const globalName = Object.keys(context.window)[0];
    fs.writeFileSync(path.join(dataRoot, file), `window.${globalName} = ${JSON.stringify(dataset, null, 2)};\n`, 'utf8');
    fs.writeFileSync(cachePath, `${JSON.stringify(cache, null, 2)}\n`, 'utf8');
    console.log(`${file}: ${dataset.length} translated examples`);
  }
  fs.writeFileSync(cachePath, `${JSON.stringify(cache, null, 2)}\n`, 'utf8');
}

main().catch((error) => {
  console.error(error.message);
  process.exitCode = 1;
});
