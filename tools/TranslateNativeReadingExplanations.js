const fs = require('fs');
const path = require('path');
const vm = require('vm');

const root = path.resolve(__dirname, '..');
const dataRoot = path.join(root, 'data');
const cachePath = path.join(__dirname, 'nativeReadingTurkish.json');
const cache = fs.existsSync(cachePath) ? JSON.parse(fs.readFileSync(cachePath, 'utf8')) : {};
const files = ['it', 'pt', 'es', 'nl'];

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
  fs.writeFileSync(cachePath, `${JSON.stringify(cache, null, 2)}\n`, 'utf8');
  return result;
}

async function main() {
  for (const language of files) {
    const file = `readingcomp${language}.js`;
    const context = { window: {} };
    vm.createContext(context);
    vm.runInContext(fs.readFileSync(path.join(dataRoot, file), 'utf8'), context);
    const globalName = Object.keys(context.window)[0];
    const passages = context.window[globalName];
    for (const passage of passages) {
      for (const question of passage.questions) {
        const nativeExplain = question.explain.split(' - ', 1)[0];
        const turkish = await translate(nativeExplain, language);
        question.explain = `${nativeExplain} - ${turkish}`;
      }
    }
    fs.writeFileSync(path.join(dataRoot, file), `window.${globalName} = ${JSON.stringify(passages, null, 2)};\n`, 'utf8');
    console.log(`${file}: ${passages.reduce((sum, item) => sum + item.questions.length, 0)} explanations translated`);
  }
}

main().catch((error) => {
  console.error(error.message);
  process.exitCode = 1;
});
