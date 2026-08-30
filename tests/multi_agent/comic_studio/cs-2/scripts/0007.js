Stage.begin('Penciler');
const fams = Skia.Font.families();
log(`font families (${fams.length}): ` + Array.from(fams).slice(0, 40).join(', '));
for (const f of ['sans-serif', 'Arial', 'Segoe UI', 'Consolas', 'Verdana', 'Tahoma']) log(`has ${f}: ${Skia.Font.has(f)}`);

// Canonical Loomis head at the measured head box, to compare against observation.
const h = Drawing.createLoomisHead(600, 64, 208, 0, 0);
log('loomis keys: ' + Object.keys(h).join(', '));
log(JSON.stringify({ origin: h.origin, crown: h.crown, hairline: h.hairline, brow: h.brow,
    eyeLineY: h.eyeLineY, noseBase: h.noseBase, mouthCenter: h.mouthCenter, chin: h.chin,
    unit: h.unit, temporalOval: h.temporalOval }));
log('nearEye: ' + JSON.stringify(h.nearEye) + '  farEye: ' + JSON.stringify(h.farEye));
log('jaw: ' + JSON.stringify(h.jaw));
exit('probe');
