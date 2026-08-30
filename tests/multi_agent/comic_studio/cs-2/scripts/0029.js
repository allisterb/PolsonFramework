Stage.begin('Critic');
const p = Skia.Image.load('artifacts/_sksl_probe.webp');
log('left ' + p.getPixel(20, 60) + '   mid ' + p.getPixel(150, 60) + '   right ' + p.getPixel(280, 60));
log('expected left ~#e64033, mid ~#8c6c8c, right ~#338cf2 (rgb ramp), and NOT uniform');
exit('probe read');
