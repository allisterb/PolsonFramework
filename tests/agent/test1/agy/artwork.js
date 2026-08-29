// artwork.js — Master script for "Wooden Sailboat at Night"
// Produces output.webp and output.svg

Stage.begin('Presentation');
Stage.note('Final master production script for wooden sailboat under starry night sky.');

const width = 1600;
const height = 1000;
const horizonY = 570;
const moonPos = { x: 380, y: 190 };

// --- 1. SNAP VECTOR LAYER (For output.svg vector deliverable) ---
const paper = Snap(width, height);

// Vector Background & Sky/Sea Gradients
paper.rect(0, 0, width, height).attr({ fill: '#030712' });
const skyVGrad = paper.gradient('l(0,0,0,1)#02040a-#071024:#0b1936:#112240');
paper.rect(0, 0, width, horizonY).attr({ fill: skyVGrad });

const seaVGrad = paper.gradient('l(0,0,0,1)#081426-#050d1b:#030711:#010307');
paper.rect(0, horizonY, width, height - horizonY).attr({ fill: seaVGrad });

// Vector Moon & Halo
paper.circle(moonPos.x, moonPos.y, 160).attr({
    fill: '#4a7db8',
    opacity: 0.15
});
paper.circle(moonPos.x, moonPos.y, 46).attr({
    fill: '#e8f4ff',
    stroke: '#ffffff',
    'stroke-width': 1.5
});

// Vector Boat Group
const boatGroup = paper.g();
boatGroup.attr({
    transform: `translate(1010, 640) rotate(-4.3)`
});

// Vector Hull
const hullPathD = "M-250,-10 C-110,2 90,-8 230,-50 L205,20 C100,58 -100,55 -235,38 Z";
boatGroup.path(hullPathD).attr({
    fill: '#452e1b',
    stroke: '#1a1007',
    'stroke-width': 2
});

// Vector Mast & Spars
boatGroup.path("M-87,-480 L-65,-5 L-53,-5 L-83,-480 Z").attr({ fill: '#6e4c2c' });
boatGroup.path("M-60,-58 L-288,-78 L-288,-71 L-60,-51 Z").attr({ fill: '#5a3d24' });
boatGroup.path("M215,-45 L385,-92 L385,-86 L215,-39 Z").attr({ fill: '#5a3d24' });

// Vector Sails
const mainSailD = "M-73,-365 L-220,-430 C-310,-280 -330,-170 -285,-78 C-205,-68 -135,-60 -60,-65 Z";
boatGroup.path(mainSailD).attr({
    fill: '#dcebff',
    stroke: '#b8d6f8',
    'stroke-width': 1.5,
    opacity: 0.92
});

const jibD = "M-85,-465 C75,-300 235,-200 370,-90 C220,-72 90,-76 -40,-85 C-55,-210 -70,-330 -85,-465 Z";
boatGroup.path(jibD).attr({
    fill: '#edf6ff',
    stroke: '#cde4ff',
    'stroke-width': 1.5,
    opacity: 0.95
});


// --- 2. CANVAS2D MASTER RASTER LAYER (Volumetric Rendering, Shaders, Lighting) ---
const canvas = createCanvas(width, height);
const ctx = canvas.getContext('2d');

// 1. SKY & CELESTIAL BACKGROUND
const skyGrad = ctx.createLinearGradient(0, 0, 0, horizonY);
skyGrad.addColorStop(0, '#010308');
skyGrad.addColorStop(0.35, '#050a16');
skyGrad.addColorStop(0.7, '#0a162c');
skyGrad.addColorStop(1.0, '#102242');
ctx.fillStyle = skyGrad;
ctx.fillRect(0, 0, width, horizonY);

// Atmospheric clouds
const cloudShader = Drawing.createAtmosphericCloudShader(0.0025, 0.0018, 3, 108);
ctx.save();
ctx.globalAlpha = 0.16;
ctx.fillStyle = cloudShader;
ctx.fillRect(0, 0, width, horizonY);
ctx.restore();

// Starfield with organic distribution
const prng = (s) => {
    let x = Math.sin(s++) * 10000;
    return x - Math.floor(x);
};

ctx.save();
for (let i = 0; i < 360; i++) {
    const sx = prng(i * 3 + 1) * width;
    const sy = prng(i * 3 + 2) * (horizonY - 15);
    const dMoon = Math.hypot(sx - moonPos.x, sy - moonPos.y);
    if (dMoon < 85) continue; // Clear zone around moon disk

    const randVal = prng(i * 5 + 3);
    let r = 0.6;
    let alpha = 0.35 + randVal * 0.45;

    if (randVal > 0.92) {
        r = 1.6 + prng(i * 7) * 0.8;
        alpha = 0.95;
        const starHalo = ctx.createRadialGradient(sx, sy, 0, sx, sy, r * 4.0);
        starHalo.addColorStop(0, `rgba(240, 248, 255, ${alpha})`);
        starHalo.addColorStop(0.4, 'rgba(180, 215, 255, 0.3)');
        starHalo.addColorStop(1, 'rgba(180, 215, 255, 0)');
        ctx.fillStyle = starHalo;
        ctx.beginPath();
        ctx.arc(sx, sy, r * 4.0, 0, Math.PI * 2);
        ctx.fill();
    } else if (randVal > 0.65) {
        r = 1.0 + prng(i * 7) * 0.5;
        alpha = 0.75;
    }

    ctx.fillStyle = `rgba(230, 242, 255, ${alpha})`;
    ctx.beginPath();
    ctx.arc(sx, sy, r, 0, Math.PI * 2);
    ctx.fill();
}
ctx.restore();

// Moon Corona & Halo
const moonHalo = ctx.createRadialGradient(moonPos.x, moonPos.y, 40, moonPos.x, moonPos.y, 340);
moonHalo.addColorStop(0, 'rgba(215, 238, 255, 0.45)');
moonHalo.addColorStop(0.25, 'rgba(160, 205, 255, 0.18)');
moonHalo.addColorStop(0.65, 'rgba(90, 150, 230, 0.05)');
moonHalo.addColorStop(1, 'rgba(90, 150, 230, 0)');
ctx.fillStyle = moonHalo;
ctx.beginPath();
ctx.arc(moonPos.x, moonPos.y, 340, 0, Math.PI * 2);
ctx.fill();

// Moon Disc with 3D Spherical Shading & Craters
const moonDisc = ctx.createRadialGradient(moonPos.x - 14, moonPos.y - 14, 6, moonPos.x, moonPos.y, 46);
moonDisc.addColorStop(0, '#ffffff');
moonDisc.addColorStop(0.3, '#f5faff');
moonDisc.addColorStop(0.7, '#c2dbfa');
moonDisc.addColorStop(1, '#81aadb');

ctx.save();
ctx.shadowColor = 'rgba(220, 242, 255, 0.95)';
ctx.shadowBlur = 28;
ctx.fillStyle = moonDisc;
ctx.beginPath();
ctx.arc(moonPos.x, moonPos.y, 46, 0, Math.PI * 2);
ctx.fill();
ctx.restore();

// Lunar Maria Craters
ctx.fillStyle = 'rgba(105, 138, 178, 0.22)';
ctx.beginPath();
ctx.arc(moonPos.x - 14, moonPos.y + 8, 12, 0, Math.PI * 2);
ctx.arc(moonPos.x + 15, moonPos.y - 9, 14, 0, Math.PI * 2);
ctx.arc(moonPos.x + 18, moonPos.y + 15, 9, 0, Math.PI * 2);
ctx.arc(moonPos.x - 8, moonPos.y - 18, 8, 0, Math.PI * 2);
ctx.arc(moonPos.x + 2, moonPos.y + 22, 7, 0, Math.PI * 2);
ctx.fill();

// 2. OCEAN & MOONBEAM GLITTER TRAIL
const seaGrad = ctx.createLinearGradient(0, horizonY, 0, height);
seaGrad.addColorStop(0, '#09152a');
seaGrad.addColorStop(0.3, '#050d1b');
seaGrad.addColorStop(0.7, '#030711');
seaGrad.addColorStop(1.0, '#010307');
ctx.fillStyle = seaGrad;
ctx.fillRect(0, horizonY, width, height - horizonY);

// Perspective Waves & Moonlight Reflections
ctx.save();
for (let y = horizonY + 1; y < height; y += 3) {
    const depth = (y - horizonY) / (height - horizonY);
    const waveAmp = 0.6 + depth * 5.5;
    const waveFreq = 0.045 - depth * 0.032;

    // Wave body layer
    ctx.beginPath();
    ctx.moveTo(0, y);
    for (let x = 0; x <= width; x += 25) {
        const yOff = Math.sin(x * waveFreq + y * 0.09) * waveAmp;
        ctx.lineTo(x, y + yOff);
    }
    ctx.lineTo(width, height);
    ctx.lineTo(0, height);
    ctx.closePath();

    ctx.fillStyle = `rgba(18, 42, 78, ${0.07 + (1.0 - depth) * 0.12})`;
    ctx.fill();

    // Ambient wave crest highlights across entire sea
    ctx.strokeStyle = `rgba(80, 130, 190, ${0.08 + depth * 0.15})`;
    ctx.lineWidth = 0.6 + depth * 1.2;
    for (let x = 20; x < width; x += 90 + (1.0 - depth) * 60) {
        const xOff = (prng(x * 7 + y * 3) - 0.5) * 40;
        const len = 15 + depth * 45 * prng(x * 13 + y);
        ctx.beginPath();
        ctx.moveTo(x + xOff, y);
        ctx.lineTo(x + xOff + len, y + 0.5);
        ctx.stroke();
    }

    // Moonlight specular glitter trail
    const beamWidth = 75 + depth * 390;
    const glitterXMin = moonPos.x - beamWidth * 0.55;
    const glitterXMax = moonPos.x + beamWidth * 0.55;

    for (let gx = glitterXMin; gx <= glitterXMax; gx += 12 + depth * 22) {
        const gOff = (prng(gx * 17 + y * 11) - 0.5) * 16;
        const gLen = 5 + depth * 32 * prng(gx * 5 + y * 7);
        const distFromCenter = Math.abs(gx - moonPos.x) / (beamWidth * 0.55);
        const gAlpha = (1.0 - distFromCenter) * (0.25 + depth * 0.65);

        ctx.strokeStyle = `rgba(225, 242, 255, ${gAlpha})`;
        ctx.lineWidth = 0.8 + depth * 2.0;
        ctx.beginPath();
        ctx.moveTo(gx + gOff, y);
        ctx.lineTo(gx + gOff + gLen, y + 0.4);
        ctx.stroke();
    }
}
ctx.restore();

// 3. WOODEN SAILBOAT HERO
const boatCenter = { x: 1010, y: 640 };

ctx.save();
ctx.translate(boatCenter.x, boatCenter.y);
ctx.rotate(-0.075); // ~ -4.3 degrees heel under the breeze

// Water shadow & reflection under boat
ctx.save();
ctx.rotate(0.075);
const waterReflectGrad = ctx.createRadialGradient(0, 35, 20, 0, 45, 260);
waterReflectGrad.addColorStop(0, 'rgba(2, 5, 12, 0.9)');
waterReflectGrad.addColorStop(0.4, 'rgba(3, 8, 18, 0.55)');
waterReflectGrad.addColorStop(0.8, 'rgba(8, 20, 40, 0.2)');
waterReflectGrad.addColorStop(1, 'rgba(8, 20, 40, 0)');
ctx.fillStyle = waterReflectGrad;
ctx.beginPath();
ctx.ellipse(-30, 40, 240, 50, -0.05, 0, Math.PI * 2);
ctx.fill();

// Shimmering reflection of white sails in the water
for (let ry = 25; ry < 130; ry += 6) {
    const rDepth = (ry - 25) / 105;
    const rAlpha = (1.0 - rDepth) * 0.24;
    const rWidth = 145 * (1.0 - rDepth * 0.5);
    ctx.strokeStyle = `rgba(190, 220, 255, ${rAlpha})`;
    ctx.lineWidth = 2.0;
    ctx.beginPath();
    const rOff = (prng(ry * 13) - 0.5) * 20;
    ctx.moveTo(-110 + rOff, ry);
    ctx.lineTo(-110 + rOff + rWidth, ry);
    ctx.stroke();
}
ctx.restore();

// A. Interior Cockpit Deck
ctx.beginPath();
ctx.moveTo(-240, -12); // Stern port
ctx.bezierCurveTo(-140, 5, 100, -8, 230, -50); // Starboard sheer
ctx.bezierCurveTo(170, -28, -40, -2, -220, -4); // Port sheer
ctx.closePath();
ctx.fillStyle = '#1e140c';
ctx.fill();

// Deck plank lines
ctx.strokeStyle = 'rgba(10, 6, 2, 0.6)';
ctx.lineWidth = 0.8;
for (let d = -200; d < 200; d += 25) {
    ctx.beginPath();
    ctx.moveTo(d, -15);
    ctx.lineTo(d - 15, 0);
    ctx.stroke();
}

// B. Volumetric Wooden Hull Planking
const strakes = 8;
for (let s = 0; s < strakes; s++) {
    const t0 = s / strakes;
    const t1 = (s + 1) / strakes;

    const y0Stern = -12 + t0 * 48;
    const y0Mid = 2 + t0 * 62;
    const y0Bow = -50 + t0 * 48;

    const y1Stern = -12 + t1 * 48;
    const y1Mid = 2 + t1 * 62;
    const y1Bow = -50 + t1 * 48;

    ctx.beginPath();
    ctx.moveTo(-250 + t0 * 18, y0Stern);
    ctx.bezierCurveTo(-110, y0Mid, 90, y0Mid - 10, 230 - t0 * 12, y0Bow);
    ctx.lineTo(230 - t1 * 12, y1Bow);
    ctx.bezierCurveTo(90, y1Mid - 10, -110, y1Mid, -250 + t1 * 18, y1Stern);
    ctx.closePath();

    const strakeGrad = ctx.createLinearGradient(-250, -50, 230, 70);
    if (s === 0) {
        strakeGrad.addColorStop(0, '#583c24');
        strakeGrad.addColorStop(0.3, '#7d5634');
        strakeGrad.addColorStop(0.7, '#452e1c');
        strakeGrad.addColorStop(1, '#2b1b10');
    } else if (s < 4) {
        strakeGrad.addColorStop(0, '#3c2818');
        strakeGrad.addColorStop(0.4, '#563820');
        strakeGrad.addColorStop(0.8, '#301d10');
        strakeGrad.addColorStop(1, '#1a1109');
    } else {
        strakeGrad.addColorStop(0, '#24180f');
        strakeGrad.addColorStop(0.4, '#322014');
        strakeGrad.addColorStop(0.7, '#151d2a');
        strakeGrad.addColorStop(1, '#0c121b');
    }
    ctx.fillStyle = strakeGrad;
    ctx.fill();

    ctx.strokeStyle = 'rgba(12, 8, 4, 0.75)';
    ctx.lineWidth = 1.0;
    ctx.stroke();
}

// Gunwale Highlight
ctx.beginPath();
ctx.moveTo(-250, -12);
ctx.bezierCurveTo(-110, 2, 90, -8, 230, -50);
ctx.strokeStyle = 'rgba(220, 240, 255, 0.85)';
ctx.lineWidth = 2.4;
ctx.stroke();

// Bow Stempost & Cutwater
ctx.beginPath();
ctx.moveTo(230, -50);
ctx.lineTo(240, -54);
ctx.bezierCurveTo(232, -15, 210, 18, 175, 48);
ctx.lineTo(165, 46);
ctx.bezierCurveTo(202, 16, 224, -18, 230, -50);
ctx.closePath();
ctx.fillStyle = '#654326';
ctx.fill();
ctx.strokeStyle = 'rgba(225, 242, 255, 0.85)';
ctx.lineWidth = 1.2;
ctx.stroke();

// Bowsprit
const bowspritGrad = ctx.createLinearGradient(215, -45, 385, -92);
bowspritGrad.addColorStop(0, '#5a3d24');
bowspritGrad.addColorStop(0.4, '#805735');
bowspritGrad.addColorStop(1, '#382212');
ctx.beginPath();
ctx.moveTo(215, -45);
ctx.lineTo(385, -92);
ctx.lineTo(385, -87);
ctx.lineTo(215, -39);
ctx.closePath();
ctx.fillStyle = bowspritGrad;
ctx.fill();

ctx.beginPath();
ctx.moveTo(215, -45);
ctx.lineTo(385, -92);
ctx.strokeStyle = 'rgba(230, 245, 255, 0.9)';
ctx.lineWidth = 1.6;
ctx.stroke();

// Transom Stern
ctx.beginPath();
ctx.moveTo(-250, -12);
ctx.lineTo(-240, 36);
ctx.lineTo(-220, 34);
ctx.lineTo(-230, -12);
ctx.closePath();
ctx.fillStyle = '#2c1e13';
ctx.fill();
ctx.strokeStyle = 'rgba(12, 8, 4, 0.8)';
ctx.stroke();

// C. MAIN MAST & SPARS
const mastGrad = ctx.createLinearGradient(-95, 0, -75, 0);
mastGrad.addColorStop(0, '#d8ebfc');
mastGrad.addColorStop(0.25, '#87603e');
mastGrad.addColorStop(0.7, '#452e1b');
mastGrad.addColorStop(1, '#160e07');

ctx.beginPath();
ctx.moveTo(-87, -480);
ctx.lineTo(-65, -5);
ctx.lineTo(-53, -5);
ctx.lineTo(-83, -480);
ctx.closePath();
ctx.fillStyle = mastGrad;
ctx.fill();

// Main Boom
const boomGrad = ctx.createLinearGradient(-60, -60, -285, -78);
boomGrad.addColorStop(0, '#664427');
boomGrad.addColorStop(0.5, '#885c36');
boomGrad.addColorStop(1, '#362111');
ctx.beginPath();
ctx.moveTo(-60, -60);
ctx.lineTo(-288, -78);
ctx.lineTo(-288, -71);
ctx.lineTo(-60, -53);
ctx.closePath();
ctx.fillStyle = boomGrad;
ctx.fill();

ctx.beginPath();
ctx.moveTo(-60, -60);
ctx.lineTo(-288, -78);
ctx.strokeStyle = 'rgba(220, 240, 255, 0.85)';
ctx.lineWidth = 1.3;
ctx.stroke();

// Gaff Spar
ctx.beginPath();
ctx.moveTo(-73, -360);
ctx.lineTo(-218, -428);
ctx.lineTo(-218, -421);
ctx.lineTo(-73, -353);
ctx.closePath();
ctx.fillStyle = boomGrad;
ctx.fill();

// D. BILLOWING CANVAS SAILS
// 1. Mainsail
ctx.beginPath();
ctx.moveTo(-73, -360);
ctx.lineTo(-218, -428);
ctx.bezierCurveTo(-308, -280, -328, -170, -285, -78);
ctx.bezierCurveTo(-205, -68, -135, -62, -60, -65);
ctx.closePath();

const mainSailGrad = ctx.createRadialGradient(-130, -300, 45, -190, -240, 310);
mainSailGrad.addColorStop(0, '#ffffff');
mainSailGrad.addColorStop(0.22, '#e6f2fc');
mainSailGrad.addColorStop(0.6, '#7e9cbe');
mainSailGrad.addColorStop(0.88, '#364a62');
mainSailGrad.addColorStop(1, '#1a2636');
ctx.fillStyle = mainSailGrad;
ctx.fill();

// Mainsail panel seams
ctx.strokeStyle = 'rgba(65, 95, 135, 0.32)';
ctx.lineWidth = 1.0;
for (let p = 1; p <= 6; p++) {
    const frac = p / 7;
    const yTop = -428 + frac * (-360 - -428);
    const xTop = -218 + frac * (-73 - -218);
    const yBot = -78 + frac * (-65 - -78);
    const xBot = -285 + frac * (-60 - -285);
    const xMid = (xTop + xBot) * 0.5 - Math.sin(frac * Math.PI) * 35;
    const yMid = (yTop + yBot) * 0.5 + 5;

    ctx.beginPath();
    ctx.moveTo(xTop, yTop);
    ctx.quadraticCurveTo(xMid, yMid, xBot, yBot);
    ctx.stroke();
}

// 2. Jib / Fore Staysail
ctx.beginPath();
ctx.moveTo(-85, -465);
ctx.bezierCurveTo(75, -300, 235, -200, 370, -90);
ctx.bezierCurveTo(220, -72, 90, -76, -40, -85);
ctx.bezierCurveTo(-55, -210, -70, -330, -85, -465);
ctx.closePath();

const jibGrad = ctx.createLinearGradient(-85, -465, 200, -85);
jibGrad.addColorStop(0, '#ffffff');
jibGrad.addColorStop(0.28, '#daf0ff');
jibGrad.addColorStop(0.68, '#82a4c8');
jibGrad.addColorStop(1, '#3b526d');
ctx.fillStyle = jibGrad;
ctx.fill();

ctx.beginPath();
ctx.moveTo(-85, -465);
ctx.bezierCurveTo(75, -300, 235, -200, 370, -90);
ctx.strokeStyle = 'rgba(235, 248, 255, 0.95)';
ctx.lineWidth = 1.8;
ctx.stroke();

// 3. Flying Outer Jib
ctx.beginPath();
ctx.moveTo(-85, -435);
ctx.bezierCurveTo(95, -270, 260, -170, 385, -92);
ctx.bezierCurveTo(270, -115, 160, -135, 60, -150);
ctx.bezierCurveTo(15, -240, -35, -340, -85, -435);
ctx.closePath();
const outerJibGrad = ctx.createLinearGradient(-85, -435, 385, -92);
outerJibGrad.addColorStop(0, '#f2f8ff');
outerJibGrad.addColorStop(0.35, '#bddbf7');
outerJibGrad.addColorStop(1, '#476382');
ctx.fillStyle = outerJibGrad;
ctx.fill();

// E. RIGGING, STAYS & SHROUDS
ctx.strokeStyle = 'rgba(195, 225, 255, 0.52)';
ctx.lineWidth = 1.0;

ctx.beginPath();
ctx.moveTo(-85, -475); ctx.lineTo(385, -92);
ctx.moveTo(-85, -370); ctx.lineTo(320, -78);
ctx.moveTo(-85, -475); ctx.lineTo(240, -50);
ctx.moveTo(-85, -475); ctx.lineTo(-240, -10);
ctx.moveTo(-85, -370); ctx.lineTo(-190, -5);
ctx.moveTo(-85, -370); ctx.lineTo(-140, 0);
ctx.moveTo(-85, -370); ctx.lineTo(-20, 2);
ctx.moveTo(-85, -370); ctx.lineTo(30, -2);
ctx.stroke();

// Ratlines
ctx.strokeStyle = 'rgba(165, 200, 240, 0.32)';
ctx.lineWidth = 0.8;
for (let r = 1; r <= 9; r++) {
    const yR = -180 + r * 18;
    const xL = -190 + (-85 - -190) * ((-5 - yR) / (-5 - -370));
    const xR = -140 + (-85 - -140) * ((0 - yR) / (0 - -370));
    ctx.beginPath();
    ctx.moveTo(xL, yR);
    ctx.lineTo(xR, yR);
    ctx.stroke();
}

// F. WARM DECK LANTERN
const lanternX = -135;
const lanternY = -22;

const lanternGlow = ctx.createRadialGradient(lanternX, lanternY, 2, lanternX, lanternY, 110);
lanternGlow.addColorStop(0, 'rgba(255, 210, 90, 0.95)');
lanternGlow.addColorStop(0.2, 'rgba(255, 155, 45, 0.58)');
lanternGlow.addColorStop(0.55, 'rgba(190, 85, 15, 0.18)');
lanternGlow.addColorStop(1, 'rgba(190, 85, 15, 0)');
ctx.fillStyle = lanternGlow;
ctx.beginPath();
ctx.arc(lanternX, lanternY, 110, 0, Math.PI * 2);
ctx.fill();

// Lantern housing
ctx.fillStyle = '#1c1005';
ctx.fillRect(lanternX - 4.5, lanternY - 9, 9, 15);
ctx.fillStyle = '#fffce8';
ctx.fillRect(lanternX - 2.5, lanternY - 5.5, 5, 8);

// G. BOW SPRAY & FOAM CUTWATER
ctx.save();
for (let f = 0; f < 45; f++) {
    const fx = 180 + prng(f * 7) * 70;
    const fy = 25 + prng(f * 11) * 35;
    const fr = 1.0 + prng(f * 13) * 3.8;
    const fa = 0.35 + prng(f * 17) * 0.55;

    ctx.fillStyle = `rgba(225, 245, 255, ${fa})`;
    ctx.beginPath();
    ctx.arc(fx, fy, fr, 0, Math.PI * 2);
    ctx.fill();
}

ctx.strokeStyle = 'rgba(190, 225, 255, 0.45)';
ctx.lineWidth = 2.8;
ctx.beginPath();
ctx.moveTo(235, 30);
ctx.bezierCurveTo(80, 68, -120, 65, -290, 25);
ctx.stroke();
ctx.restore();

ctx.restore();

// 4. COMPOSITION POLISH & VIGNETTE
Drawing.drawVignette(ctx, width, height, { vignetteColor: '#000000', intensity: 0.5, radius: 0.88 });

canvas;
