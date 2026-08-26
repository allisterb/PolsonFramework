/**
 * Nexus Dynamics - Vector Brand Mark System
 * Built with Polson VectorLogoToolkit & Snap.svg
 */

const s = Snap(800, 800);

// --- 1. Defs & Gradients ---
const bgGrad = s.gradient('l(0, 0, 1, 1)#0f172a-#1e293b');
const primaryGrad = s.gradient('l(0, 0, 1, 1)#4f46e5-#06b6d4');
const secondaryGrad = s.gradient('l(0, 1, 1, 0)#3b82f6-#06b6d4');

// --- 2. Construction Grid Layer (Non-repro geometric guides) ---
const guidesGroup = s.g().attr({ id: 'construction-guides', opacity: 0.25 });
const isoGrid = s.isometricGrid(800, 800, 50);
const polarGrid = s.polarGrid(400, 400, 300, 5, 12);
const goldenCircles = s.goldenCircles(400, 400, 240, 5);
guidesGroup.append(isoGrid);
guidesGroup.append(polarGrid);
guidesGroup.append(goldenCircles);

// --- 3. Container Squircle (App Icon Badge) ---
const squircleContainer = s.squircle(150, 150, 500, 500, 4.5).attr({
    id: 'app-squircle-badge',
    fill: bgGrad,
    stroke: '#334155',
    strokeWidth: 2
});

// --- 4. Hero Mark Geometry ---
const markGroup = s.g().attr({ id: 'nexus-hero-mark' });

// Sweeping Upper Loop Ribbon (SVG Path)
const upperRibbon = s.path(
    'M 280 370 ' +
    'A 90 90 0 0 1 400 280 ' +
    'L 520 430 ' +
    'A 90 90 0 0 1 400 520 ' +
    'Z'
).attr({
    id: 'upper-ribbon',
    fill: primaryGrad
});

// Interlocking Lower Cross-Ribbon
const crossRibbon = s.path(
    'M 430 280 ' +
    'A 90 90 0 0 1 520 400 ' +
    'L 370 520 ' +
    'A 90 90 0 0 1 280 400 ' +
    'Z'
).attr({
    id: 'cross-ribbon',
    fill: secondaryGrad,
    opacity: 0.95
});

// Central Negative Space Diamond Core
const diamondCore = s.path(
    'M 400 360 ' +
    'L 440 400 ' +
    'L 400 440 ' +
    'L 360 400 ' +
    'Z'
).attr({
    id: 'negative-space-core',
    fill: '#0f172a'
});

markGroup.append(upperRibbon);
markGroup.append(crossRibbon);
markGroup.append(diamondCore);

// --- 5. Protective Clear Space Guide ---
const clearSpace = s.clearSpaceGuide(150, 150, 500, 500, 40).attr({
    id: 'clear-space-boundary',
    opacity: 0.6
});

s;
