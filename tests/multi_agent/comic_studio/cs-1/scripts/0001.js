// Archive copy: re-encode the prior run's stage-1 sheet into archive/.
// No filesystem copy is exposed to scripts, so this round-trips through a canvas.
const img = Skia.Image.load('artifacts/stage1_penciler.webp');
log('stage1 source: ' + img.width + ' x ' + img.height);
const c = createCanvas(img.width, img.height);
const ctx = c.getContext('2d');
ctx.drawImage(img, 0, 0);
c;