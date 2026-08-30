// Smoke test only — confirming the engine, globals, font availability and pixel readback
// respond before any design work starts. Not a design artifact.
const out = {};
out.globals = ['Stage', 'Scale', 'Layout', 'Css', 'Assets', 'Skia', 'Snap', 'Drawing', 'LogoType']
    .filter(n => typeof this[n] !== 'undefined');

// The neo-brutalist language needs a heavy grotesque. Find out what actually exists
// rather than naming one and letting Skia substitute silently.
const families = ['Arial Black', 'Impact', 'Anton', 'Archivo Black', 'Oswald', 'Bebas Neue',
                  'Helvetica', 'Arial', 'Inter', 'Inter Tight', 'Roboto', 'Roboto Condensed',
                  'DejaVu Sans', 'Barlow Condensed', 'Space Grotesk', 'Courier New', 'JetBrains Mono'];
out.fonts = families.filter(f => Skia.Font.has(f));

const canvas = createCanvas(60, 40);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#FF2D00';
ctx.fillRect(0, 0, 60, 40);
out.px = canvas.bitmap.getPixel(10, 10);

log(JSON.stringify(out, null, 1));
canvas;
