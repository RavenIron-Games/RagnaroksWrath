// Proves a config migration by NAME: compares the file an older build wrote with the pair of files
// the migration left, key by key, and reports every value that changed, went missing, or appeared.
// It is how 0.28.0's layout move was verified on real BepInEx (146 of 146 values on two server installs and a client).
// The retired list below is 0.28.0's; extend it when a later layout retires more.
//
//   node tools/config-guide/check-migrated.js <old.cfg> <new main.cfg> <new advanced.cfg>
//
// Expect "value changed: 0", "missing: 0", "keys not in v1: none" and the new stamp for a clean run.
const fs = require('fs');
function parse(p) {
  const out = []; let section = null;
  for (const raw of fs.readFileSync(p, 'utf8').split(/\r?\n/)) {
    const line = raw.trim();
    if (!line || line.startsWith('#')) continue;
    const m = line.match(/^\[(.*)\]$/);
    if (m) { section = m[1]; continue; }
    const eq = line.indexOf('=');
    if (eq < 0) continue;
    out.push({ section, key: line.slice(0, eq).trim(), value: line.slice(eq + 1).trim() });
  }
  return out;
}
const [v1p, mainp, advp] = process.argv.slice(2);
const v1 = parse(v1p), main = parse(mainp), adv = parse(advp);
const retired = new Set(['StormFireRiskMultiplier', 'StormWindMultiplier']);
const now = new Map();
let dupes = 0;
for (const [file, list] of [['main', main], ['advanced', adv]])
  for (const e of list) { if (now.has(e.key)) dupes++; now.set(e.key, { ...e, file }); }
let carried = 0, wrong = [], missing = [], retiredStill = [];
for (const e of v1) {
  if (e.key === 'ConfigVersion') continue;
  const n = now.get(e.key);
  if (retired.has(e.key)) { if (n) retiredStill.push(e.key); continue; }
  if (!n) { missing.push(`${e.section}::${e.key}`); continue; }
  if (n.value !== e.value) wrong.push(`${e.key}: v1 '${e.value}' -> ${n.file} '${n.value}'`);
  else carried++;
}
const v1keys = new Set(v1.map(e => e.key));
const extra = [...now.keys()].filter(k => !v1keys.has(k));
const oldSections = new Set(v1.map(e => e.section));
const newSections = new Set([...main, ...adv].map(e => e.section));
const lingering = [...newSections].filter(s => oldSections.has(s));
console.log(`v1 lines: ${v1.length}; main keys: ${main.length}; advanced keys: ${adv.length}; duplicate keys across files: ${dupes}`);
console.log(`carried with identical value: ${carried}`);
console.log(`value changed: ${wrong.length}${wrong.length ? '\n  ' + wrong.join('\n  ') : ''}`);
console.log(`missing: ${missing.length}${missing.length ? '\n  ' + missing.join('\n  ') : ''}`);
console.log(`retired still present: ${retiredStill.length ? retiredStill.join(', ') : 'none'}`);
console.log(`keys not in v1: ${extra.join(', ') || 'none'}`);
console.log(`stamp: ${(now.get('ConfigVersion') || {}).value} in [${(now.get('ConfigVersion') || {}).section}] of ${(now.get('ConfigVersion') || {}).file}`);
console.log(`section names shared with v1: ${lingering.join(', ') || 'none'}`);
console.log(`main sections: ${[...new Set(main.map(e => e.section))].join(' | ')}`);
console.log(`advanced sections: ${[...new Set(adv.map(e => e.section))].join(' | ')}`);
