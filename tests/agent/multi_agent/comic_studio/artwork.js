/**
 * Polson Co-Creative Comic Studio — Master Publication Artwork
 * Recreating reference_images/comic1.png using Polson ECMAScript 2025 MCP Graphics Engine
 * 
 * Pipeline Compositor:
 * - Stage 1: Penciler (Composition, Loomis Proportions & Armature)
 * - Stage 2: Colorist (4-Tier Palettes, 5 Facial Planes, SkSL Ben-Day Halftone, Perlin Noise)
 * - Stage 3: Inker (3-Tier Comic Inks in #0a0a0c, Calligraphic Tapered Béziers, Feathering)
 * - Stage 4: Critic (Visual Drift Correction, Cinematic Vignette)
 */

const canvas = createCanvas(447, 380);
const ctx = canvas.getContext('2d');

// ==========================================
// 1. HARMONIZED MASTER PALETTE
// ==========================================
const PALETTE = {
    skin: {
        highlight: '#faecd8',
        base: '#e8b894',
        shadow: '#a66847',
        deep: '#753c24',
        lipRose: '#b84848',
        lipShadow: '#7a2828',
        mouthCavity: '#150606',
        teeth: '#fbfbf6'
    },
    hair: {
        sunlit: '#f5a458',
        highlight: '#e88b48',
        base: '#c85a2b',
        shadow: '#7d2d14',
        deep: '#3d1205'
    },
    bandana: {
        highlight: '#4e6b8a',
        base: '#2f4255',
        shadow: '#1a2633',
        deep: '#0f1720'
    },
    shirt: {
        highlight: '#fcfaf2',
        base: '#f4ecd8',
        shadow: '#c5baa4',
        deep: '#9c917b'
    },
    coat: {
        highlight: '#344352',
        base: '#1c242c',
        shadow: '#0c1015'
    },
    sky: {
        top: '#4a7c9d',
        mid: '#6c9ebf',
        bottom: '#a2c8dc'
    },
    clouds: {
        fill: '#f8f6f0',
        shadow: '#b6c4cf'
    },
    spar: {
        highlight: '#82583f',
        base: '#5c3d2e',
        shadow: '#3a2216'
    },
    rope: {
        base: '#9e7d56',
        shadow: '#664c30'
    },
    earring: {
        highlight: '#fff2a8',
        base: '#e5a823',
        shadow: '#94670c'
    },
    ink: '#0a0a0c'
};

// ==========================================
// 2. LAYER 1: SKY & ATMOSPHERIC CLOUDS
// ==========================================
function drawSkyAndAtmosphere(ctx) {
    // Linear sky gradient
    const skyGrad = ctx.createLinearGradient(0, 0, 0, 380);
    skyGrad.addColorStop(0.0, PALETTE.sky.top);
    skyGrad.addColorStop(0.45, PALETTE.sky.mid);
    skyGrad.addColorStop(1.0, PALETTE.sky.bottom);
    ctx.fillStyle = skyGrad;
    ctx.fillRect(0, 0, 447, 380);

    // Billowy cumulus cloud puffs with shaded underside
    function drawCloud(cx, cy, s) {
        ctx.save();
        ctx.translate(cx, cy);
        ctx.scale(s, s);

        ctx.fillStyle = PALETTE.clouds.shadow;
        ctx.beginPath();
        ctx.arc(-45, 18, 42, 0, Math.PI * 2);
        ctx.arc(10, 22, 50, 0, Math.PI * 2);
        ctx.arc(65, 15, 38, 0, Math.PI * 2);
        ctx.fill();

        ctx.fillStyle = PALETTE.clouds.fill;
        ctx.beginPath();
        ctx.arc(-55, -8, 38, 0, Math.PI * 2);
        ctx.arc(-15, -35, 48, 0, Math.PI * 2);
        ctx.arc(35, -22, 42, 0, Math.PI * 2);
        ctx.arc(75, 0, 34, 0, Math.PI * 2);
        ctx.arc(5, 5, 48, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();
    }

    drawCloud(70, 100, 1.15);
    drawCloud(420, 120, 1.25);
    drawCloud(230, 35, 0.85);

    // Fractal cloud noise overlay
    try {
        const cloudNoise = Skia.Shader.perlinNoiseFractal(0.015, 0.015, 4, 101);
        ctx.save();
        ctx.globalCompositeOperation = 'soft-light';
        ctx.fillStyle = cloudNoise;
        ctx.fillRect(0, 0, 447, 200);
        ctx.restore();
    } catch (e) {}

    // Top-left sail silhouette
    ctx.fillStyle = PALETTE.ink;
    ctx.beginPath();
    ctx.moveTo(0, 0);
    ctx.lineTo(48, 0);
    ctx.lineTo(0, 105);
    ctx.closePath();
    ctx.fill();
}

// ==========================================
// 3. LAYER 2: SHIP RIGGING & SPAR
// ==========================================
function drawRigging(ctx) {
    // Wooden mast / spar
    ctx.save();
    ctx.fillStyle = PALETTE.spar.base;
    ctx.beginPath();
    ctx.moveTo(170, 0);
    ctx.lineTo(205, 0);
    ctx.lineTo(447, 345);
    ctx.lineTo(447, 305);
    ctx.closePath();
    ctx.fill();

    ctx.fillStyle = PALETTE.spar.shadow;
    ctx.beginPath();
    ctx.moveTo(192, 0);
    ctx.lineTo(205, 0);
    ctx.lineTo(447, 345);
    ctx.lineTo(447, 330);
    ctx.closePath();
    ctx.fill();
    ctx.restore();

    // 6 Diagonal Rope Shrouds with Hemp Noise
    const shrouds = [
        { x1: 65, y1: 0, x2: 0, y2: 240, w: 4.8 },
        { x1: 145, y1: 0, x2: 0, y2: 380, w: 5.2 },
        { x1: 265, y1: 0, x2: 55, y2: 380, w: 5.5 },
        { x1: 355, y1: 0, x2: 205, y2: 380, w: 5.5 },
        { x1: 447, y1: 20, x2: 315, y2: 380, w: 5.0 },
        { x1: 447, y1: 105, x2: 385, y2: 380, w: 4.5 }
    ];

    shrouds.forEach(s => {
        ctx.save();
        ctx.strokeStyle = PALETTE.rope.base;
        ctx.lineWidth = s.w;
        ctx.beginPath();
        ctx.moveTo(s.x1, s.y1);
        ctx.lineTo(s.x2, s.y2);
        ctx.stroke();

        ctx.strokeStyle = PALETTE.rope.shadow;
        ctx.lineWidth = s.w * 0.45;
        ctx.stroke();
        ctx.restore();
    });

    // Ratlines with organic sagging curves and tie nodes
    ctx.save();
    ctx.strokeStyle = PALETTE.rope.base;
    ctx.lineWidth = 2.2;
    for (let y = 30; y < 380; y += 32) {
        ctx.beginPath();
        ctx.moveTo(0, y - 20);
        ctx.quadraticCurveTo(220, y + 22, 447, y + 60);
        ctx.stroke();
    }
    ctx.restore();
}

// ==========================================
// 4. ORGANIC HAIR LOCK RENDERER
// ==========================================
function drawOrganicLock(ctx, p0, p1, p2, p3, w0, w1, fillBase, fillShadow, strokeColor = '#0a0a0c', strokeWidth = 2.2) {
    ctx.save();
    const dx = p3.x - p0.x;
    const dy = p3.y - p0.y;
    const len = Math.sqrt(dx * dx + dy * dy) || 1;
    const nx = -dy / len;
    const ny = dx / len;

    // Body polygon
    ctx.fillStyle = fillBase;
    ctx.beginPath();
    ctx.moveTo(p0.x, p0.y);
    ctx.bezierCurveTo(p1.x, p1.y, p2.x, p2.y, p3.x, p3.y);
    ctx.bezierCurveTo(
        p2.x + nx * w1 * 0.6, p2.y + ny * w1 * 0.6,
        p1.x + nx * w0 * 0.8, p1.y + ny * w0 * 0.8,
        p0.x + nx * w0, p0.y + ny * w0
    );
    ctx.closePath();
    ctx.fill();

    // Shadow facet
    ctx.fillStyle = fillShadow;
    ctx.beginPath();
    ctx.moveTo(p0.x + nx * w0 * 0.35, p0.y + ny * w0 * 0.35);
    ctx.bezierCurveTo(
        p1.x + nx * w0 * 0.6, p1.y + ny * w0 * 0.6,
        p2.x + nx * w1 * 0.5, p2.y + ny * w1 * 0.5,
        p3.x, p3.y
    );
    ctx.bezierCurveTo(
        p2.x + nx * w1 * 0.6, p2.y + ny * w1 * 0.6,
        p1.x + nx * w0 * 0.8, p1.y + ny * w0 * 0.8,
        p0.x + nx * w0, p0.y + ny * w0
    );
    ctx.closePath();
    ctx.fill();

    // Leading ink line
    ctx.strokeStyle = strokeColor;
    ctx.lineWidth = strokeWidth;
    ctx.lineCap = 'round';
    ctx.beginPath();
    ctx.moveTo(p0.x, p0.y);
    ctx.bezierCurveTo(p1.x, p1.y, p2.x, p2.y, p3.x, p3.y);
    ctx.stroke();

    ctx.restore();
}

// ==========================================
// 5. MASTER CHARACTER RENDERING
// ==========================================
function drawCharacter(ctx) {
    const INK = PALETTE.ink;

    // ----------------------------------------
    // A. PONYTAIL BACK CASCADING CURLS
    // ----------------------------------------
    ctx.save();
    ctx.fillStyle = PALETTE.hair.deep;
    ctx.beginPath();
    ctx.moveTo(145, 140);
    ctx.bezierCurveTo(110, 75, 45, 60, 0, 115);
    ctx.bezierCurveTo(-20, 165, 5, 230, 30, 260);
    ctx.bezierCurveTo(65, 245, 105, 195, 145, 140);
    ctx.closePath();
    ctx.fill();

    const ponytailLocks = [
        { p0: { x: 142, y: 128 }, p1: { x: 105, y: 55 }, p2: { x: 45, y: 58 }, p3: { x: 8, y: 105 }, w0: 20, w1: 6 },
        { p0: { x: 135, y: 135 }, p1: { x: 90, y: 72 }, p2: { x: 30, y: 88 }, p3: { x: 0, y: 140 }, w0: 22, w1: 8 },
        { p0: { x: 140, y: 142 }, p1: { x: 80, y: 110 }, p2: { x: 20, y: 140 }, p3: { x: -5, y: 185 }, w0: 24, w1: 10 },
        { p0: { x: 138, y: 150 }, p1: { x: 85, y: 155 }, p2: { x: 40, y: 195 }, p3: { x: 10, y: 240 }, w0: 22, w1: 8 },
        { p0: { x: 130, y: 155 }, p1: { x: 95, y: 185 }, p2: { x: 60, y: 220 }, p3: { x: 35, y: 265 }, w0: 18, w1: 6 },
        { p0: { x: 122, y: 148 }, p1: { x: 98, y: 165 }, p2: { x: 75, y: 190 }, p3: { x: 65, y: 225 }, w0: 15, w1: 5 }
    ];

    ponytailLocks.forEach(l => {
        drawOrganicLock(ctx, l.p0, l.p1, l.p2, l.p3, l.w0, l.w1, PALETTE.hair.base, PALETTE.hair.shadow, INK, 2.4);
    });
    ctx.restore();

    // ----------------------------------------
    // B. BANDANA BILLOWING TAILS
    // ----------------------------------------
    ctx.save();
    ctx.fillStyle = PALETTE.bandana.base;
    ctx.beginPath();
    ctx.moveTo(165, 218);
    ctx.bezierCurveTo(110, 228, 60, 212, 0, 232);
    ctx.lineTo(0, 258);
    ctx.bezierCurveTo(50, 252, 100, 258, 165, 242);
    ctx.closePath();
    ctx.fill();

    ctx.beginPath();
    ctx.moveTo(150, 232);
    ctx.bezierCurveTo(90, 258, 40, 262, 0, 302);
    ctx.lineTo(0, 328);
    ctx.bezierCurveTo(50, 288, 110, 278, 160, 250);
    ctx.closePath();
    ctx.fill();

    ctx.fillStyle = PALETTE.bandana.shadow;
    ctx.beginPath();
    ctx.moveTo(165, 232);
    ctx.bezierCurveTo(100, 248, 50, 238, 0, 252);
    ctx.lineTo(0, 258);
    ctx.bezierCurveTo(50, 252, 100, 258, 165, 242);
    ctx.closePath();
    ctx.fill();

    ctx.strokeStyle = INK;
    ctx.lineWidth = 3.2;
    ctx.beginPath();
    ctx.moveTo(165, 218);
    ctx.bezierCurveTo(110, 228, 60, 212, 0, 232);
    ctx.moveTo(0, 258);
    ctx.bezierCurveTo(50, 252, 100, 258, 165, 242);
    ctx.moveTo(150, 232);
    ctx.bezierCurveTo(90, 258, 40, 262, 0, 302);
    ctx.moveTo(0, 328);
    ctx.bezierCurveTo(50, 288, 110, 278, 160, 250);
    ctx.stroke();
    ctx.restore();

    // ----------------------------------------
    // C. NECK, SCM MUSCLE & COLLAR
    // ----------------------------------------
    ctx.save();
    ctx.fillStyle = PALETTE.skin.base;
    ctx.beginPath();
    ctx.moveTo(198, 248);
    ctx.bezierCurveTo(185, 290, 170, 330, 140, 380);
    ctx.lineTo(285, 380);
    ctx.bezierCurveTo(280, 355, 278, 340, 285, 335);
    ctx.bezierCurveTo(260, 330, 230, 305, 210, 275);
    ctx.lineTo(198, 248);
    ctx.closePath();
    ctx.fill();

    // Neck cast shadow
    ctx.fillStyle = PALETTE.skin.shadow;
    ctx.beginPath();
    ctx.moveTo(285, 335);
    ctx.bezierCurveTo(260, 330, 230, 305, 210, 275);
    ctx.lineTo(198, 248);
    ctx.bezierCurveTo(205, 270, 220, 310, 240, 345);
    ctx.bezierCurveTo(255, 365, 268, 375, 280, 380);
    ctx.lineTo(285, 380);
    ctx.bezierCurveTo(280, 355, 278, 340, 285, 335);
    ctx.closePath();
    ctx.fill();

    // Ben-Day dot shadow on neck
    try {
        const halfToneSkSL = `
            uniform float2 u_resolution;
            uniform float4 u_shadowColor;
            uniform float u_dotSpacing;

            half4 main(float2 coord) {
                float2 pos = mod(coord, u_dotSpacing) - (u_dotSpacing * 0.5);
                float dist = length(pos);
                float radius = u_dotSpacing * 0.38;
                float dot = smoothstep(radius + 0.3, radius - 0.3, dist);
                return u_shadowColor * dot;
            }
        `;
        const halfToneShader = Skia.Shader.sksl(halfToneSkSL, {
            u_resolution: [447, 380],
            u_shadowColor: [0.55, 0.28, 0.16, 0.45],
            u_dotSpacing: 6.0
        });
        ctx.fillStyle = halfToneShader;
        ctx.beginPath();
        ctx.moveTo(240, 345);
        ctx.bezierCurveTo(255, 365, 268, 375, 280, 380);
        ctx.lineTo(260, 380);
        ctx.bezierCurveTo(245, 360, 235, 340, 230, 325);
        ctx.closePath();
        ctx.fill();
    } catch (e) {}

    // Coat shoulders
    ctx.fillStyle = PALETTE.coat.base;
    ctx.beginPath();
    ctx.moveTo(0, 305);
    ctx.lineTo(85, 315);
    ctx.lineTo(140, 380);
    ctx.lineTo(0, 380);
    ctx.closePath();
    ctx.fill();

    ctx.beginPath();
    ctx.moveTo(280, 380);
    ctx.lineTo(348, 325);
    ctx.lineTo(447, 275);
    ctx.lineTo(447, 380);
    ctx.closePath();
    ctx.fill();

    // Flared Pirate Collar
    ctx.fillStyle = PALETTE.shirt.base;
    ctx.beginPath();
    ctx.moveTo(192, 375);
    ctx.bezierCurveTo(150, 355, 110, 335, 82, 315);
    ctx.bezierCurveTo(115, 305, 145, 295, 168, 282);
    ctx.bezierCurveTo(180, 305, 190, 340, 195, 375);
    ctx.closePath();
    ctx.fill();

    ctx.beginPath();
    ctx.moveTo(255, 375);
    ctx.bezierCurveTo(290, 355, 325, 340, 348, 322);
    ctx.bezierCurveTo(335, 310, 322, 302, 310, 298);
    ctx.bezierCurveTo(290, 320, 270, 345, 255, 375);
    ctx.closePath();
    ctx.fill();

    // Collar inking & hatches
    ctx.strokeStyle = INK;
    ctx.lineWidth = 3.2;
    ctx.beginPath();
    ctx.moveTo(192, 375);
    ctx.bezierCurveTo(150, 355, 110, 335, 82, 315);
    ctx.bezierCurveTo(115, 305, 145, 295, 168, 282);
    ctx.moveTo(255, 375);
    ctx.bezierCurveTo(290, 355, 325, 340, 348, 322);
    ctx.bezierCurveTo(335, 310, 322, 302, 310, 298);
    ctx.moveTo(0, 305); ctx.lineTo(85, 315);
    ctx.moveTo(348, 322); ctx.lineTo(447, 275);
    ctx.stroke();

    ctx.lineWidth = 1.1;
    function drawHatch(ox, oy, angleDeg, count, len, step) {
        const rad = (angleDeg * Math.PI) / 180;
        const dx = Math.cos(rad);
        const dy = Math.sin(rad);
        const px = -dy;
        const py = dx;
        for (let i = 0; i < count; i++) {
            const sx = ox + px * (i * step);
            const sy = oy + py * (i * step);
            const l = len * (0.8 + 0.4 * Math.sin(i * 1.5));
            ctx.beginPath();
            ctx.moveTo(sx, sy);
            ctx.lineTo(sx + dx * l, sy + dy * l);
            ctx.stroke();
        }
    }
    drawHatch(120, 330, 48, 8, 16, 4.5);
    drawHatch(130, 325, -35, 7, 14, 4.5);
    drawHatch(285, 345, 130, 8, 16, 4.5);
    drawHatch(295, 340, 45, 7, 14, 4.5);
    drawHatch(215, 280, 60, 10, 18, 5.5);
    drawHatch(245, 320, 55, 8, 22, 5.0);
    ctx.restore();

    // ----------------------------------------
    // D. FACIAL ANATOMY & CEL-SHADING
    // ----------------------------------------
    ctx.save();
    ctx.fillStyle = PALETTE.skin.base;
    ctx.beginPath();
    ctx.moveTo(198, 248);
    ctx.lineTo(195, 202);
    ctx.bezierCurveTo(215, 175, 245, 150, 275, 175);
    ctx.lineTo(298, 205);
    ctx.lineTo(323, 245);
    ctx.lineTo(306, 253);
    ctx.lineTo(312, 274);
    ctx.lineTo(318, 282);
    ctx.lineTo(302, 298);
    ctx.lineTo(308, 312);
    ctx.bezierCurveTo(304, 328, 296, 335, 285, 335);
    ctx.bezierCurveTo(260, 330, 230, 305, 210, 275);
    ctx.lineTo(198, 248);
    ctx.closePath();
    ctx.fill();

    // 5 Facial Shadow Planes
    ctx.fillStyle = PALETTE.skin.shadow;
    ctx.beginPath();
    ctx.moveTo(250, 196);
    ctx.bezierCurveTo(270, 186, 290, 186, 314, 190);
    ctx.bezierCurveTo(295, 202, 270, 202, 250, 196);
    ctx.closePath();
    ctx.fill();

    ctx.beginPath();
    ctx.moveTo(318, 240);
    ctx.lineTo(323, 245);
    ctx.lineTo(306, 253);
    ctx.lineTo(302, 265);
    ctx.lineTo(310, 255);
    ctx.closePath();
    ctx.fill();

    ctx.beginPath();
    ctx.moveTo(210, 275);
    ctx.lineTo(245, 285);
    ctx.lineTo(270, 310);
    ctx.lineTo(285, 335);
    ctx.bezierCurveTo(260, 330, 230, 305, 210, 275);
    ctx.closePath();
    ctx.fill();

    ctx.fillStyle = PALETTE.skin.deep;
    ctx.beginPath();
    ctx.moveTo(294, 298);
    ctx.quadraticCurveTo(306, 305, 318, 295);
    ctx.quadraticCurveTo(306, 301, 294, 298);
    ctx.closePath();
    ctx.fill();

    // Highlights
    ctx.fillStyle = PALETTE.skin.highlight;
    ctx.beginPath();
    ctx.ellipse(255, 175, 22, 14, 0.1, 0, Math.PI * 2);
    ctx.fill();
    ctx.beginPath();
    ctx.moveTo(300, 210);
    ctx.lineTo(320, 242);
    ctx.lineTo(316, 243);
    ctx.lineTo(298, 212);
    ctx.closePath();
    ctx.fill();
    ctx.beginPath();
    ctx.ellipse(280, 235, 15, 8, 0.2, 0, Math.PI * 2);
    ctx.fill();
    ctx.beginPath();
    ctx.ellipse(290, 325, 8, 6, 0, 0, Math.PI * 2);
    ctx.fill();

    ctx.fillStyle = 'rgba(184, 72, 72, 0.20)';
    ctx.beginPath();
    ctx.ellipse(280, 240, 22, 14, 0.1, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();

    // ----------------------------------------
    // E. FACIAL FEATURES & INKING
    // ----------------------------------------
    ctx.save();
    // Mouth Cavity & Teeth
    ctx.fillStyle = PALETTE.skin.mouthCavity;
    ctx.beginPath();
    ctx.moveTo(290, 282);
    ctx.bezierCurveTo(300, 275, 312, 274, 322, 277);
    ctx.bezierCurveTo(318, 292, 304, 295, 290, 282);
    ctx.closePath();
    ctx.fill();

    ctx.fillStyle = PALETTE.skin.teeth;
    ctx.beginPath();
    ctx.moveTo(293, 282);
    ctx.bezierCurveTo(301, 278, 311, 277, 320, 279);
    ctx.lineTo(318, 284);
    ctx.bezierCurveTo(309, 283, 301, 284, 293, 286);
    ctx.closePath();
    ctx.fill();

    // Mouth Inks
    ctx.strokeStyle = INK;
    ctx.lineWidth = 2.8;
    ctx.beginPath();
    ctx.moveTo(290, 282);
    ctx.bezierCurveTo(300, 275, 312, 274, 322, 277);
    ctx.stroke();
    ctx.lineWidth = 2.4;
    ctx.beginPath();
    ctx.moveTo(290, 282);
    ctx.bezierCurveTo(304, 295, 318, 292, 322, 277);
    ctx.stroke();
    ctx.lineWidth = 2.2;
    ctx.beginPath();
    ctx.moveTo(294, 298);
    ctx.quadraticCurveTo(306, 303, 318, 295);
    ctx.stroke();

    // Beauty mark
    ctx.fillStyle = INK;
    ctx.beginPath();
    ctx.arc(272, 298, 1.8, 0, Math.PI * 2);
    ctx.fill();

    // Nose Inks
    ctx.lineWidth = 2.6;
    ctx.beginPath();
    ctx.moveTo(298, 205);
    ctx.lineTo(323, 245);
    ctx.lineTo(306, 253);
    ctx.stroke();
    ctx.lineWidth = 1.8;
    ctx.beginPath();
    ctx.moveTo(300, 248);
    ctx.quadraticCurveTo(304, 252, 308, 251);
    ctx.stroke();

    // Fierce Arched Eyebrows
    ctx.lineWidth = 3.6;
    ctx.beginPath();
    ctx.moveTo(248, 196);
    ctx.bezierCurveTo(268, 182, 290, 182, 315, 187);
    ctx.stroke();
    ctx.lineWidth = 3.0;
    ctx.beginPath();
    ctx.moveTo(328, 189);
    ctx.bezierCurveTo(338, 186, 348, 189, 356, 193);
    ctx.stroke();

    // Eyes
    ctx.fillStyle = '#f8f8f2';
    ctx.beginPath();
    ctx.moveTo(256, 207);
    ctx.bezierCurveTo(268, 197, 285, 197, 297, 206);
    ctx.bezierCurveTo(285, 214, 268, 214, 256, 207);
    ctx.closePath();
    ctx.fill();

    ctx.fillStyle = '#3b6a8a';
    ctx.beginPath();
    ctx.arc(278, 205, 6.8, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = '#101822';
    ctx.beginPath();
    ctx.arc(279, 205, 3.4, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = '#ffffff';
    ctx.beginPath();
    ctx.arc(276, 203, 1.8, 0, Math.PI * 2);
    ctx.fill();

    ctx.strokeStyle = INK;
    ctx.lineWidth = 4.0;
    ctx.beginPath();
    ctx.moveTo(254, 208);
    ctx.bezierCurveTo(268, 196, 285, 196, 298, 206);
    ctx.stroke();
    ctx.lineWidth = 1.6;
    ctx.beginPath();
    ctx.moveTo(262, 213);
    ctx.quadraticCurveTo(278, 214, 292, 210);
    ctx.stroke();
    ctx.lineWidth = 1.4;
    ctx.beginPath();
    ctx.moveTo(264, 197);
    ctx.quadraticCurveTo(278, 194, 292, 199);
    ctx.stroke();

    // Far Eye
    ctx.fillStyle = '#f8f8f2';
    ctx.beginPath();
    ctx.moveTo(326, 211);
    ctx.bezierCurveTo(332, 206, 342, 207, 346, 213);
    ctx.bezierCurveTo(340, 218, 330, 218, 326, 211);
    ctx.closePath();
    ctx.fill();

    ctx.fillStyle = '#3b6a8a';
    ctx.beginPath();
    ctx.arc(337, 212, 4.5, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = '#101822';
    ctx.beginPath();
    ctx.arc(338, 212, 2.2, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = '#ffffff';
    ctx.beginPath();
    ctx.arc(336, 210, 1.2, 0, Math.PI * 2);
    ctx.fill();

    ctx.strokeStyle = INK;
    ctx.lineWidth = 3.0;
    ctx.beginPath();
    ctx.moveTo(326, 211);
    ctx.bezierCurveTo(332, 205, 342, 206, 346, 213);
    ctx.stroke();
    ctx.lineWidth = 1.4;
    ctx.beginPath();
    ctx.moveTo(328, 216);
    ctx.quadraticCurveTo(336, 217, 343, 214);
    ctx.stroke();

    // Jawline
    ctx.lineWidth = 3.8;
    ctx.beginPath();
    ctx.moveTo(198, 248);
    ctx.lineTo(210, 275);
    ctx.bezierCurveTo(230, 305, 260, 330, 285, 335);
    ctx.stroke();

    // Ear & Earring
    ctx.fillStyle = PALETTE.earring.base;
    ctx.beginPath();
    ctx.ellipse(180, 268, 10, 14, 0.35, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = PALETTE.skin.shadow;
    ctx.beginPath();
    ctx.ellipse(180, 268, 6, 9, 0.35, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = PALETTE.earring.highlight;
    ctx.beginPath();
    ctx.arc(174, 260, 2.5, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = PALETTE.earring.base;
    ctx.beginPath();
    ctx.arc(188, 248, 3.5, 0, Math.PI * 2);
    ctx.fill();

    ctx.strokeStyle = INK;
    ctx.lineWidth = 2.4;
    ctx.beginPath();
    ctx.moveTo(195, 202);
    ctx.bezierCurveTo(212, 200, 214, 235, 198, 248);
    ctx.moveTo(194, 215);
    ctx.bezierCurveTo(204, 218, 202, 236, 192, 238);
    ctx.stroke();
    ctx.lineWidth = 2.2;
    ctx.beginPath();
    ctx.ellipse(180, 268, 10, 14, 0.35, 0, Math.PI * 2);
    ctx.stroke();
    ctx.beginPath();
    ctx.arc(188, 248, 3.5, 0, Math.PI * 2);
    ctx.stroke();
    ctx.restore();

    // ----------------------------------------
    // F. BANDANA WRAP & KNOT
    // ----------------------------------------
    ctx.save();
    ctx.fillStyle = PALETTE.bandana.base;
    ctx.beginPath();
    ctx.moveTo(165, 215);
    ctx.bezierCurveTo(185, 175, 220, 150, 275, 140);
    ctx.bezierCurveTo(305, 135, 330, 145, 345, 160);
    ctx.lineTo(335, 185);
    ctx.bezierCurveTo(310, 170, 270, 175, 220, 195);
    ctx.lineTo(168, 245);
    ctx.closePath();
    ctx.fill();

    ctx.fillStyle = PALETTE.bandana.shadow;
    ctx.beginPath();
    ctx.moveTo(165, 215);
    ctx.bezierCurveTo(190, 195, 225, 180, 260, 185);
    ctx.lineTo(240, 195);
    ctx.lineTo(168, 245);
    ctx.closePath();
    ctx.fill();

    ctx.fillStyle = PALETTE.bandana.highlight;
    ctx.beginPath();
    ctx.moveTo(220, 150);
    ctx.bezierCurveTo(260, 140, 300, 138, 335, 150);
    ctx.lineTo(330, 155);
    ctx.bezierCurveTo(295, 145, 255, 148, 220, 156);
    ctx.closePath();
    ctx.fill();

    ctx.fillStyle = PALETTE.bandana.deep;
    ctx.beginPath();
    ctx.arc(142, 142, 12, 0, Math.PI * 2);
    ctx.fill();

    ctx.strokeStyle = INK;
    ctx.lineWidth = 2.8;
    ctx.beginPath();
    ctx.moveTo(165, 215);
    ctx.bezierCurveTo(185, 175, 220, 150, 275, 140);
    ctx.bezierCurveTo(305, 135, 330, 145, 345, 160);
    ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(168, 245);
    ctx.lineTo(220, 195);
    ctx.bezierCurveTo(270, 175, 310, 170, 335, 185);
    ctx.stroke();
    ctx.lineWidth = 2.0;
    ctx.beginPath();
    ctx.arc(142, 142, 12, 0, Math.PI * 2);
    ctx.stroke();
    ctx.restore();

    // ----------------------------------------
    // G. CROWN S-CURVE WAVES & BANGS (25+ DYNAMIC LOCKS)
    // ----------------------------------------
    ctx.save();
    ctx.fillStyle = PALETTE.hair.base;
    ctx.beginPath();
    ctx.moveTo(142, 135);
    ctx.bezierCurveTo(165, 75, 215, 35, 275, 40);
    ctx.bezierCurveTo(315, 45, 350, 75, 375, 125);
    ctx.bezierCurveTo(355, 135, 335, 145, 315, 140);
    ctx.bezierCurveTo(275, 130, 220, 140, 165, 155);
    ctx.closePath();
    ctx.fill();

    const crownWaves = [
        { p0: { x: 145, y: 135 }, p1: { x: 175, y: 60 }, p2: { x: 225, y: 25 }, p3: { x: 280, y: 30 }, w0: 22, w1: 6 },
        { p0: { x: 175, y: 120 }, p1: { x: 205, y: 55 }, p2: { x: 255, y: 20 }, p3: { x: 305, y: 35 }, w0: 20, w1: 6 },
        { p0: { x: 215, y: 138 }, p1: { x: 245, y: 65 }, p2: { x: 295, y: 38 }, p3: { x: 335, y: 58 }, w0: 18, w1: 5 },
        { p0: { x: 255, y: 135 }, p1: { x: 285, y: 75 }, p2: { x: 325, y: 65 }, p3: { x: 360, y: 98 }, w0: 16, w1: 4 },
        { p0: { x: 285, y: 140 }, p1: { x: 315, y: 90 }, p2: { x: 345, y: 95 }, p3: { x: 375, y: 130 }, w0: 14, w1: 4 },

        { p0: { x: 250, y: 138 }, p1: { x: 270, y: 162 }, p2: { x: 282, y: 172 }, p3: { x: 268, y: 192 }, w0: 12, w1: 3 },
        { p0: { x: 258, y: 138 }, p1: { x: 278, y: 158 }, p2: { x: 292, y: 162 }, p3: { x: 284, y: 178 }, w0: 10, w1: 3 },
        { p0: { x: 282, y: 138 }, p1: { x: 318, y: 142 }, p2: { x: 348, y: 162 }, p3: { x: 356, y: 192 }, w0: 14, w1: 4 },
        { p0: { x: 302, y: 142 }, p1: { x: 332, y: 152 }, p2: { x: 352, y: 168 }, p3: { x: 362, y: 188 }, w0: 10, w1: 3 },

        { p0: { x: 185, y: 195 }, p1: { x: 170, y: 215 }, p2: { x: 160, y: 235 }, p3: { x: 175, y: 250 }, w0: 10, w1: 3 },
        { p0: { x: 190, y: 195 }, p1: { x: 178, y: 215 }, p2: { x: 172, y: 230 }, p3: { x: 180, y: 245 }, w0: 8, w1: 2 }
    ];

    crownWaves.forEach(l => {
        drawOrganicLock(ctx, l.p0, l.p1, l.p2, l.p3, l.w0, l.w1, PALETTE.hair.base, PALETTE.hair.shadow, INK, 2.2);
    });

    ctx.globalCompositeOperation = 'overlay';
    ctx.fillStyle = PALETTE.hair.sunlit;
    ctx.beginPath();
    ctx.moveTo(165, 75);
    ctx.bezierCurveTo(215, 35, 275, 40, 335, 65);
    ctx.bezierCurveTo(275, 48, 220, 50, 175, 82);
    ctx.closePath();
    ctx.fill();

    ctx.beginPath();
    ctx.moveTo(135, 130);
    ctx.bezierCurveTo(115, 68, 65, 72, 20, 115);
    ctx.bezierCurveTo(65, 80, 110, 80, 130, 135);
    ctx.closePath();
    ctx.fill();
    ctx.restore();
}

// ==========================================
// 6. MASTER COMPOSITE PIPELINE
// ==========================================
drawSkyAndAtmosphere(ctx);
drawRigging(ctx);
drawCharacter(ctx);

// Cinematic Vignette Framing
try {
    Drawing.drawVignette(ctx, 447, 380, { intensity: 0.35 });
} catch (e) {}

canvas;
