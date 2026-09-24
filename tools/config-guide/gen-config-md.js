// Builds docs/CONFIG.md from the two config files a real boot of the mod wrote (descriptions,
// types, defaults and ranges exactly as BepInEx prints them; the VALUES in them are ignored, so any
// server's files will do) plus the longer guide texts and client-read flags in guides.json and the
// section intros in config-intros.json. Taking the text from a real boot rather than from
// ModConfig.cs is the point: it is what an owner actually sees in the file.
//
//   node tools/config-guide/gen-config-md.js <cfg dir>/com.raveniron.ragnarokswrath.cfg \
//        <cfg dir>/com.raveniron.ragnarokswrath.advanced.cfg docs/CONFIG.md
//
// Add `--list` instead of the output path to print the keys per section and file. A section with
// no intro in config-intros.json fails the run, so a new section cannot ship undocumented.
const fs = require('fs');
const path = require('path');
const [mainP, advP, outP, flag] = process.argv.slice(2);
const guides = require(path.join(__dirname, 'guides.json'));

function parse(p) {
  const sections = []; let cur = null; let pending = { desc: [], type: null, def: null, range: null };
  for (const raw of fs.readFileSync(p, 'utf8').split(/\r?\n/)) {
    const line = raw.trim();
    if (!line) continue;
    let m;
    if ((m = line.match(/^\[(.*)\]$/))) { cur = { name: m[1], entries: [] }; sections.push(cur); continue; }
    if (line.startsWith('## ')) { if (cur) pending.desc.push(line.slice(3)); continue; }
    if ((m = line.match(/^# Setting type: (.*)$/))) { pending.type = m[1]; continue; }
    if ((m = line.match(/^# Default value: ?(.*)$/))) { pending.def = m[1]; continue; }
    if ((m = line.match(/^# Acceptable value range: From (.*) to (.*)$/))) { pending.range = [m[1], m[2]]; continue; }
    if (line.startsWith('#')) continue;
    const eq = line.indexOf('=');
    if (eq < 0 || !cur) continue;
    cur.entries.push({ key: line.slice(0, eq).trim(), desc: pending.desc.join(' '), type: pending.type, def: pending.def, range: pending.range });
    pending = { desc: [], type: null, def: null, range: null };
  }
  return sections;
}

const main = parse(mainP), adv = parse(advP);
if (flag === '--list') {
  for (const s of main) console.log(`${s.name} MAIN: ${s.entries.map(e => e.key).join(', ')}`);
  for (const s of adv) console.log(`${s.name} ADV: ${s.entries.map(e => e.key).join(', ')}`);
  const g = Object.entries(guides.guide).filter(([, v]) => v && v.trim()).length;
  console.log('guides with text: ' + g);
  process.exit(0);
}

const intros = require(path.join(__dirname, 'config-intros.json'));
const typeName = { Boolean: 'on/off', Single: 'number', Int32: 'whole number', String: 'text' };
const selfSaysClient = d => /player'?s'? own game|every player|players' own games|on each player|^Client-side/i.test(d);

function entryMd(e) {
  const bits = [];
  bits.push(`**\`${e.key}\`**`);
  const meta = [];
  meta.push(typeName[e.type] || e.type || '');
  meta.push('default `' + (e.def === '' ? '(empty)' : e.def) + '`');
  if (e.range) meta.push(`${e.range[0]} to ${e.range[1]}`);
  if (guides.client[e.key] && !selfSaysClient(e.desc)) meta.push('read on each player\'s own game');
  let out = `- ${bits.join(' ')} (${meta.filter(Boolean).join(', ')}): ${e.desc}`;
  const g = guides.guide[e.key];
  if (g && g.trim()) out += ` ${g.trim()}`;
  return out;
}

const lines = [];
const P = s => lines.push(s);
P('# Configuring Ragnarok\'s Wrath');
P('');
P(intros._top.trim());
P('');
const byName = new Map();
for (const s of main) byName.set(s.name, { main: s.entries, adv: [] });
for (const s of adv) { if (!byName.has(s.name)) byName.set(s.name, { main: [], adv: [] }); byName.get(s.name).adv = s.entries; }
const names = [...byName.keys()].filter(n => n !== 'Meta').sort();
P('## Sections');
P('');
for (const n of names) P(`- [${n}](#${n.toLowerCase().replace(/[^a-z0-9 -]/g, '').replace(/ /g, '-')})`);
P('');
for (const n of names) {
  const s = byName.get(n);
  P(`## ${n}`);
  P('');
  if (!intros[n]) { console.error('no intro for ' + n); process.exit(1); }
  P(intros[n].trim());
  P('');
  if (s.main.length) {
    P(`In \`com.raveniron.ragnarokswrath.cfg\`:`);
    P('');
    for (const e of s.main) P(entryMd(e));
    P('');
  }
  if (s.adv.length) {
    P(`In \`com.raveniron.ragnarokswrath.advanced.cfg\`:`);
    P('');
    for (const e of s.adv) P(entryMd(e));
    P('');
  }
}
P('## Meta');
P('');
P(intros.Meta.trim());
P('');
fs.writeFileSync(outP, lines.join('\n'));
console.log(`wrote ${outP}: ${names.length} sections, ${main.reduce((a, s) => a + s.entries.length, 0)} main keys, ${adv.reduce((a, s) => a + s.entries.length, 0)} advanced keys`);
