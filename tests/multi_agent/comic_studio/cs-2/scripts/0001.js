Stage.begin('Penciler');
Stage.note('Probing the reference bitmap before measuring: confirm Skia.Image.load reads the project-relative reference, and learn what bitmap.getPixel returns so I can classify colour regions numerically rather than by eye.');

const ref = Skia.Image.load('reference_images/comic1.png');
log(`ref dims: ${ref.width} x ${ref.height}`);

// Learn the return shape of getPixel at a few known-different points.
const probes = [
  ['corner/bg', 10, 10],
  ['face', 590, 190],
  ['cape red', 300, 600],
  ['suit blue', 590, 430],
  ['hair dark', 600, 90]
];
for (const [name, x, y] of probes) {
  const p = ref.getPixel(x, y);
  log(`${name} (${x},${y}) -> typeof=${typeof p} str=${String(p)} json=${JSON.stringify(p)}`);
}
exit('probe complete');
