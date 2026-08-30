// Archive copy: the prior run's stage-2 colorist render (out of scope for this brief, kept as evidence).
const img = Skia.Image.load('artifacts/stage2_colorist.webp');
log('stage2 source: ' + img.width + ' x ' + img.height);
const c = createCanvas(img.width, img.height);
const ctx = c.getContext('2d');
ctx.drawImage(img, 0, 0);
c;