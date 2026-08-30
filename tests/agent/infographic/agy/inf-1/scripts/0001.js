log('Checking available fonts and environment...');
log('Skia fonts check:');
const fonts = ['Inter', 'Inter Tight', 'Helvetica', 'Arial', 'Roboto', 'Impact', 'Courier New', 'monospace', 'sans-serif'];
for (const f of fonts) {
  try {
    log(f + ': ' + (Skia && Skia.Font ? Skia.Font.has(f) : 'no Skia.Font.has'));
  } catch (e) {
    log(f + ' error: ' + e.message);
  }
}
log('Canvas and LogoType checks:');
log('typeof Scale: ' + typeof Scale);
log('typeof Layout: ' + typeof Layout);
log('typeof Css: ' + typeof Css);
log('typeof LogoType: ' + typeof LogoType);
