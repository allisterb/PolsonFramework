Stage.begin('Archetype');
Stage.note('Applying optical balance, bottom foot scoops, and refined hull curve to the master vector artwork.');

const paper = Snap(512, 512);
paper.rect(0, 0, 512, 512).attr({ fill: '#0F141D' });

// Refined SVG paths
const mainsailD = "M 160 360 C 130 240 180 130 255 95 C 210 190 200 290 235 365 C 195 375 175 372 160 360 Z";
const foresailD = "M 250 365 C 255 260 300 180 345 160 C 315 245 305 315 335 365 C 290 375 270 372 250 365 Z";
const hullD = "M 125 375 C 230 422 315 410 385 360 C 315 385 230 395 125 375 Z";

const g = paper.g().attr({ id: 'refined-mark' });

const m1 = paper.path(mainsailD).attr({ fill: '#D97757', id: 'mainsail' });
const m2 = paper.path(foresailD).attr({ fill: '#E8C59A', id: 'foresail' });
const m3 = paper.path(hullD).attr({ fill: '#FAF5EC', id: 'hull-wave' });

g.add(m1, m2, m3);

const b1 = Snap.path.getBBox(mainsailD);
const b2 = Snap.path.getBBox(foresailD);
const b3 = Snap.path.getBBox(hullD);
console.log('Mainsail BBox: ' + JSON.stringify(b1));
console.log('Foresail BBox: ' + JSON.stringify(b2));
console.log('Hull BBox: ' + JSON.stringify(b3));

paper;
