/**
 * Artwork: Instructional Perspective Lighting Diagram Reproduction
 * Target Engine: Polson Graphics MCP Server (Canvas2D / Skia)
 * Dimensions: 900 x 1200
 */

const W = 900;
const H = 1200;

const canvas = createCanvas(W, H);
const ctx = canvas.getContext("2d");

// =========================================================================
// COLOR PALETTE & STYLES
// =========================================================================
const PALETTE = {
  bg: '#FFFFFF',
  frame: '#000000',
  horizon: '#000000',
  orange: '#FA7B05',
  orangeDark: '#E06800',
  orangeText: '#FA7B05',
  cubeTop: '#FFFFFF',
  cubeLeft: '#1A33A8',
  cubeRight: '#2B4BC4',
  shadow: '#2B4BC4',
  wireframe: '#000000',
  text: '#1C1C1C',
  footerText: '#222222',
  badgeBg: '#FA7B05',
  badgeText: '#FFFFFF'
};

// Clear canvas background
ctx.fillStyle = PALETTE.bg;
ctx.fillRect(0, 0, W, H);

// =========================================================================
// GEOMETRIC UTILITIES
// =========================================================================
function lineIntersect(p1, p2, p3, p4) {
  const x1 = p1.x, y1 = p1.y, x2 = p2.x, y2 = p2.y;
  const x3 = p3.x, y3 = p3.y, x4 = p4.x, y4 = p4.y;
  const denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
  if (Math.abs(denom) < 1e-6) return null;
  const t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;
  return { x: x1 + t * (x2 - x1), y: y1 + t * (y2 - y1) };
}

function projectRay(start, through, extraDist) {
  const dx = through.x - start.x;
  const dy = through.y - start.y;
  const d = Math.hypot(dx, dy);
  if (d < 1e-6) return { x: through.x, y: through.y };
  return {
    x: through.x + (dx / d) * extraDist,
    y: through.y + (dy / d) * extraDist
  };
}

// =========================================================================
// ICON DRAWING HELPERS
// =========================================================================
function drawLightBulb(cx, cy, radius, cordTopY) {
  ctx.save();
  
  // Hanging cord
  ctx.strokeStyle = PALETTE.orange;
  ctx.lineWidth = 1.8;
  ctx.beginPath();
  ctx.moveTo(cx, cordTopY);
  ctx.lineTo(cx, cy - radius * 1.3);
  ctx.stroke();

  // Socket base
  const socketW = radius * 0.75;
  const socketH = radius * 0.45;
  const socketY = cy - radius * 1.25;

  ctx.fillStyle = PALETTE.bg;
  ctx.fillRect(cx - socketW / 2, socketY, socketW, socketH);
  ctx.strokeStyle = PALETTE.orange;
  ctx.lineWidth = 1.8;
  ctx.strokeRect(cx - socketW / 2, socketY, socketW, socketH);

  // Smooth bulb body
  ctx.beginPath();
  ctx.moveTo(cx - socketW / 2, socketY + socketH);
  ctx.bezierCurveTo(
    cx - socketW / 2, cy - radius * 0.3,
    cx - radius, cy - radius * 0.5,
    cx - radius, cy
  );
  ctx.arc(cx, cy, radius, Math.PI, 0, true);
  ctx.bezierCurveTo(
    cx + radius, cy - radius * 0.5,
    cx + socketW / 2, cy - radius * 0.3,
    cx + socketW / 2, socketY + socketH
  );
  ctx.closePath();
  ctx.fillStyle = PALETTE.bg;
  ctx.fill();
  ctx.stroke();

  // Center filament light dot
  ctx.fillStyle = PALETTE.orange;
  ctx.beginPath();
  ctx.arc(cx, cy, 3.5, 0, Math.PI * 2);
  ctx.fill();

  ctx.restore();
}

function drawSunIcon(cx, cy, radius) {
  ctx.save();
  ctx.strokeStyle = PALETTE.orange;
  ctx.fillStyle = PALETTE.orange;
  ctx.lineWidth = 3.2;
  ctx.lineCap = 'round';

  // Central ring
  ctx.beginPath();
  ctx.arc(cx, cy, radius * 0.55, 0, Math.PI * 2);
  ctx.stroke();

  // 8 Radial rays
  const numRays = 8;
  const rInner = radius * 0.75;
  const rOuter = radius * 1.4;
  for (let i = 0; i < numRays; i++) {
    const angle = (i * 2 * Math.PI) / numRays;
    const x1 = cx + Math.cos(angle) * rInner;
    const y1 = cy + Math.sin(angle) * rInner;
    const x2 = cx + Math.cos(angle) * rOuter;
    const y2 = cy + Math.sin(angle) * rOuter;
    ctx.beginPath();
    ctx.moveTo(x1, y1);
    ctx.lineTo(x2, y2);
    ctx.stroke();
  }

  ctx.restore();
}

// =========================================================================
// LAYOUT DIMENSIONS (900 x 1200)
// =========================================================================
const P1 = {
  x: 68,
  y: 99,
  w: 395,
  h: 340,
  horizonY: 247
};

const P2 = {
  x: 68,
  y: 451,
  w: 395,
  h: 340,
  horizonY: 598
};

const P3 = {
  x: 68,
  y: 803,
  w: 312,
  h: 298,
  horizonY: 953
};

const P4 = {
  x: 392,
  y: 803,
  w: 312,
  h: 298,
  horizonY: 953
};

// =========================================================================
// 1. TOP PANEL (PANEL 1)
// =========================================================================
function drawPanel1() {
  // Panel frame
  ctx.strokeStyle = PALETTE.frame;
  ctx.lineWidth = 2.4;
  ctx.strokeRect(P1.x, P1.y, P1.w, P1.h);

  // Horizon line
  ctx.beginPath();
  ctx.moveTo(P1.x, P1.horizonY);
  ctx.lineTo(P1.x + P1.w, P1.horizonY);
  ctx.stroke();

  // Light bulb & Ground Point
  const bulbX = P1.x + 45;
  const bulbY = P1.y + 31;
  const groundY = P1.y + 201;

  drawLightBulb(bulbX, bulbY, 17, P1.y);

  // Vertical drop line
  ctx.strokeStyle = PALETTE.orange;
  ctx.lineWidth = 1.8;
  ctx.beginPath();
  ctx.moveTo(bulbX, bulbY);
  ctx.lineTo(bulbX, groundY);
  ctx.stroke();

  // Ground dot
  ctx.fillStyle = PALETTE.orange;
  ctx.beginPath();
  ctx.arc(bulbX, groundY, 4, 0, Math.PI * 2);
  ctx.fill();

  // 3D cube vertices in P1
  const TL = { x: 218, y: 269 };
  const TF = { x: 271, y: 305 };
  const TR = { x: 309, y: 252 };
  const TB = { x: 257, y: 245 };

  const BL = { x: 218, y: 295 };
  const BF = { x: 271, y: 331 };
  const BR = { x: 309, y: 295 };

  const L = { x: bulbX, y: bulbY };
  const G = { x: bulbX, y: groundY };

  // Calculate Shadow Intersections
  const S_TL = lineIntersect(L, TL, G, BL);
  const S_TF = lineIntersect(L, TF, G, BF);
  const S_TR = lineIntersect(L, TR, G, BR);

  // Draw Cast Shadow Polygon
  ctx.fillStyle = PALETTE.shadow;
  ctx.beginPath();
  ctx.moveTo(BL.x, BL.y);
  ctx.lineTo(S_TL.x, S_TL.y);
  ctx.lineTo(S_TF.x, S_TF.y);
  ctx.lineTo(S_TR.x, S_TR.y);
  ctx.lineTo(BR.x, BR.y);
  ctx.lineTo(BF.x, BF.y);
  ctx.closePath();
  ctx.fill();

  // Draw Orange Perspective Projection Rays
  ctx.strokeStyle = PALETTE.orange;
  ctx.lineWidth = 1.6;

  // Ground rays (extending slightly past shadow vertices)
  const gExtTL = projectRay(G, S_TL, 15);
  const gExtTF = projectRay(G, S_TF, 25);
  const gExtTR = projectRay(G, S_TR, 15);

  ctx.beginPath();
  ctx.moveTo(G.x, G.y); ctx.lineTo(gExtTL.x, gExtTL.y);
  ctx.moveTo(G.x, G.y); ctx.lineTo(gExtTF.x, gExtTF.y);
  ctx.moveTo(G.x, G.y); ctx.lineTo(gExtTR.x, gExtTR.y);
  ctx.stroke();

  // Light rays (extending to shadow vertices)
  ctx.beginPath();
  ctx.moveTo(L.x, L.y); ctx.lineTo(S_TL.x, S_TL.y);
  ctx.moveTo(L.x, L.y); ctx.lineTo(S_TF.x, S_TF.y);
  ctx.moveTo(L.x, L.y); ctx.lineTo(S_TR.x, S_TR.y);
  ctx.stroke();

  // Draw Blue 3D Cube
  // 1. Left Face (Dark Blue)
  ctx.fillStyle = PALETTE.cubeLeft;
  ctx.beginPath();
  ctx.moveTo(TL.x, TL.y); ctx.lineTo(TF.x, TF.y); ctx.lineTo(BF.x, BF.y); ctx.lineTo(BL.x, BL.y);
  ctx.closePath(); ctx.fill();
  ctx.strokeStyle = PALETTE.frame; ctx.lineWidth = 2.2; ctx.stroke();

  // 2. Right Face (Medium Blue)
  ctx.fillStyle = PALETTE.cubeRight;
  ctx.beginPath();
  ctx.moveTo(TF.x, TF.y); ctx.lineTo(TR.x, TR.y); ctx.lineTo(BR.x, BR.y); ctx.lineTo(BF.x, BF.y);
  ctx.closePath(); ctx.fill(); ctx.stroke();

  // 3. Top Face (White)
  ctx.fillStyle = PALETTE.cubeTop;
  ctx.beginPath();
  ctx.moveTo(TB.x, TB.y); ctx.lineTo(TL.x, TL.y); ctx.lineTo(TF.x, TF.y); ctx.lineTo(TR.x, TR.y);
  ctx.closePath(); ctx.fill(); ctx.stroke();
}

// =========================================================================
// 2. MIDDLE PANEL (PANEL 2)
// =========================================================================
function drawPanel2() {
  // Panel frame
  ctx.strokeStyle = PALETTE.frame;
  ctx.lineWidth = 2.4;
  ctx.strokeRect(P2.x, P2.y, P2.w, P2.h);

  // Horizon line
  ctx.beginPath();
  ctx.moveTo(P2.x, P2.horizonY);
  ctx.lineTo(P2.x + P2.w, P2.horizonY);
  ctx.stroke();

  // Light bulb & Ground Point
  const bulbX = P2.x + 45;
  const bulbY = P2.y + 31;
  const groundY = P2.y + 201;

  drawLightBulb(bulbX, bulbY, 17, P2.y);

  // Vertical drop line
  ctx.strokeStyle = PALETTE.orange;
  ctx.lineWidth = 1.8;
  ctx.beginPath();
  ctx.moveTo(bulbX, bulbY);
  ctx.lineTo(bulbX, groundY);
  ctx.stroke();

  // Ground dot
  ctx.fillStyle = PALETTE.orange;
  ctx.beginPath();
  ctx.arc(bulbX, groundY, 4, 0, Math.PI * 2);
  ctx.fill();

  // Cube Vertices in P2
  const TL = { x: 218, y: 269 + 352 };
  const TF = { x: 271, y: 305 + 352 };
  const TR = { x: 309, y: 252 + 352 };
  const TB = { x: 257, y: 245 + 352 };

  const BL = { x: 218, y: 295 + 352 };
  const BF = { x: 271, y: 331 + 352 };
  const BR = { x: 309, y: 295 + 352 };

  const L = { x: bulbX, y: bulbY };
  const G = { x: bulbX, y: groundY };

  const S_TL = lineIntersect(L, TL, G, BL);
  const S_TF = lineIntersect(L, TF, G, BF);
  const S_TR = lineIntersect(L, TR, G, BR);

  // Cast Shadow
  ctx.fillStyle = PALETTE.shadow;
  ctx.beginPath();
  ctx.moveTo(BL.x, BL.y);
  ctx.lineTo(S_TL.x, S_TL.y);
  ctx.lineTo(S_TF.x, S_TF.y);
  ctx.lineTo(S_TR.x, S_TR.y);
  ctx.lineTo(BR.x, BR.y);
  ctx.lineTo(BF.x, BF.y);
  ctx.closePath();
  ctx.fill();

  // Orange Projection Rays
  ctx.strokeStyle = PALETTE.orange;
  ctx.lineWidth = 1.6;

  const gExtTL = projectRay(G, S_TL, 15);
  const gExtTF = projectRay(G, S_TF, 25);
  const gExtTR = projectRay(G, S_TR, 15);

  ctx.beginPath();
  ctx.moveTo(G.x, G.y); ctx.lineTo(gExtTL.x, gExtTL.y);
  ctx.moveTo(G.x, G.y); ctx.lineTo(gExtTF.x, gExtTF.y);
  ctx.moveTo(G.x, G.y); ctx.lineTo(gExtTR.x, gExtTR.y);
  ctx.moveTo(L.x, L.y); ctx.lineTo(S_TL.x, S_TL.y);
  ctx.moveTo(L.x, L.y); ctx.lineTo(S_TF.x, S_TF.y);
  ctx.moveTo(L.x, L.y); ctx.lineTo(S_TR.x, S_TR.y);
  ctx.stroke();

  // Right Vanishing Point on Horizon
  const RVP = { x: P2.x + P2.w, y: P2.horizonY };

  // Vanishing Lines (Black) to RVP
  ctx.strokeStyle = PALETTE.frame;
  ctx.lineWidth = 1.4;
  ctx.beginPath();
  ctx.moveTo(TB.x, TB.y); ctx.lineTo(RVP.x, RVP.y);
  ctx.moveTo(TL.x, TL.y); ctx.lineTo(RVP.x, RVP.y);
  ctx.moveTo(TF.x, TF.y); ctx.lineTo(RVP.x, RVP.y);
  ctx.moveTo(BF.x, BF.y); ctx.lineTo(RVP.x, RVP.y);
  ctx.moveTo(S_TF.x, S_TF.y); ctx.lineTo(RVP.x, RVP.y);
  ctx.moveTo(S_TR.x, S_TR.y); ctx.lineTo(RVP.x, RVP.y);
  ctx.stroke();

  // Cube Faces
  ctx.fillStyle = PALETTE.cubeLeft;
  ctx.beginPath();
  ctx.moveTo(TL.x, TL.y); ctx.lineTo(TF.x, TF.y); ctx.lineTo(BF.x, BF.y); ctx.lineTo(BL.x, BL.y);
  ctx.closePath(); ctx.fill();
  ctx.strokeStyle = PALETTE.frame; ctx.lineWidth = 2.2; ctx.stroke();

  ctx.fillStyle = PALETTE.cubeRight;
  ctx.beginPath();
  ctx.moveTo(TF.x, TF.y); ctx.lineTo(TR.x, TR.y); ctx.lineTo(BR.x, BR.y); ctx.lineTo(BF.x, BF.y);
  ctx.closePath(); ctx.fill(); ctx.stroke();

  ctx.fillStyle = PALETTE.cubeTop;
  ctx.beginPath();
  ctx.moveTo(TB.x, TB.y); ctx.lineTo(TL.x, TL.y); ctx.lineTo(TF.x, TF.y); ctx.lineTo(TR.x, TR.y);
  ctx.closePath(); ctx.fill(); ctx.stroke();

  // Vanishing Point Annotation
  ctx.save();
  ctx.fillStyle = PALETTE.orangeText;
  ctx.font = 'bold 20px "Liberation Sans", "Helvetica Neue", Arial, sans-serif';
  ctx.textAlign = 'right';
  ctx.fillText('Right', RVP.x - 6, RVP.y - 30);
  ctx.fillText('Vanishing Point', RVP.x - 6, RVP.y - 8);
  ctx.restore();
}

// =========================================================================
// 3. BOTTOM-LEFT PANEL (PANEL 3) - WIREFRAME
// =========================================================================
function drawPanel3() {
  ctx.strokeStyle = PALETTE.frame;
  ctx.lineWidth = 2.4;
  ctx.strokeRect(P3.x, P3.y, P3.w, P3.h);

  // Horizon line
  ctx.beginPath();
  ctx.moveTo(P3.x, P3.horizonY);
  ctx.lineTo(P3.x + P3.w, P3.horizonY);
  ctx.stroke();

  // Light bulb & Ground Point
  const bulbX = P3.x + 45;
  const bulbY = P3.y + 26;
  const groundY = P3.y + 162;

  drawLightBulb(bulbX, bulbY, 15, P3.y);

  // Vertical drop line & ground dot
  ctx.strokeStyle = PALETTE.orange;
  ctx.lineWidth = 1.8;
  ctx.beginPath();
  ctx.moveTo(bulbX, bulbY); ctx.lineTo(bulbX, groundY);
  ctx.stroke();

  ctx.fillStyle = PALETTE.orange;
  ctx.beginPath();
  ctx.arc(bulbX, groundY, 4, 0, Math.PI * 2);
  ctx.fill();

  // Wireframe Cube Vertices spanning across the horizon
  const TL = { x: 148, y: 925 };
  const TF = { x: 192, y: 956 };
  const TR = { x: 245, y: 934 };
  const TB = { x: 192, y: 912 };

  const BL = { x: 148, y: 958 };
  const BF = { x: 192, y: 989 };
  const BR = { x: 245, y: 967 };
  const BB = { x: 192, y: 945 };

  const L = { x: bulbX, y: bulbY };
  const G = { x: bulbX, y: groundY };

  const S_TL = lineIntersect(L, TL, G, BL);
  const S_TF = lineIntersect(L, TF, G, BF);
  const S_TR = lineIntersect(L, TR, G, BR);

  // Construction rays (orange)
  ctx.strokeStyle = PALETTE.orange;
  ctx.lineWidth = 1.5;
  ctx.beginPath();
  // Ground rays
  const gExtTL = projectRay(G, S_TL, 15);
  const gExtTF = projectRay(G, S_TF, 25);
  const gExtTR = projectRay(G, S_TR, 15);

  ctx.moveTo(G.x, G.y); ctx.lineTo(gExtTL.x, gExtTL.y);
  ctx.moveTo(G.x, G.y); ctx.lineTo(gExtTF.x, gExtTF.y);
  ctx.moveTo(G.x, G.y); ctx.lineTo(gExtTR.x, gExtTR.y);
  // Light rays
  ctx.moveTo(L.x, L.y); ctx.lineTo(S_TL.x, S_TL.y);
  ctx.moveTo(L.x, L.y); ctx.lineTo(S_TF.x, S_TF.y);
  ctx.moveTo(L.x, L.y); ctx.lineTo(S_TR.x, S_TR.y);
  ctx.stroke();

  // Shadow polygon outline on ground (orange)
  ctx.beginPath();
  ctx.moveTo(BL.x, BL.y);
  ctx.lineTo(S_TL.x, S_TL.y);
  ctx.lineTo(S_TF.x, S_TF.y);
  ctx.lineTo(S_TR.x, S_TR.y);
  ctx.lineTo(BR.x, BR.y);
  ctx.stroke();

  // Draw Wireframe Cube (12 crisp black edges)
  ctx.strokeStyle = PALETTE.wireframe;
  ctx.lineWidth = 2.2;
  ctx.beginPath();
  // Bottom face
  ctx.moveTo(BL.x, BL.y); ctx.lineTo(BF.x, BF.y);
  ctx.lineTo(BR.x, BR.y); ctx.lineTo(BB.x, BB.y);
  ctx.closePath();
  // Top face
  ctx.moveTo(TL.x, TL.y); ctx.lineTo(TF.x, TF.y);
  ctx.lineTo(TR.x, TR.y); ctx.lineTo(TB.x, TB.y);
  ctx.closePath();
  // 4 Vertical pillars
  ctx.moveTo(BL.x, BL.y); ctx.lineTo(TL.x, TL.y);
  ctx.moveTo(BF.x, BF.y); ctx.lineTo(TF.x, TF.y);
  ctx.moveTo(BR.x, BR.y); ctx.lineTo(TR.x, TR.y);
  ctx.moveTo(BB.x, BB.y); ctx.lineTo(TB.x, TB.y);
  ctx.stroke();
}

// =========================================================================
// 4. BOTTOM-RIGHT PANEL (PANEL 4) - SHADOW TO LEFT VP
// =========================================================================
function drawPanel4() {
  ctx.strokeStyle = PALETTE.frame;
  ctx.lineWidth = 2.4;
  ctx.strokeRect(P4.x, P4.y, P4.w, P4.h);

  // Horizon line
  ctx.beginPath();
  ctx.moveTo(P4.x, P4.horizonY);
  ctx.lineTo(P4.x + P4.w, P4.horizonY);
  ctx.stroke();

  // Light bulb & Ground Point
  const bulbX = P4.x + 45;
  const bulbY = P4.y + 26;
  const groundY = P4.y + 162;

  drawLightBulb(bulbX, bulbY, 15, P4.y);

  // Vertical drop line & ground dot
  ctx.strokeStyle = PALETTE.orange;
  ctx.lineWidth = 1.8;
  ctx.beginPath();
  ctx.moveTo(bulbX, bulbY); ctx.lineTo(bulbX, groundY);
  ctx.stroke();

  ctx.fillStyle = PALETTE.orange;
  ctx.beginPath();
  ctx.arc(bulbX, groundY, 4, 0, Math.PI * 2);
  ctx.fill();

  // 3D Cube in P4
  const TL = { x: 472, y: 925 };
  const TF = { x: 516, y: 956 };
  const TR = { x: 569, y: 934 };
  const TB = { x: 516, y: 912 };

  const BL = { x: 472, y: 958 };
  const BF = { x: 516, y: 989 };
  const BR = { x: 569, y: 967 };

  const L = { x: bulbX, y: bulbY };
  const G = { x: bulbX, y: groundY };

  const S_TL = lineIntersect(L, TL, G, BL);
  const S_TF = lineIntersect(L, TF, G, BF);
  const S_TR = lineIntersect(L, TR, G, BR);

  // Cast Shadow Polygon (Blue)
  ctx.fillStyle = PALETTE.shadow;
  ctx.beginPath();
  ctx.moveTo(BL.x, BL.y);
  ctx.lineTo(S_TL.x, S_TL.y);
  ctx.lineTo(S_TF.x, S_TF.y);
  ctx.lineTo(S_TR.x, S_TR.y);
  ctx.lineTo(BR.x, BR.y);
  ctx.lineTo(BF.x, BF.y);
  ctx.closePath();
  ctx.fill();

  // Orange Projection Rays
  ctx.strokeStyle = PALETTE.orange;
  ctx.lineWidth = 1.5;
  const gExtTL = projectRay(G, S_TL, 15);
  const gExtTF = projectRay(G, S_TF, 25);
  const gExtTR = projectRay(G, S_TR, 15);

  ctx.beginPath();
  ctx.moveTo(G.x, G.y); ctx.lineTo(gExtTL.x, gExtTL.y);
  ctx.moveTo(G.x, G.y); ctx.lineTo(gExtTF.x, gExtTF.y);
  ctx.moveTo(G.x, G.y); ctx.lineTo(gExtTR.x, gExtTR.y);
  ctx.moveTo(L.x, L.y); ctx.lineTo(S_TL.x, S_TL.y);
  ctx.moveTo(L.x, L.y); ctx.lineTo(S_TF.x, S_TF.y);
  ctx.moveTo(L.x, L.y); ctx.lineTo(S_TR.x, S_TR.y);
  ctx.stroke();

  // Cube Faces
  ctx.fillStyle = PALETTE.cubeLeft;
  ctx.beginPath();
  ctx.moveTo(TL.x, TL.y); ctx.lineTo(TF.x, TF.y); ctx.lineTo(BF.x, BF.y); ctx.lineTo(BL.x, BL.y);
  ctx.closePath(); ctx.fill();
  ctx.strokeStyle = PALETTE.frame; ctx.lineWidth = 2.2; ctx.stroke();

  ctx.fillStyle = PALETTE.cubeRight;
  ctx.beginPath();
  ctx.moveTo(TF.x, TF.y); ctx.lineTo(TR.x, TR.y); ctx.lineTo(BR.x, BR.y); ctx.lineTo(BF.x, BF.y);
  ctx.closePath(); ctx.fill(); ctx.stroke();

  ctx.fillStyle = PALETTE.cubeTop;
  ctx.beginPath();
  ctx.moveTo(TB.x, TB.y); ctx.lineTo(TL.x, TL.y); ctx.lineTo(TF.x, TF.y); ctx.lineTo(TR.x, TR.y);
  ctx.closePath(); ctx.fill(); ctx.stroke();

  // Vanishing line receding to Left VP on horizon
  ctx.strokeStyle = PALETTE.frame;
  ctx.lineWidth = 1.4;
  ctx.beginPath();
  ctx.moveTo(P4.x, P4.horizonY - 8);
  ctx.lineTo(TB.x, TB.y);
  ctx.stroke();

  // Annotation "To Left Vanishing Point"
  ctx.save();
  ctx.fillStyle = PALETTE.orangeText;
  ctx.font = 'bold 17px "Liberation Sans", "Helvetica Neue", Arial, sans-serif';
  ctx.textAlign = 'center';
  ctx.fillText('To Left', P4.x + 230, P4.y + 90);
  ctx.fillText('Vanishing', P4.x + 230, P4.y + 110);
  ctx.fillText('Point', P4.x + 230, P4.y + 130);

  // Curved arrow pointing left
  ctx.strokeStyle = PALETTE.orange;
  ctx.lineWidth = 2.4;
  ctx.beginPath();
  ctx.moveTo(P4.x + 235, P4.y + 144);
  ctx.quadraticCurveTo(P4.x + 195, P4.y + 150, P4.x + 172, P4.y + 138);
  ctx.stroke();

  // Arrowhead
  ctx.fillStyle = PALETTE.orange;
  ctx.beginPath();
  ctx.moveTo(P4.x + 168, P4.y + 136);
  ctx.lineTo(P4.x + 181, P4.y + 131);
  ctx.lineTo(P4.x + 178, P4.y + 145);
  ctx.closePath();
  ctx.fill();

  ctx.restore();
}

// =========================================================================
// 5. RIGHT COLUMN TYPOGRAPHY
// =========================================================================
function drawTypography() {
  const textX = 492;
  const font = '17px "Liberation Serif", "Times New Roman", Georgia, serif';
  const lineHeight = 22.5;

  // Paragraph 1 (aligned with Top Panel)
  const p1Lines = [
    "Just as with the pole, we plot lines from",
    "the light source and the position of the light",
    "source on the ground plane through the top",
    "and bottom of the lines that make up the",
    "cube. Connecting the points created by this",
    "process gives us the shape of the shadow."
  ];

  ctx.save();
  ctx.font = font;
  ctx.fillStyle = PALETTE.text;
  ctx.textAlign = 'left';
  ctx.textBaseline = 'top';

  let y = 99;
  for (const line of p1Lines) {
    ctx.fillText(line, textX, y);
    y += lineHeight;
  }

  // Paragraph 2 (aligned with Middle Panel)
  const p2Lines = [
    "The lines of the shadow we plotted",
    "recede to the same vanishing point as the",
    "lines of the form that cast them, provided the",
    "lines of that form are parallel to the ground",
    "plane. In the top example, the lines of the",
    "cast shadow and the box both recede to the",
    "right vanishing point."
  ];

  y = 448;
  for (const line of p2Lines) {
    ctx.fillText(line, textX, y);
    y += lineHeight;
  }

  // Paragraph 3 (aligned below Paragraph 2)
  const p3Lines = [
    "We can find lines of a shadow when part",
    "of the form that is casting them is obscured",
    "by extending lines to vanishing points of that",
    "form. In the bottom example, the lines of",
    "the cast shadow and the box recede to the",
    "left vanishing point, allowing us to plot the",
    "shadow even when it is partly obscured by",
    "the box."
  ];

  y = 620;
  for (const line of p3Lines) {
    ctx.fillText(line, textX, y);
    y += lineHeight;
  }

  ctx.restore();
}

// =========================================================================
// 6. FOOTER
// =========================================================================
function drawFooter() {
  const footerY = 1145;

  // Footer Title: "Properties Of Light"
  ctx.save();
  ctx.font = '24px "Liberation Serif", "Times New Roman", Georgia, serif';
  ctx.fillStyle = PALETTE.footerText;
  ctx.textAlign = 'right';
  ctx.textBaseline = 'middle';
  ctx.fillText('Properties Of Light', 718, footerY);

  // Sun Icon
  drawSunIcon(752, footerY, 19);

  // Page Badge: "287"
  const badgeW = 116;
  const badgeH = 46;
  const badgeX = W - badgeW;
  const badgeY = footerY - badgeH / 2;

  ctx.fillStyle = PALETTE.badgeBg;
  ctx.fillRect(badgeX, badgeY, badgeW, badgeH);

  ctx.fillStyle = PALETTE.badgeText;
  ctx.font = 'bold 24px "Liberation Sans", "Helvetica Neue", Arial, sans-serif';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText('287', badgeX + badgeW / 2, footerY);

  ctx.restore();
}

// =========================================================================
// RENDER FULL COMPOSITION
// =========================================================================
drawPanel1();
drawPanel2();
drawPanel3();
drawPanel4();
drawTypography();
drawFooter();

// Return canvas
canvas;
