const fs = require('fs');
const inPath = process.argv[2];
const outPath = process.argv[3];
let text = fs.readFileSync(inPath, 'utf8').trim();
let b64 = text;
try {
    const obj = JSON.parse(text);
    b64 = obj.imageBytes || obj.imageDataUri || text;
} catch (e) {}

if (b64.includes('base64,')) {
    b64 = b64.split('base64,')[1];
}
b64 = b64.replace(/[\r\n"']/g, '').trim();
const buf = Buffer.from(b64, 'base64');
fs.writeFileSync(outPath, buf);
console.log(`Saved ${outPath} (${buf.length} bytes)`);

