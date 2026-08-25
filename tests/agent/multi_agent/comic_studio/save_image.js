const fs = require('fs');
const b64Path = process.argv[2];
const outPath = process.argv[3];
let b64 = fs.readFileSync(b64Path, 'utf8').trim();
if (b64.includes('base64,')) {
    b64 = b64.split('base64,')[1];
}
const buf = Buffer.from(b64, 'base64');
fs.writeFileSync(outPath, buf);
console.log(`Saved ${outPath} (${buf.length} bytes)`);
