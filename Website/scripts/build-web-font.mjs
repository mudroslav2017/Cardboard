import fs from 'node:fs/promises';
import assert from 'node:assert/strict';

const original = await fs.readFile(new URL('../dist/assets/troublemarker.otf', import.meta.url));
const output = Buffer.from(original);
const tables = new Map();
for (let i = 0; i < output.readUInt16BE(4); i++) {
  const record = 12 + i * 16;
  const name = output.toString('ascii', record, record + 4);
  tables.set(name, {record, offset:output.readUInt32BE(record + 8), length:output.readUInt32BE(record + 12)});
}
const head = tables.get('head');
const hhea = tables.get('hhea');
const os2 = tables.get('OS/2');
assert.ok(head && hhea && os2, 'Expected OpenType metrics tables');
const ascent = output.readUInt16BE(os2.offset + 74);
const descent = output.readUInt16BE(os2.offset + 76);
assert.ok(ascent > 0 && ascent < 32768 && descent < 32768);

// Preserve the glyphs and the existing Windows metrics. CoreText and browsers
// that use hhea/typo metrics should reserve the same vertical ink space.
output.writeInt16BE(ascent, hhea.offset + 4);
output.writeInt16BE(-descent, hhea.offset + 6);
output.writeInt16BE(0, hhea.offset + 8);
output.writeInt16BE(ascent, os2.offset + 68);
output.writeInt16BE(-descent, os2.offset + 70);
output.writeInt16BE(0, os2.offset + 72);
if (output.readUInt16BE(os2.offset) >= 4) {
  output.writeUInt16BE(output.readUInt16BE(os2.offset + 62) | 0x80, os2.offset + 62);
}

function checksum(bytes, offset = 0, length = bytes.length) {
  let sum = 0;
  for (let i = 0; i < length; i += 4) {
    let word = 0;
    for (let j = 0; j < 4; j++) word = ((word << 8) | (i + j < length ? bytes[offset + i + j] : 0)) >>> 0;
    sum = (sum + word) >>> 0;
  }
  return sum;
}
output.writeUInt32BE(0, head.offset + 8);
for (const table of tables.values()) {
  output.writeUInt32BE(checksum(output, table.offset, table.length), table.record + 4);
}
output.writeUInt32BE((0xb1b0afba - checksum(output)) >>> 0, head.offset + 8);
assert.equal(checksum(output), 0xb1b0afba, 'OpenType checksum must remain valid');
for (const [name, table] of tables) {
  if (['head', 'hhea', 'OS/2'].includes(name)) continue;
  assert.ok(original.subarray(table.offset, table.offset + table.length)
    .equals(output.subarray(table.offset, table.offset + table.length)), `${name} must stay unchanged`);
}
await fs.writeFile(new URL('../dist/assets/troublemarker-web.otf', import.meta.url), output);
console.log(JSON.stringify({ascent, descent, glyphsUnchanged:true, checksumValid:true}));
