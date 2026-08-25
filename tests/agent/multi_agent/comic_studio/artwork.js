// ============================================================================
// POLSON COMIC STUDIO: MASTER PUBLICATION ARTWORK
// ============================================================================
// Artwork: Pirate Woman on Ship Deck (Master Study: reference_images/comic1.png)
// Canvas: 900 x 750 px
// Engine: Polson ECMAScript 2025 MCP Graphics Engine (Canvas2D + Skia Shaders)
// ============================================================================

const canvas = createCanvas(900, 750);
const ctx = canvas.getContext('2d');

// ============================================================================
// 1. MASTER 4-TIER COLOR PALETTE & LIGHTING CONSTANTS
// ============================================================================
const PALETTE = {
    sky: { top: '#3b6886', mid: '#588da8', bottom: '#9ec3d5' },
    clouds: { fill: '#faf8f2', shadow: '#c4d0da', outline: '#344858' },
    sailWedge: '#0a0a0c',
    rigging: {
        ropeBase: '#9e7348', ropeShadow: '#4e3016',
        ropeLight: '#d8ab7a', knot: '#341c0c'
    },
    skin: {
        highlight: '#fef3e4', base: '#e6b18a',
        midShadow: '#ca835b', shadow: '#9e5f3c', deep: '#6c351e',
        blush: 'rgba(195, 75, 55, 0.20)', lipBase: '#983734', lipShadow: '#621d1b',
        teeth: '#f8f8f2', mouthDark: '#120404'
    },
    hair: {
        sunlit: '#fca858', highlight: '#e6803b', base: '#c65324',
        shadow: '#782610', deep: '#3e1106'
    },
    bandana: {
        highlight: '#526e8d', base: '#304256',
        shadow: '#1a2634', deep: '#0c121a'
    },
    shirt: { base: '#f3ebd6', shadow: '#c4baa2', deep: '#948a74' },
    coat: { highlight: '#364656', base: '#1a222a', shadow: '#0a0e14' },
    earring: { highlight: '#ffea88', base: '#d49b28', shadow: '#7e530e' },
    eyes: { sclera: '#f5f5f0', scleraShadow: '#c4cbd4', iris: '#3e6c86', irisDark: '#183244', pupil: '#0a0a0c' },
    ink: '#0a0a0c'
};

// ============================================================================
// 2. INKING UTILITIES (Tapered Strokes & Directional Feathering)
// ============================================================================

function drawTaperedStroke(ctx, start, cp1, cp2, end, maxThickness, color = PALETTE.ink) {
    const steps = 24;
    const points = [];
    for (let i = 0; i <= steps; i++) {
        const t = i / steps;
        const it = 1 - t;
        const x = it*it*it*start.x + 3*it*it*t*cp1.x + 3*it*t*t*cp2.x + t*t*t*end.x;
        const y = it*it*it*start.y + 3*it*it*t*cp1.y + 3*it*t*t*cp2.y + t*t*t*end.y;
        const dx = 3*it*it*(cp1.x - start.x) + 6*it*t*(cp2.x - cp1.x) + 3*t*t*(end.x - cp2.x);
        const dy = 3*it*it*(cp1.y - start.y) + 6*it*t*(cp2.y - cp1.y) + 3*t*t*(end.y - cp2.y);
        const len = Math.sqrt(dx*dx + dy*dy) || 1;
        const nx = -dy / len;
        const ny = dx / len;
        const thickness = maxThickness * Math.sin(t * Math.PI);
        points.push({ x, y, nx, ny, thickness });
    }
    ctx.beginPath();
    ctx.moveTo(points[0].x, points[0].y);
    for (let i = 0; i < points.length; i++) {
        const p = points[i];
        ctx.lineTo(p.x + p.nx * (p.thickness * 0.5), p.y + p.ny * (p.thickness * 0.5));
    }
    for (let i = points.length - 1; i >= 0; i--) {
        const p = points[i];
        ctx.lineTo(p.x - p.nx * (p.thickness * 0.5), p.y - p.ny * (p.thickness * 0.5));
    }
    ctx.closePath();
    ctx.fillStyle = color;
    ctx.fill();
}

function drawFeatheringHatch(ctx, origin, directionAngleDeg, count, length, spacing, color = PALETTE.ink, lineWidth = 1.2) {
    const rad = (directionAngleDeg * Math.PI) / 180;
    const dx = Math.cos(rad);
    const dy = Math.sin(rad);
    const px = -dy;
    const py = dx;

    ctx.save();
    ctx.strokeStyle = color;
    ctx.lineWidth = lineWidth;
    ctx.lineCap = 'round';

    for (let i = 0; i < count; i++) {
        const sx = origin.x + px * (i * spacing);
        const sy = origin.y + py * (i * spacing);
        const lenVar = length * (0.75 + 0.45 * Math.sin(i * 1.4));
        const ex = sx + dx * lenVar;
        const ey = sy + dy * lenVar;
        ctx.beginPath();
        ctx.moveTo(sx, sy);
        ctx.lineTo(ex, ey);
        ctx.stroke();
    }
    ctx.restore();
}

// ============================================================================
// 3. LAYER 1: SKY, PROCEDURAL CLOUDS & SHIP'S RIGGING
// ============================================================================
function drawBackgroundAndRigging(ctx) {
    // 1. Sky Gradient
    const skyGrad = ctx.createLinearGradient(0, 0, 0, 750);
    skyGrad.addColorStop(0.0, PALETTE.sky.top);
    skyGrad.addColorStop(0.55, PALETTE.sky.mid);
    skyGrad.addColorStop(1.0, PALETTE.sky.bottom);
    ctx.fillStyle = skyGrad;
    ctx.fillRect(0, 0, 900, 750);

    // 2. Billowy Clouds
    function drawCloudCluster(cx, cy, s) {
        ctx.save();
        ctx.translate(cx, cy);
        ctx.scale(s, s);

        ctx.beginPath();
        ctx.moveTo(-160, 30);
        ctx.lineTo(160, 30);
        ctx.quadraticCurveTo(0, 85, -160, 30);
        ctx.fillStyle = PALETTE.clouds.shadow;
        ctx.fill();

        ctx.beginPath();
        ctx.arc(-100, 0, 55, 0, Math.PI * 2);
        ctx.arc(-35, -45, 75, 0, Math.PI * 2);
        ctx.arc(45, -35, 65, 0, Math.PI * 2);
        ctx.arc(110, 10, 50, 0, Math.PI * 2);
        ctx.closePath();
        ctx.fillStyle = PALETTE.clouds.fill;
        ctx.fill();

        ctx.strokeStyle = PALETTE.clouds.outline;
        ctx.lineWidth = 1.8;
        ctx.stroke();
        ctx.restore();
    }

    drawCloudCluster(110, 140, 1.25);
    drawCloudCluster(270, 260, 0.95);
    drawCloudCluster(800, 190, 1.3);
    drawCloudCluster(860, 380, 0.95);

    try {
        const cloudNoise = Skia.Shader.perlinNoiseFractal(0.012, 0.012, 4, 101);
        ctx.save();
        ctx.globalCompositeOperation = 'soft-light';
        ctx.fillStyle = cloudNoise;
        ctx.fillRect(0, 0, 900, 500);
        ctx.restore();
    } catch (e) {}

    // Top-left sail corner
    ctx.beginPath();
    ctx.moveTo(0, 0);
    ctx.lineTo(115, 0);
    ctx.lineTo(0, 210);
    ctx.closePath();
    ctx.fillStyle = PALETTE.sailWedge;
    ctx.fill();

    // 3. Rigging Shrouds & Ratlines
    const shrouds = [
        { x1: 360, y1: 0, x2: -40, y2: 680, w: 10 },
        { x1: 540, y1: 0, x2: 40, y2: 750, w: 11 },
        { x1: 770, y1: 0, x2: 240, y2: 750, w: 12 },
        { x1: 960, y1: 50, x2: 540, y2: 750, w: 12 },
        { x1: 1040, y1: 250, x2: 790, y2: 750, w: 10 }
    ];

    shrouds.forEach(s => {
        ctx.beginPath();
        ctx.moveTo(s.x1, s.y1);
        ctx.lineTo(s.x2, s.y2);
        ctx.strokeStyle = PALETTE.rigging.ropeShadow;
        ctx.lineWidth = s.w;
        ctx.stroke();

        ctx.beginPath();
        ctx.moveTo(s.x1 - 2, s.y1);
        ctx.lineTo(s.x2 - 2, s.y2);
        ctx.strokeStyle = PALETTE.rigging.ropeBase;
        ctx.lineWidth = s.w - 3;
        ctx.stroke();

        ctx.beginPath();
        ctx.moveTo(s.x1 - 3.5, s.y1);
        ctx.lineTo(s.x2 - 3.5, s.y2);
        ctx.strokeStyle = PALETTE.rigging.ropeLight;
        ctx.lineWidth = 1.8;
        ctx.stroke();
    });

    const ratlineYs = [65, 125, 185, 245, 305, 365, 425, 485, 545, 605, 665, 725];
    ratlineYs.forEach(y => {
        ctx.beginPath();
        ctx.moveTo(0, y + 10);
        ctx.bezierCurveTo(300, y + 2, 600, y - 6, 900, y - 15);
        ctx.strokeStyle = PALETTE.rigging.ropeBase;
        ctx.lineWidth = 3.5;
        ctx.stroke();

        ctx.beginPath();
        ctx.moveTo(0, y + 12);
        ctx.bezierCurveTo(300, y + 4, 600, y - 4, 900, y - 13);
        ctx.strokeStyle = PALETTE.rigging.ropeShadow;
        ctx.lineWidth = 1.2;
        ctx.stroke();

        shrouds.forEach(s => {
            const t = (y - s.y1) / (s.y2 - s.y1);
            if (t >= 0 && t <= 1) {
                const kx = s.x1 + t * (s.x2 - s.x1);
                const ky = s.y1 + t * (s.y2 - s.y1);
                ctx.beginPath();
                ctx.ellipse(kx, ky, 6, 7, 0.3, 0, Math.PI * 2);
                ctx.fillStyle = PALETTE.rigging.knot;
                ctx.fill();

                ctx.beginPath();
                ctx.arc(kx - 1, ky - 1, 2.5, 0, Math.PI * 2);
                ctx.fillStyle = PALETTE.rigging.ropeLight;
                ctx.fill();
            }
        });
    });

    try {
        const fiberShader = Skia.Shader.perlinNoiseTurbulence(0.08, 0.40, 3, 42);
        ctx.save();
        ctx.globalCompositeOperation = 'overlay';
        shrouds.forEach(s => {
            ctx.beginPath();
            ctx.moveTo(s.x1, s.y1);
            ctx.lineTo(s.x2, s.y2);
            ctx.strokeStyle = fiberShader;
            ctx.lineWidth = s.w;
            ctx.stroke();
        });
        ctx.restore();
    } catch (e) {}
}

// ============================================================================
// 4. LAYER 2: ORGANIC DYNAMIC HAIR (ORGANIC WINDSWEPT S-CURVES & PONYTAIL)
// ============================================================================
function drawHairLayers(ctx) {
    // 1. Deep Background Hair Mass (Chestnut / dark copper under-mass)
    ctx.beginPath();
    ctx.moveTo(350, 420);
    ctx.bezierCurveTo(280, 240, 180, 110, 40, 210);
    ctx.bezierCurveTo(0, 250, 10, 330, 40, 390);
    ctx.bezierCurveTo(20, 430, 60, 480, 100, 470);
    ctx.bezierCurveTo(160, 450, 220, 420, 280, 380);
    ctx.bezierCurveTo(320, 410, 350, 460, 350, 480);
    ctx.bezierCurveTo(390, 430, 470, 360, 560, 310);
    ctx.bezierCurveTo(650, 310, 715, 270, 765, 270);
    ctx.bezierCurveTo(770, 200, 730, 130, 660, 65);
    ctx.bezierCurveTo(570, 25, 460, 30, 370, 70);
    ctx.bezierCurveTo(320, 190, 335, 310, 350, 420);
    ctx.closePath();
    ctx.fillStyle = PALETTE.hair.deep;
    ctx.fill();

    // 2. Rich Copper-Red Main Body
    ctx.beginPath();
    ctx.moveTo(350, 420);
    ctx.bezierCurveTo(290, 250, 200, 130, 60, 210);
    ctx.bezierCurveTo(20, 250, 20, 320, 50, 380);
    ctx.bezierCurveTo(30, 420, 70, 465, 110, 455);
    ctx.bezierCurveTo(170, 440, 230, 405, 290, 370);
    ctx.bezierCurveTo(330, 395, 360, 445, 360, 465);
    ctx.bezierCurveTo(400, 415, 480, 350, 570, 305);
    ctx.bezierCurveTo(650, 305, 715, 265, 755, 265);
    ctx.bezierCurveTo(755, 195, 720, 125, 650, 60);
    ctx.bezierCurveTo(560, 25, 450, 28, 360, 68);
    ctx.bezierCurveTo(315, 185, 335, 300, 350, 420);
    ctx.closePath();
    ctx.fillStyle = PALETTE.hair.base;
    ctx.fill();

    // 3. Flowing 3D Ribbon Curls (Ponytail & Crown Waves)
    function renderFlowingLock(root, cp1, cp2, tip, cp3, cp4, width, fill, shadowFill) {
        ctx.beginPath();
        ctx.moveTo(root.x, root.y);
        ctx.bezierCurveTo(cp1.x, cp1.y, cp2.x, cp2.y, tip.x, tip.y);
        ctx.bezierCurveTo(cp3.x, cp3.y, cp4.x, cp4.y, root.x + width, root.y + 5);
        ctx.closePath();
        ctx.fillStyle = fill;
        ctx.fill();

        ctx.beginPath();
        ctx.moveTo(cp3.x, cp3.y);
        ctx.bezierCurveTo(cp4.x, cp4.y, tip.x, tip.y, tip.x, tip.y);
        ctx.bezierCurveTo(cp3.x - 12, cp3.y + 12, cp4.x - 6, cp4.y + 6, cp3.x, cp3.y);
        ctx.closePath();
        ctx.fillStyle = shadowFill;
        ctx.fill();
    }

    // Top Crown Arching Waves
    renderFlowingLock({x:590, y:290}, {x:680, y:150}, {x:640, y:60}, {x:500, y:30}, {x:410, y:50}, {x:510, y:150}, 35, PALETTE.hair.highlight, PALETTE.hair.shadow);
    renderFlowingLock({x:560, y:300}, {x:600, y:170}, {x:510, y:100}, {x:390, y:65}, {x:310, y:110}, {x:440, y:200}, 30, PALETTE.hair.highlight, PALETTE.hair.shadow);
    renderFlowingLock({x:530, y:310}, {x:530, y:210}, {x:440, y:150}, {x:330, y:120}, {x:260, y:180}, {x:380, y:250}, 25, PALETTE.hair.highlight, PALETTE.hair.shadow);

    // Ponytail Cascading Locks
    renderFlowingLock({x:350, y:410}, {x:290, y:230}, {x:190, y:110}, {x:80, y:130}, {x:130, y:180}, {x:230, y:230}, 28, PALETTE.hair.highlight, PALETTE.hair.shadow);
    renderFlowingLock({x:340, y:420}, {x:250, y:200}, {x:130, y:140}, {x:30, y:210}, {x:60, y:250}, {x:170, y:250}, 32, PALETTE.hair.highlight, PALETTE.hair.shadow);
    renderFlowingLock({x:345, y:430}, {x:230, y:270}, {x:110, y:250}, {x:20, y:310}, {x:50, y:350}, {x:150, y:330}, 30, PALETTE.hair.highlight, PALETTE.hair.shadow);
    renderFlowingLock({x:340, y:440}, {x:240, y:340}, {x:130, y:350}, {x:40, y:410}, {x:70, y:440}, {x:180, y:390}, 26, PALETTE.hair.highlight, PALETTE.hair.shadow);
    renderFlowingLock({x:330, y:450}, {x:260, y:410}, {x:170, y:440}, {x:85, y:480}, {x:120, y:470}, {x:220, y:420}, 22, PALETTE.hair.base, PALETTE.hair.shadow);
}

// ============================================================================
// 5. LAYER 3: BANDANA WRAP & FLOWING FABRIC TAILS
// ============================================================================
function drawBandana(ctx) {
    const kx = 355;
    const ky = 455;

    // 1. Streaming Fabric Tails
    // Upper Tail
    ctx.beginPath();
    ctx.moveTo(kx - 5, ky - 10);
    ctx.bezierCurveTo(260, 445, 140, 495, 0, 485);
    ctx.lineTo(0, 540);
    ctx.bezierCurveTo(130, 545, 250, 500, kx - 15, ky + 15);
    ctx.closePath();
    ctx.fillStyle = PALETTE.bandana.base;
    ctx.fill();

    ctx.beginPath();
    ctx.moveTo(kx - 5, ky - 5);
    ctx.bezierCurveTo(250, 470, 140, 520, 0, 510);
    ctx.lineTo(0, 540);
    ctx.bezierCurveTo(130, 545, 250, 500, kx - 15, ky + 15);
    ctx.closePath();
    ctx.fillStyle = PALETTE.bandana.shadow;
    ctx.fill();

    // Lower Tail
    ctx.beginPath();
    ctx.moveTo(kx - 15, ky + 20);
    ctx.bezierCurveTo(240, 505, 120, 590, 0, 580);
    ctx.lineTo(0, 640);
    ctx.bezierCurveTo(110, 650, 220, 575, kx - 20, ky + 45);
    ctx.closePath();
    ctx.fillStyle = PALETTE.bandana.base;
    ctx.fill();

    ctx.beginPath();
    ctx.moveTo(kx - 15, ky + 30);
    ctx.bezierCurveTo(220, 540, 120, 615, 0, 605);
    ctx.lineTo(0, 640);
    ctx.bezierCurveTo(110, 650, 220, 575, kx - 20, ky + 45);
    ctx.closePath();
    ctx.fillStyle = PALETTE.bandana.shadow;
    ctx.fill();

    // 2. Bandana Headband Wrap across Forehead
    ctx.beginPath();
    ctx.moveTo(kx, ky - 20);
    ctx.bezierCurveTo(420, 375, 495, 325, 585, 315);
    ctx.bezierCurveTo(620, 315, 645, 330, 658, 355);
    ctx.bezierCurveTo(640, 372, 610, 372, 575, 365);
    ctx.bezierCurveTo(480, 388, 405, 438, kx, ky + 15);
    ctx.closePath();
    ctx.fillStyle = PALETTE.bandana.base;
    ctx.fill();

    // Bandana Top Highlight Fold
    ctx.beginPath();
    ctx.moveTo(kx, ky - 20);
    ctx.bezierCurveTo(420, 375, 495, 325, 585, 315);
    ctx.bezierCurveTo(620, 315, 645, 330, 658, 355);
    ctx.bezierCurveTo(630, 342, 590, 332, 555, 332);
    ctx.bezierCurveTo(460, 355, 390, 405, kx, ky - 20);
    ctx.closePath();
    ctx.fillStyle = PALETTE.bandana.highlight;
    ctx.fill();

    // Bandana Wrapped Knot Base
    ctx.beginPath();
    ctx.ellipse(kx, ky, 16, 22, 0.35, 0, Math.PI * 2);
    ctx.fillStyle = PALETTE.bandana.deep;
    ctx.fill();

    ctx.beginPath();
    ctx.ellipse(kx - 3, ky - 4, 8, 11, 0.35, 0, Math.PI * 2);
    ctx.fillStyle = PALETTE.bandana.highlight;
    ctx.fill();
}

// ============================================================================
// 6. LAYER 4: BODY, COAT & WHITE PIRATE SHIRT WITH SPREAD COLLAR
// ============================================================================
function drawClothingAndBody(ctx) {
    // 1. Captain's Coat (Left Shoulder)
    ctx.beginPath();
    ctx.moveTo(0, 660);
    ctx.bezierCurveTo(80, 620, 180, 595, 260, 600);
    ctx.bezierCurveTo(340, 610, 400, 640, 430, 660);
    ctx.lineTo(430, 750);
    ctx.lineTo(0, 750);
    ctx.closePath();
    ctx.fillStyle = PALETTE.coat.base;
    ctx.fill();

    // Coat Right Shoulder
    ctx.beginPath();
    ctx.moveTo(900, 690);
    ctx.bezierCurveTo(840, 640, 770, 615, 710, 610);
    ctx.lineTo(570, 660);
    ctx.lineTo(570, 750);
    ctx.lineTo(900, 750);
    ctx.closePath();
    ctx.fillStyle = PALETTE.coat.base;
    ctx.fill();

    // Coat Shadow Folds
    ctx.beginPath();
    ctx.moveTo(0, 720);
    ctx.bezierCurveTo(80, 680, 180, 660, 240, 680);
    ctx.lineTo(240, 750);
    ctx.lineTo(0, 750);
    ctx.closePath();
    ctx.fillStyle = PALETTE.coat.shadow;
    ctx.fill();

    // 2. White Piratical Shirt with Dynamic Flared Collar Wings
    // Left Wing Collar (Flaring to x=160, y=610)
    ctx.beginPath();
    ctx.moveTo(435, 660);
    ctx.bezierCurveTo(350, 630, 240, 600, 160, 610);
    ctx.bezierCurveTo(240, 670, 340, 710, 430, 750);
    ctx.closePath();
    ctx.fillStyle = PALETTE.shirt.base;
    ctx.fill();

    ctx.beginPath();
    ctx.moveTo(320, 655);
    ctx.bezierCurveTo(240, 635, 190, 620, 160, 610);
    ctx.bezierCurveTo(210, 655, 270, 680, 330, 710);
    ctx.closePath();
    ctx.fillStyle = PALETTE.shirt.shadow;
    ctx.fill();

    // Right Wing Collar (Flaring to x=780, y=625)
    ctx.beginPath();
    ctx.moveTo(565, 660);
    ctx.bezierCurveTo(630, 630, 710, 605, 780, 625);
    ctx.bezierCurveTo(710, 680, 640, 720, 570, 750);
    ctx.closePath();
    ctx.fillStyle = PALETTE.shirt.base;
    ctx.fill();

    ctx.beginPath();
    ctx.moveTo(630, 665);
    ctx.bezierCurveTo(680, 645, 740, 630, 780, 625);
    ctx.bezierCurveTo(720, 675, 670, 705, 620, 725);
    ctx.closePath();
    ctx.fillStyle = PALETTE.shirt.shadow;
    ctx.fill();

    // Open V-Neck Chest
    ctx.beginPath();
    ctx.moveTo(438, 662);
    ctx.bezierCurveTo(490, 705, 520, 710, 565, 665);
    ctx.lineTo(540, 750);
    ctx.lineTo(460, 750);
    ctx.closePath();
    ctx.fillStyle = PALETTE.skin.shadow;
    ctx.fill();

    // Clavicle shadow
    ctx.beginPath();
    ctx.moveTo(490, 710);
    ctx.bezierCurveTo(515, 730, 535, 725, 545, 710);
    ctx.bezierCurveTo(535, 735, 515, 740, 490, 710);
    ctx.closePath();
    ctx.fillStyle = PALETTE.skin.deep;
    ctx.fill();
}

// ============================================================================
// 7. LAYER 5: HEAD, JAWLINE & 5 ANATOMICAL CEL-SHADOW PLANES
// ============================================================================
function drawHeadAndCelShading(ctx) {
    // 1. Neck Base
    ctx.beginPath();
    ctx.moveTo(438, 485);
    ctx.bezierCurveTo(420, 550, 400, 620, 380, 710);
    ctx.lineTo(565, 750);
    ctx.bezierCurveTo(585, 690, 605, 650, 636, 606);
    ctx.bezierCurveTo(570, 590, 468, 535, 438, 485);
    ctx.closePath();
    ctx.fillStyle = PALETTE.skin.base;
    ctx.fill();

    // 2. Facial Mask (Defined 3/4 Yaw & Sharp Mandible Angle)
    ctx.beginPath();
    ctx.moveTo(360, 440);
    ctx.bezierCurveTo(415, 375, 495, 325, 585, 315);
    ctx.bezierCurveTo(620, 315, 645, 330, 658, 355);
    ctx.bezierCurveTo(690, 370, 730, 400, 746, 450);
    ctx.bezierCurveTo(740, 475, 725, 530, 695, 575);
    ctx.bezierCurveTo(670, 605, 650, 615, 636, 616);
    ctx.bezierCurveTo(575, 600, 478, 545, 445, 495);
    ctx.bezierCurveTo(425, 505, 400, 490, 415, 440);
    ctx.closePath();
    ctx.fillStyle = PALETTE.skin.base;
    ctx.fill();

    // 3. Right Ear Base
    ctx.beginPath();
    ctx.moveTo(422, 430);
    ctx.bezierCurveTo(398, 418, 392, 475, 424, 498);
    ctx.bezierCurveTo(436, 502, 440, 470, 430, 440);
    ctx.closePath();
    ctx.fillStyle = PALETTE.skin.base;
    ctx.fill();

    ctx.beginPath();
    ctx.moveTo(418, 445);
    ctx.bezierCurveTo(408, 455, 412, 480, 422, 485);
    ctx.closePath();
    ctx.fillStyle = PALETTE.skin.deep;
    ctx.fill();

    // 4. Gold Hoop Earring
    ctx.beginPath();
    ctx.ellipse(400, 518, 19, 23, 0.2, 0, Math.PI * 2);
    ctx.fillStyle = PALETTE.earring.base;
    ctx.fill();

    ctx.beginPath();
    ctx.ellipse(400, 518, 13, 17, 0.2, 0, Math.PI * 2);
    ctx.fillStyle = PALETTE.skin.shadow;
    ctx.fill();

    ctx.beginPath();
    ctx.arc(392, 508, 4.5, 0, Math.PI * 2);
    ctx.fillStyle = PALETTE.earring.highlight;
    ctx.fill();

    ctx.beginPath();
    ctx.arc(410, 530, 3.5, 0, Math.PI * 2);
    ctx.fillStyle = PALETTE.earring.shadow;
    ctx.fill();

    // --- 5 ANATOMICAL FACIAL CEL-SHADOW PLANES ---
    // Plane 1: Under-Brow Socket Shadows
    ctx.beginPath();
    ctx.moveTo(525, 375);
    ctx.bezierCurveTo(552, 355, 592, 355, 628, 378);
    ctx.lineTo(614, 404);
    ctx.bezierCurveTo(592, 384, 552, 386, 532, 404);
    ctx.closePath();
    ctx.fillStyle = PALETTE.skin.shadow;
    ctx.fill();

    ctx.beginPath();
    ctx.moveTo(664, 386);
    ctx.bezierCurveTo(684, 374, 714, 376, 732, 394);
    ctx.lineTo(724, 414);
    ctx.bezierCurveTo(711, 396, 684, 394, 668, 410);
    ctx.closePath();
    ctx.fillStyle = PALETTE.skin.shadow;
    ctx.fill();

    // Plane 2: Nose Cast Shadow Triangle
    ctx.beginPath();
    ctx.moveTo(692, 478);
    ctx.lineTo(665, 515);
    ctx.lineTo(654, 484);
    ctx.closePath();
    ctx.fillStyle = PALETTE.skin.shadow;
    ctx.fill();

    ctx.beginPath();
    ctx.moveTo(692, 478);
    ctx.bezierCurveTo(680, 488, 658, 488, 652, 482);
    ctx.closePath();
    ctx.fillStyle = PALETTE.skin.deep;
    ctx.fill();

    // Plane 3: Cheek Hollow
    ctx.beginPath();
    ctx.moveTo(445, 495);
    ctx.bezierCurveTo(475, 530, 525, 560, 575, 575);
    ctx.lineTo(585, 610);
    ctx.bezierCurveTo(535, 590, 475, 545, 445, 495);
    ctx.closePath();
    ctx.fillStyle = PALETTE.skin.midShadow;
    ctx.fill();

    // Plane 4: Under-Lip Crescent Shadow
    ctx.beginPath();
    ctx.moveTo(620, 565);
    ctx.bezierCurveTo(635, 584, 655, 582, 665, 568);
    ctx.bezierCurveTo(655, 594, 631, 594, 620, 565);
    ctx.closePath();
    ctx.fillStyle = PALETTE.skin.shadow;
    ctx.fill();

    // Plane 5: Major Neck Cast Shadow
    ctx.beginPath();
    ctx.moveTo(445, 495);
    ctx.bezierCurveTo(475, 545, 575, 600, 636, 616);
    ctx.lineTo(630, 630);
    ctx.bezierCurveTo(570, 650, 515, 675, 460, 685);
    ctx.bezierCurveTo(430, 620, 418, 550, 445, 495);
    ctx.closePath();
    ctx.fillStyle = PALETTE.skin.deep;
    ctx.fill();

    // Sunlit Skin Highlights
    ctx.beginPath();
    ctx.ellipse(565, 345, 25, 12, -0.1, 0, Math.PI * 2);
    ctx.fillStyle = PALETTE.skin.highlight;
    ctx.fill();

    ctx.beginPath();
    ctx.moveTo(644, 395);
    ctx.lineTo(686, 472);
    ctx.lineTo(680, 475);
    ctx.lineTo(640, 398);
    ctx.closePath();
    ctx.fillStyle = PALETTE.skin.highlight;
    ctx.fill();

    ctx.beginPath();
    ctx.arc(636, 606, 7.5, 0, Math.PI * 2);
    ctx.fillStyle = PALETTE.skin.highlight;
    ctx.fill();

    // Subtle skin tone warmth glaze
    ctx.save();
    ctx.globalCompositeOperation = 'multiply';
    ctx.beginPath();
    ctx.ellipse(560, 490, 45, 30, -0.2, 0, Math.PI * 2);
    ctx.fillStyle = PALETTE.skin.blush;
    ctx.fill();
    ctx.restore();
}

// ============================================================================
// 8. LAYER 6: EXPRESSIVE FACIAL FEATURES (EYES, IRISES, NOSE & FIERCE GRIMACE)
// ============================================================================
function drawFacialExpression(ctx) {
    // --- RIGHT EYE (Near Eye) ---
    ctx.beginPath();
    ctx.moveTo(540, 408);
    ctx.bezierCurveTo(562, 384, 600, 383, 622, 408);
    ctx.bezierCurveTo(604, 424, 568, 424, 540, 408);
    ctx.closePath();
    ctx.fillStyle = PALETTE.eyes.sclera;
    ctx.fill();

    ctx.save();
    ctx.beginPath();
    ctx.moveTo(540, 408);
    ctx.bezierCurveTo(562, 384, 600, 383, 622, 408);
    ctx.bezierCurveTo(604, 424, 568, 424, 540, 408);
    ctx.clip();

    ctx.beginPath();
    ctx.arc(592, 405, 14, 0, Math.PI * 2);
    ctx.fillStyle = PALETTE.eyes.irisDark;
    ctx.fill();

    ctx.beginPath();
    ctx.arc(592, 405, 12, 0, Math.PI * 2);
    ctx.fillStyle = PALETTE.eyes.iris;
    ctx.fill();

    ctx.beginPath();
    ctx.arc(594, 405, 6.5, 0, Math.PI * 2);
    ctx.fillStyle = PALETTE.eyes.pupil;
    ctx.fill();

    ctx.beginPath();
    ctx.arc(588, 401, 3.5, 0, Math.PI * 2);
    ctx.fillStyle = '#ffffff';
    ctx.fill();
    ctx.restore();

    // --- LEFT EYE (Far Eye) ---
    ctx.beginPath();
    ctx.moveTo(680, 412);
    ctx.bezierCurveTo(694, 394, 724, 396, 736, 418);
    ctx.bezierCurveTo(726, 428, 698, 430, 680, 412);
    ctx.closePath();
    ctx.fillStyle = PALETTE.eyes.sclera;
    ctx.fill();

    ctx.save();
    ctx.beginPath();
    ctx.moveTo(680, 412);
    ctx.bezierCurveTo(694, 394, 724, 396, 736, 418);
    ctx.bezierCurveTo(726, 428, 698, 430, 680, 412);
    ctx.clip();

    ctx.beginPath();
    ctx.ellipse(714, 412, 10, 13, 0, 0, Math.PI * 2);
    ctx.fillStyle = PALETTE.eyes.irisDark;
    ctx.fill();

    ctx.beginPath();
    ctx.ellipse(714, 412, 8.5, 11.5, 0, 0, Math.PI * 2);
    ctx.fillStyle = PALETTE.eyes.iris;
    ctx.fill();

    ctx.beginPath();
    ctx.ellipse(716, 412, 4.5, 6.5, 0, 0, Math.PI * 2);
    ctx.fillStyle = PALETTE.eyes.pupil;
    ctx.fill();

    ctx.beginPath();
    ctx.arc(711, 408, 2.5, 0, Math.PI * 2);
    ctx.fillStyle = '#ffffff';
    ctx.fill();
    ctx.restore();

    // --- FIERCE OPEN MOUTH GRIMACE ---
    ctx.beginPath();
    ctx.moveTo(622, 538);
    ctx.bezierCurveTo(642, 544, 662, 546, 682, 542);
    ctx.bezierCurveTo(673, 568, 643, 570, 624, 546);
    ctx.closePath();
    ctx.fillStyle = PALETTE.skin.mouthDark;
    ctx.fill();

    // White teeth shelf
    ctx.beginPath();
    ctx.moveTo(628, 540);
    ctx.bezierCurveTo(644, 545, 662, 545, 674, 542);
    ctx.lineTo(672, 550);
    ctx.bezierCurveTo(658, 553, 642, 553, 630, 548);
    ctx.closePath();
    ctx.fillStyle = PALETTE.skin.teeth;
    ctx.fill();

    // Upper Lip
    ctx.beginPath();
    ctx.moveTo(622, 538);
    ctx.bezierCurveTo(637, 528, 653, 530, 682, 542);
    ctx.bezierCurveTo(666, 544, 645, 542, 622, 538);
    ctx.closePath();
    ctx.fillStyle = PALETTE.skin.lipBase;
    ctx.fill();

    // Lower Lip
    ctx.beginPath();
    ctx.moveTo(624, 546);
    ctx.bezierCurveTo(639, 568, 665, 566, 680, 542);
    ctx.bezierCurveTo(666, 574, 635, 574, 624, 546);
    ctx.closePath();
    ctx.fillStyle = PALETTE.skin.lipShadow;
    ctx.fill();
}

// ============================================================================
// 9. LAYER 7: FOREGROUND CURLS, BANGS & SUNLIT OVERLAY HIGHLIGHTS
// ============================================================================
function drawForegroundCurlsAndHighlights(ctx) {
    // 1. Forehead Bangs & Loose Tendrils
    ctx.beginPath();
    ctx.moveTo(585, 315);
    ctx.bezierCurveTo(605, 230, 650, 170, 730, 150);
    ctx.bezierCurveTo(690, 210, 650, 270, 620, 320);
    ctx.closePath();
    ctx.fillStyle = PALETTE.hair.base;
    ctx.fill();

    // S-Curve Bang Lock whipping over brow
    ctx.beginPath();
    ctx.moveTo(680, 300);
    ctx.bezierCurveTo(725, 280, 770, 295, 780, 325);
    ctx.bezierCurveTo(750, 345, 710, 350, 675, 360);
    ctx.closePath();
    ctx.fillStyle = PALETTE.hair.highlight;
    ctx.fill();

    // Temple Hook Curl
    ctx.beginPath();
    ctx.moveTo(550, 360);
    ctx.bezierCurveTo(575, 390, 595, 420, 580, 455);
    ctx.bezierCurveTo(565, 440, 555, 405, 535, 375);
    ctx.closePath();
    ctx.fillStyle = PALETTE.hair.base;
    ctx.fill();

    // Forehead Spiral C-Curl
    ctx.beginPath();
    ctx.moveTo(640, 290);
    ctx.bezierCurveTo(665, 300, 685, 330, 675, 360);
    ctx.bezierCurveTo(655, 375, 640, 360, 650, 340);
    ctx.closePath();
    ctx.fillStyle = PALETTE.hair.highlight;
    ctx.fill();

    // Cheek Tendril
    ctx.beginPath();
    ctx.moveTo(445, 450);
    ctx.bezierCurveTo(402, 470, 372, 510, 348, 570);
    ctx.bezierCurveTo(362, 550, 392, 520, 440, 485);
    ctx.closePath();
    ctx.fillStyle = PALETTE.hair.base;
    ctx.fill();

    // 2. Sunlit Golden Rim Highlights on Windward Curls (Overlay Mode)
    ctx.save();
    ctx.globalCompositeOperation = 'overlay';

    ctx.beginPath();
    ctx.moveTo(500, 30);
    ctx.bezierCurveTo(640, 60, 680, 150, 590, 290);
    ctx.bezierCurveTo(620, 240, 570, 150, 490, 105);
    ctx.bezierCurveTo(410, 50, 500, 30, 500, 30);
    ctx.closePath();
    ctx.fillStyle = PALETTE.hair.sunlit;
    ctx.fill();

    ctx.beginPath();
    ctx.moveTo(190, 110);
    ctx.bezierCurveTo(130, 130, 80, 180, 40, 210);
    ctx.bezierCurveTo(65, 195, 120, 170, 180, 180);
    ctx.closePath();
    ctx.fillStyle = PALETTE.hair.sunlit;
    ctx.fill();

    ctx.beginPath();
    ctx.moveTo(680, 300);
    ctx.bezierCurveTo(725, 280, 770, 295, 780, 325);
    ctx.bezierCurveTo(760, 310, 720, 300, 680, 300);
    ctx.closePath();
    ctx.fillStyle = PALETTE.hair.sunlit;
    ctx.fill();

    ctx.restore();
}

// ============================================================================
// 10. LAYER 8: MASTER INKER PASS (3-TIER HIERARCHY, TAPERED STROKES & FEATHERING)
// ============================================================================
function drawMasterInks(ctx) {
    const ink = PALETTE.ink;

    // ------------------------------------------------------------------------
    // TIER 1: OUTER SILHOUETTES (3.8px – 5.0px)
    // ------------------------------------------------------------------------
    ctx.save();
    ctx.strokeStyle = ink;
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';

    // Jawline with Mandible Angle (4.4px)
    ctx.lineWidth = 4.4;
    ctx.beginPath();
    ctx.moveTo(445, 495);
    ctx.bezierCurveTo(475, 545, 575, 600, 636, 616);
    ctx.stroke();

    // Neck left silhouette (3.8px)
    ctx.lineWidth = 3.8;
    ctx.beginPath();
    ctx.moveTo(445, 495);
    ctx.bezierCurveTo(425, 560, 405, 630, 380, 710);
    ctx.stroke();

    // Coat shoulders (4.6px)
    ctx.lineWidth = 4.6;
    ctx.beginPath();
    ctx.moveTo(0, 660);
    ctx.bezierCurveTo(80, 620, 180, 595, 260, 600);
    ctx.bezierCurveTo(340, 610, 400, 640, 430, 660);
    ctx.stroke();

    ctx.beginPath();
    ctx.moveTo(900, 690);
    ctx.bezierCurveTo(840, 640, 770, 615, 710, 610);
    ctx.lineTo(570, 660);
    ctx.stroke();

    // Shirt Wing Collar Outlines (3.6px)
    ctx.lineWidth = 3.6;
    ctx.beginPath();
    ctx.moveTo(435, 660);
    ctx.bezierCurveTo(350, 630, 240, 600, 160, 610);
    ctx.bezierCurveTo(240, 670, 340, 710, 430, 750);
    ctx.stroke();

    ctx.beginPath();
    ctx.moveTo(565, 660);
    ctx.bezierCurveTo(630, 630, 710, 605, 780, 625);
    ctx.bezierCurveTo(710, 680, 640, 720, 570, 750);
    ctx.stroke();

    // Bandana Tails Silhouette (3.6px)
    ctx.lineWidth = 3.6;
    ctx.beginPath();
    ctx.moveTo(355, 445);
    ctx.bezierCurveTo(260, 445, 140, 495, 0, 485);
    ctx.stroke();

    ctx.beginPath();
    ctx.moveTo(340, 470);
    ctx.bezierCurveTo(250, 500, 130, 545, 0, 540);
    ctx.stroke();

    ctx.beginPath();
    ctx.moveTo(340, 475);
    ctx.bezierCurveTo(240, 505, 120, 590, 0, 580);
    ctx.stroke();

    ctx.beginPath();
    ctx.moveTo(335, 500);
    ctx.bezierCurveTo(220, 575, 110, 650, 0, 640);
    ctx.stroke();

    ctx.restore();

    // ------------------------------------------------------------------------
    // TIER 2: INTERNAL FACIAL CONTOURS & 25+ TAPERED HAIR BÉZIERS
    // ------------------------------------------------------------------------
    ctx.save();
    ctx.strokeStyle = ink;
    ctx.lineCap = 'round';

    // 1. Near Eye: Heavy S-Curve Eyeliner (4.0px) with Winged Lash Tip & Lower Lid (1.6px)
    ctx.lineWidth = 4.0;
    ctx.beginPath();
    ctx.moveTo(536, 409);
    ctx.bezierCurveTo(558, 382, 602, 381, 626, 406);
    ctx.stroke();

    ctx.lineWidth = 2.5;
    ctx.beginPath();
    ctx.moveTo(624, 406);
    ctx.lineTo(632, 402);
    ctx.stroke();

    ctx.lineWidth = 1.6;
    ctx.beginPath();
    ctx.moveTo(548, 414);
    ctx.bezierCurveTo(573, 426, 603, 425, 622, 410);
    ctx.stroke();

    ctx.lineWidth = 1.5;
    ctx.beginPath();
    ctx.moveTo(548, 394);
    ctx.bezierCurveTo(573, 382, 603, 382, 620, 398);
    ctx.stroke();

    // 2. Far Eye: Foreshortened Upper Lid (3.4px) & Lower Lid (1.5px)
    ctx.lineWidth = 3.4;
    ctx.beginPath();
    ctx.moveTo(676, 412);
    ctx.bezierCurveTo(692, 392, 724, 394, 738, 417);
    ctx.stroke();

    ctx.lineWidth = 1.5;
    ctx.beginPath();
    ctx.moveTo(686, 418);
    ctx.bezierCurveTo(700, 430, 724, 428, 734, 420);
    ctx.stroke();

    // 3. Fierce Eyebrows (Strong downward angle toward glabella)
    ctx.lineWidth = 3.2;
    ctx.beginPath();
    ctx.moveTo(522, 376);
    ctx.bezierCurveTo(555, 350, 595, 352, 635, 378);
    ctx.stroke();

    ctx.beginPath();
    ctx.moveTo(672, 386);
    ctx.bezierCurveTo(692, 370, 722, 372, 742, 395);
    ctx.stroke();

    drawFeatheringHatch(ctx, {x: 535, y: 370}, 80, 8, 8, 4, ink, 1.0);
    drawFeatheringHatch(ctx, {x: 680, y: 382}, 80, 6, 7, 4, ink, 1.0);

    // 4. Heroic Comic Nose
    ctx.lineWidth = 2.4;
    ctx.beginPath();
    ctx.moveTo(644, 395);
    ctx.lineTo(692, 478);
    ctx.stroke();

    ctx.lineWidth = 2.6;
    ctx.beginPath();
    ctx.moveTo(692, 478);
    ctx.bezierCurveTo(680, 488, 658, 488, 652, 482);
    ctx.stroke();

    ctx.lineWidth = 1.8;
    ctx.beginPath();
    ctx.moveTo(674, 474);
    ctx.bezierCurveTo(684, 472, 686, 484, 678, 484);
    ctx.stroke();

    // 5. Determined Open Grimace Mouth
    ctx.lineWidth = 2.6;
    ctx.beginPath();
    ctx.moveTo(622, 538);
    ctx.bezierCurveTo(637, 528, 653, 530, 682, 542);
    ctx.stroke();

    ctx.lineWidth = 2.0;
    ctx.beginPath();
    ctx.moveTo(624, 546);
    ctx.bezierCurveTo(639, 568, 665, 566, 680, 542);
    ctx.stroke();

    ctx.lineWidth = 1.8;
    ctx.beginPath();
    ctx.moveTo(631, 576);
    ctx.bezierCurveTo(645, 584, 659, 582, 665, 576);
    ctx.stroke();

    // 6. Ear & Gold Hoop Earring
    ctx.lineWidth = 2.2;
    ctx.beginPath();
    ctx.moveTo(422, 430);
    ctx.bezierCurveTo(398, 418, 392, 475, 424, 498);
    ctx.stroke();

    ctx.lineWidth = 1.6;
    ctx.beginPath();
    ctx.moveTo(418, 445);
    ctx.bezierCurveTo(408, 455, 412, 480, 422, 485);
    ctx.stroke();

    ctx.lineWidth = 2.5;
    ctx.beginPath();
    ctx.ellipse(400, 518, 19, 23, 0.2, 0, Math.PI * 2);
    ctx.stroke();

    ctx.lineWidth = 1.8;
    ctx.beginPath();
    ctx.ellipse(400, 518, 13, 17, 0.2, 0, Math.PI * 2);
    ctx.stroke();

    // 7. Bandana Contours & Tension Folds
    ctx.lineWidth = 3.0;
    ctx.beginPath();
    ctx.moveTo(355, 435);
    ctx.bezierCurveTo(420, 375, 495, 325, 585, 315);
    ctx.bezierCurveTo(620, 315, 645, 330, 658, 355);
    ctx.stroke();

    ctx.beginPath();
    ctx.moveTo(355, 465);
    ctx.bezierCurveTo(410, 440, 480, 388, 575, 365);
    ctx.bezierCurveTo(610, 372, 640, 372, 658, 355);
    ctx.stroke();

    ctx.lineWidth = 2.6;
    ctx.beginPath();
    ctx.ellipse(355, 455, 16, 22, 0.35, 0, Math.PI * 2);
    ctx.stroke();

    ctx.restore();

    // 8. 25+ Dynamic Tapered Bézier Hair Strands
    // Top Crown Waves
    drawTaperedStroke(ctx, {x:590, y:290}, {x:680, y:150}, {x:640, y:60}, {x:500, y:30}, 3.6);
    drawTaperedStroke(ctx, {x:500, y:30}, {x:410, y:50}, {x:350, y:90}, {x:240, y:190}, 3.2);
    drawTaperedStroke(ctx, {x:420, y:115}, {x:510, y:105}, {x:590, y:150}, {x:640, y:240}, 2.8);

    drawTaperedStroke(ctx, {x:560, y:300}, {x:600, y:170}, {x:510, y:100}, {x:390, y:65}, 3.0);
    drawTaperedStroke(ctx, {x:390, y:65}, {x:310, y:110}, {x:280, y:160}, {x:230, y:210}, 2.8);

    drawTaperedStroke(ctx, {x:530, y:310}, {x:530, y:210}, {x:440, y:150}, {x:330, y:120}, 2.8);
    drawTaperedStroke(ctx, {x:330, y:120}, {x:260, y:180}, {x:250, y:240}, {x:200, y:280}, 2.4);

    // Forehead Bangs & Wisps
    drawTaperedStroke(ctx, {x:585, y:315}, {x:605, y:230}, {x:650, y:170}, {x:730, y:150}, 3.2);
    drawTaperedStroke(ctx, {x:730, y:150}, {x:690, y:210}, {x:650, y:270}, {x:620, y:320}, 2.5);

    drawTaperedStroke(ctx, {x:680, y:300}, {x:725, y:280}, {x:770, y:295}, {x:780, y:325}, 3.2);
    drawTaperedStroke(ctx, {x:780, y:325}, {x:750, y:345}, {x:710, y:350}, {x:675, y:360}, 2.6);

    drawTaperedStroke(ctx, {x:550, y:360}, {x:575, y:390}, {x:595, y:420}, {x:580, y:455}, 2.8);
    drawTaperedStroke(ctx, {x:640, y:290}, {x:665, y:300}, {x:685, y:330}, {x:675, y:360}, 2.5);
    drawTaperedStroke(ctx, {x:445, y:450}, {x:402, y:470}, {x:372, y:510}, {x:348, y:570}, 2.6);

    // Ponytail Multi-Lock Curls
    drawTaperedStroke(ctx, {x:350, y:410}, {x:290, y:230}, {x:190, y:110}, {x:80, y:130}, 3.6);
    drawTaperedStroke(ctx, {x:80, y:130}, {x:130, y:180}, {x:230, y:230}, {x:300, y:290}, 2.6);

    drawTaperedStroke(ctx, {x:340, y:420}, {x:250, y:200}, {x:130, y:140}, {x:30, y:210}, 3.4);
    drawTaperedStroke(ctx, {x:30, y:210}, {x:60, y:250}, {x:170, y:250}, {x:250, y:310}, 2.5);

    drawTaperedStroke(ctx, {x:345, y:430}, {x:230, y:270}, {x:110, y:250}, {x:20, y:310}, 3.5);
    drawTaperedStroke(ctx, {x:20, y:310}, {x:50, y:350}, {x:150, y:330}, {x:240, y:360}, 2.6);

    drawTaperedStroke(ctx, {x:340, y:440}, {x:240, y:340}, {x:130, y:350}, {x:40, y:410}, 3.4);
    drawTaperedStroke(ctx, {x:40, y:410}, {x:70, y:440}, {x:180, y:390}, {x:260, y:400}, 2.4);

    drawTaperedStroke(ctx, {x:330, y:450}, {x:260, y:410}, {x:170, y:440}, {x:85, y:480}, 3.0);

    // ------------------------------------------------------------------------
    // TIER 3: FEATHERING & CROSS-HATCHING PASSES
    // ------------------------------------------------------------------------
    // Neck Muscle & Jawline Shadow Feathering
    drawFeatheringHatch(ctx, {x: 455, y: 530}, 75, 14, 28, 7, ink, 1.3);
    drawFeatheringHatch(ctx, {x: 485, y: 570}, 80, 12, 35, 7.5, ink, 1.2);
    drawFeatheringHatch(ctx, {x: 545, y: 610}, 85, 10, 30, 8, ink, 1.1);

    // Shirt Collar Wing Cross-Hatching
    drawFeatheringHatch(ctx, {x: 230, y: 650}, 45, 8, 22, 6, ink, 1.0);
    drawFeatheringHatch(ctx, {x: 230, y: 650}, -45, 8, 22, 6, ink, 1.0);

    drawFeatheringHatch(ctx, {x: 670, y: 670}, 135, 9, 24, 6, ink, 1.0);
    drawFeatheringHatch(ctx, {x: 670, y: 670}, -135, 9, 24, 6, ink, 1.0);

    // Cheek Contour Hatching
    drawFeatheringHatch(ctx, {x: 545, y: 450}, 60, 5, 12, 5, ink, 0.9);
    drawFeatheringHatch(ctx, {x: 715, y: 455}, 65, 4, 10, 4.5, ink, 0.8);
}

// ============================================================================
// 11. MASTER PIPELINE EXECUTION
// ============================================================================
drawBackgroundAndRigging(ctx);
drawHairLayers(ctx);
drawBandana(ctx);
drawClothingAndBody(ctx);
drawHeadAndCelShading(ctx);
drawFacialExpression(ctx);
drawForegroundCurlsAndHighlights(ctx);
drawMasterInks(ctx);

// Return canvas for rendering
canvas;
