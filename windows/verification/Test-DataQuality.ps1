#requires -Version 7.0
<#
.SYNOPSIS
Read-only structural audit of root datasets; never repairs files or the manifest.
.DESCRIPTION
Requires Node.js 22+. Uses node --check plus a non-executing literal parser.
Exit 0: no errors; 1: findings; 2: prerequisite/runner failure. JSON goes to stdout
and optionally ReportPath. SelfTest exercises the parser without reading datasets.
#>
[CmdletBinding()]
param(
    [string]$DataDirectory = (Join-Path $PSScriptRoot '../../data'),
    [string]$ReportPath,
    [switch]$SelfTest
)
$ErrorActionPreference = 'Stop'
$oldOutputEncoding = $OutputEncoding
$oldConsoleEncoding = [Console]::OutputEncoding
$validator = @'
'use strict';
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const {spawnSync} = require('node:child_process');
// Not eval/vm: accept only window.NAME = [literal, ...]; plus comments and
// string concatenation. Reject executable expressions, computed keys and spreads.
function parse(source, json = false) {
  let i = 0;
  const duplicates = [];
  function fail(message) { throw new Error(`${message} at offset ${i}`); }
  function skip() {
    for (;;) {
      while (/\s/.test(source[i] || '') && i < source.length) i++;
      if (!json && source.startsWith('//', i)) { while (i < source.length && source[i] !== '\n') i++; }
      else if (!json && source.startsWith('/*', i)) {
        const end = source.indexOf('*/', i + 2); if (end < 0) fail('Unclosed comment'); i = end + 2;
      } else return;
    }
  }
  function take(c) { skip(); if (source[i] === c) { i++; return true; } return false; }
  function need(c) { if (!take(c)) fail(`Expected ${c}`); }
  function identifier() {
    skip(); const m = /^[A-Za-z_$][\w$]*/.exec(source.slice(i));
    if (!m) fail('Expected identifier'); i += m[0].length; return m[0];
  }
  function string() {
    skip(); const quote = source[i++]; let result = '';
    if (quote !== '"' && (json || quote !== "'")) fail('Expected quoted string');
    while (i < source.length) {
      let c = source[i++]; if (c === quote) return result;
      if (c === '\n' || c === '\r') fail('Unescaped newline');
      if (c !== '\\') { result += c; continue; }
      c = source[i++];
      const escapes = {n:'\n', r:'\r', t:'\t', b:'\b', f:'\f', v:'\v', '0':'\0'};
      if (c === 'u' || (!json && c === 'x')) {
        const n = c === 'u' ? 4 : 2; const hex = source.slice(i, i+n);
        if (!new RegExp(`^[0-9a-fA-F]{${n}}$`).test(hex)) fail('Invalid hex escape');
        result += String.fromCharCode(parseInt(hex,16)); i += n;
      } else if (!json && (c === '\n' || c === '\r')) { if (c === '\r' && source[i] === '\n') i++; }
      else result += escapes[c] ?? c;
    }
    fail('Unclosed string');
  }
  function value(location = '$', depth = 0) {
    if (depth > 32) fail('Nesting limit'); skip();
    if (take('[')) {
      const result = []; if (take(']')) return result;
      do { result.push(value(`${location}[${result.length}]`, depth+1)); if (take(']')) return result; need(','); }
      while (!(take(']'))); return result;
    }
    if (take('{')) {
      const result = Object.create(null); if (take('}')) return result;
      do {
        skip(); const key = source[i] === '"' || source[i] === "'" ? string() : identifier();
        need(':'); if (Object.hasOwn(result, key)) duplicates.push(`${location}.${key}`);
        result[key] = value(`${location}.${key}`, depth+1);
        if (take('}')) return result; need(',');
      } while (!take('}')); return result;
    }
    if (source[i] === '"' || source[i] === "'") {
      let result = string(); if (!json) while (take('+')) result += string(); return result;
    }
    const m = /^(?:-?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?|true|false|null)\b/.exec(source.slice(i));
    if (!m) fail('Expected literal (executable expressions are forbidden)');
    i += m[0].length; return JSON.parse(m[0]);
  }
  let global = null;
  if (!json) { if (identifier() !== 'window') fail('Expected window'); need('.'); global = identifier(); need('='); }
  const result = value(); if (!json) take(';'); skip();
  if (i !== source.length) fail('Unexpected trailing content');
  if (!json && !Array.isArray(result)) fail('Dataset must be an array');
  return {value:result, duplicates, global};
}
const issues = [];
function issue(file, code, message, severity = 'error') { issues.push({file,code,message,severity}); }
const nonempty = v => typeof v === 'string' && v.trim().length > 0;
function records(file, entries) {
  if (!Array.isArray(entries) || !entries.length) { issue(file,'empty','Expected nonempty array'); return; }
  const keys = new Set();
  entries.forEach((entry,index) => {
    const at = `record ${index+1}`;
    if (!entry || typeof entry !== 'object' || Array.isArray(entry)) { issue(file,'record',at); return; }
    const reading = file.startsWith('readingcomp');
    const fields = reading ? ['title','text'] : ['word','level','category','definition','example'];
    if (!reading && !file.startsWith('synant')) fields.push('pos');
    for (const field of fields) if (!nonempty(entry[field])) issue(file,'required',`${at}: ${field}`);
    const key = reading ? `${entry.level ?? entry.grade}:${entry.title}` : `${entry.level}:${entry.word}`;
    if (keys.has(key)) issue(file,'duplicate-record',`${at}: ${key}`); keys.add(key);
    const level = /^words([abc][12])/.exec(file)?.[1].toUpperCase();
    if (level && entry.level !== level) issue(file,'level',`${at}: expected ${level}, got ${entry.level}`);
    if (file.startsWith('synant') && !nonempty(entry.synonyms) && !nonempty(entry.antonyms)) issue(file,'relationships',`${at}: neither synonyms nor antonyms`);
    if (!reading && /no example sentence available|related to:/i.test(`${entry.definition} ${entry.example}`)) issue(file,'placeholder',at,'warning');
    if (reading) {
      if (file === 'readingcomp.js' ? !Number.isInteger(entry.grade) || entry.grade < 1 || entry.grade > 12 : !/^[ABC][12]$/.test(entry.level)) issue(file,'reading-level',at);
      if (!Array.isArray(entry.questions) || !entry.questions.length) issue(file,'questions',at);
      else entry.questions.forEach((q,n) => {
        if (!q || !nonempty(q.q) || !Array.isArray(q.options) || q.options.length < 2 || !q.options.every(nonempty) || !Number.isInteger(q.correct) || q.correct < 0 || q.correct >= q.options.length) issue(file,'question',`${at}, question ${n+1}`);
        else if (new Set(q.options).size !== q.options.length) issue(file,'duplicate-option',`${at}, question ${n+1}`);
      });
    }
  });
}
function selfTest() {
  const assert = require('node:assert/strict');
  assert.equal(parse(`/* [ */ window.T=[{word:'caf\\u00e9', text:'a'+/*x*/"b", n:2,},];`).value[0].text,'ab');
  assert.equal(parse(`window.T=[{word:'caf\\u00e9'}];`).value[0].word,'café');
  assert.deepEqual(parse(`window.T=[{word:'a', word:'b'}];`).duplicates,['$[0].word']);
  assert.equal(parse('{"a":1,"a":2}',true).duplicates.length,1);
  for (const bad of [`window.T=[{x:fetch('https://invalid')}];`, `window.T=[{x:1,,}];`, `window.T=[]; process.exit(0);`, `window.T=[...other];`]) assert.throws(() => parse(bad));
  records('wordsa1.js',[{word:'x',level:'A2'},{word:'x',level:'A2'}]);
  assert.ok(issues.some(x => x.code === 'required'));
  assert.ok(issues.some(x => x.code === 'duplicate-record'));
  assert.ok(issues.some(x => x.code === 'level'));
  records('readingcomp.js',[{grade:1,title:'t',text:'t',questions:[{q:'q',options:['a','b'],correct:2}]}]);
  assert.ok(issues.some(x => x.code === 'question'));
  return {schema:1,kind:'data-validator-self-test',status:'passed'};
}
function audit(root) {
  const manifestText = fs.readFileSync(path.join(root,'manifest.json'),'utf8').replace(/^\uFEFF/,'');
  const manifest = JSON.parse(manifestText); // strict JSON syntax, then duplicate detection
  for (const key of parse(manifestText,true).duplicates) issue('manifest.json','duplicate-property',key);
  if (manifest.schema !== 1 || !Array.isArray(manifest.files)) throw new Error('Unsupported manifest schema');
  const names = fs.readdirSync(root).filter(n => n.endsWith('.js')).sort();
  const expected = new Map(); let bytes = 0; let count = 0;
  for (const row of manifest.files) {
    if (typeof row.name !== 'string' || path.basename(row.name) !== row.name || !row.name.endsWith('.js')) { issue('manifest.json','path','Invalid dataset name'); continue; }
    if (expected.has(row.name)) issue('manifest.json','duplicate-file',row.name);
    expected.set(row.name,row);
    if (!names.includes(row.name)) issue(row.name,'missing-file','Listed in manifest but absent');
  }
  const globals = new Set();
  for (const name of names) {
    const full = path.join(root,name); const buffer = fs.readFileSync(full); bytes += buffer.length;
    const row = expected.get(name); const hash = crypto.createHash('sha256').update(buffer).digest('hex');
    if (!row) issue(name,'unlisted','Not in manifest');
    else {
      if (row.bytes !== buffer.length) issue(name,'bytes',`Manifest ${row.bytes}; actual ${buffer.length}`);
      if (typeof row.sha256 !== 'string' || row.sha256.toLowerCase() !== hash) issue(name,'hash',`SHA-256 mismatch; actual ${hash}`);
    }
    const checked = spawnSync(process.execPath,['--check',full],{encoding:'utf8',timeout:15000,windowsHide:true});
    if (checked.error || checked.status !== 0) { issue(name,'syntax',checked.error?.message || checked.stderr); continue; }
    try {
      const source = new TextDecoder('utf-8',{fatal:true}).decode(buffer);
      const parsed = parse(source);
      if (globals.has(parsed.global)) issue(name,'duplicate-global',parsed.global); globals.add(parsed.global);
      for (const key of parsed.duplicates) issue(name,'duplicate-property',key);
      records(name,parsed.value); count += parsed.value.length;
    } catch (e) { issue(name,'literal-parse',e.message); }
  }
  if (manifest.fileCount !== names.length || manifest.files.length !== names.length) issue('manifest.json','file-count',`Actual ${names.length}, declared ${manifest.fileCount}`);
  if (manifest.totalBytes !== bytes) issue('manifest.json','total-bytes',`Actual ${bytes}, declared ${manifest.totalBytes}`);
  const errors = issues.filter(i => i.severity === 'error').length;
  return {schema:1,kind:'data-quality',status:errors ? 'failed':'passed',files:names.length,records:count,errors,warnings:issues.length-errors,issues};
}
try {
  const result = process.argv[3] === 'self-test' ? selfTest() : audit(process.argv[2]);
  console.log(JSON.stringify(result)); process.exitCode = result.status === 'failed' ? 1 : 0;
} catch (e) { console.log(JSON.stringify({schema:1,kind:'data-quality',status:'error',message:e.message})); process.exitCode=2; }
'@
try {
  $OutputEncoding = [Text.UTF8Encoding]::new($false)
  [Console]::OutputEncoding = $OutputEncoding
    $node = (Get-Command node -CommandType Application -ErrorAction Stop).Source
  $version = & $node --version
  if ($LASTEXITCODE -ne 0 -or $version -notmatch '^v(\d+)\.' -or [int]$Matches[1] -lt 22) { throw 'Node.js 22 or newer is required.' }
    $arguments = @('-', [IO.Path]::GetFullPath($DataDirectory), $(if ($SelfTest) { 'self-test' } else { 'audit' }))
    $json = $validator | & $node @arguments
    $resultCode = $LASTEXITCODE
    if (-not $json) { throw 'Node returned no report.' }
    $text = $json -join [Environment]::NewLine
    $null = $text | ConvertFrom-Json
    if ($ReportPath) { [IO.File]::WriteAllText([IO.Path]::GetFullPath($ReportPath), $text, [Text.UTF8Encoding]::new($false)) }
    Write-Output $text
    exit $resultCode
}
catch {
    [ordered]@{ schema = 1; kind = 'data-quality'; status = 'error'; message = $_.Exception.Message } | ConvertTo-Json -Compress
    exit 2
}
finally {
  $OutputEncoding = $oldOutputEncoding
  [Console]::OutputEncoding = $oldConsoleEncoding
}