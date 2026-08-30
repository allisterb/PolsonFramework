// Smoke test: confirm the engine, Stage, Scale, Skia.Font and pixel readback all respond
// before committing to the project. Nothing here is design work.
const out = {};
out.hasStage = typeof Stage !== 'undefined';
out.hasScale = typeof Scale !== 'undefined';
out.hasAssets = typeof Assets !== 'undefined';

// A heavy grotesque is the spine of the neo-brutalist language — check what exists.
const families = ['Arial Black', 'Impact', 'Helvetica', 'Arial', 'Inter', 'Roboto', 'DejaVu Sans', 'Archivo Black', 'Oswald'];
out.fonts = families.filter(f => Skia.Font.has(f));

const canvas = Skia.createCanvas(60, 40);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#FF2D00';
ctx.fillRect(0, 0, 60, 40);
out.px = canvas.bitmap.getPixel(10, 10);

log(JSON.stringify(out));
canvas;
