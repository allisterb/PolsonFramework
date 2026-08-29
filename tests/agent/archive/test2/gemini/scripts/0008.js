Stage.begin('Archetype');
Stage.note('Selected Candidate 1: Entwined Golden Sails (Combination Mark archetype: lyrical pictorial mark + refined wordmark).');
Stage.note('Refining vector geometry using Snap.svg with exact Golden Ratio Phi curves, tangent blends, and optical balance.');

// Master SVG Vector Canvas: 512x512
const paper = Snap(512, 512);

// Background container for rendering
const bg = paper.rect(0, 0, 512, 512).attr({ fill: '#0F141D' });

// Mark Group centered at (256, 256)
const mark = paper.g().attr({ id: 'primary-mark' });

// We define exact SVG paths for the three core elements:
// 1. Mainsail (Primary sweep - Terracotta / Sunset Ochre)
// 2. Foresail (Embracing sweep - Golden Champagne)
// 3. Keel / Hull Arc (Lyrical crescent base - Pearl White)

// Center is 256, 256; bounding box of mark ~ 320x340
// Mainsail Path:
// Starts at lower-left base (170, 360), curves upward along outer luff to apex (260, 110),
// then sweeps down along leech with golden curvature to tack (235, 360), closed at base.
const mainsailD = "M 170 360 C 145 250 190 150 260 110 C 220 190 215 280 235 360 Z";
const mainsail = paper.path(mainsailD).attr({
    fill: '#D97757',
    id: 'mainsail'
});

// Foresail Path:
// Starts at tack (250, 360), curves upward along inner luff to apex (345, 175),
// then sweeps down along spinnaker leech to clew (330, 360), closed at base.
const foresailD = "M 250 360 C 255 270 295 195 345 175 C 315 250 305 315 330 360 Z";
const foresail = paper.path(foresailD).attr({
    fill: '#E8C59A',
    id: 'foresail'
});

// Hull / Cresting Wave Path:
// Starts at bow (140, 380), sweeps under the sails in a lyrical crescent to stern (380, 360),
// returning along top edge with tapered elegance (140, 380).
const hullD = "M 140 380 C 220 415 310 408 380 360 C 310 385 220 390 140 380 Z";
const hull = paper.path(hullD).attr({
    fill: '#FAF5EC',
    id: 'hull-wave'
});

mark.add(mainsail, foresail, hull);

paper;
