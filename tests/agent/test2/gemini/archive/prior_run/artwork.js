/**
 * Polson Autonomous Artwork Generator - comic1.png Recreation
 * Engine: Polson Graphics MCP Server (Sandboxed ECMAScript 2025)
 * Canvas 2D + Comic Inking & Cel-Shading Pipeline
 */

const W = 447;
const H = 380;

const cv = createCanvas(W, H);
const ctx = cv.getContext('2d');

ctx.lineCap = 'round';
ctx.lineJoin = 'round';

// =========================================================================
// 1. LAYER 0: BACKGROUND ATMOSPHERE & BILLOWING COMIC CLOUDS
// =========================================================================
// Atmospheric Sky Gradient
const skyGrad = ctx.createLinearGradient(0, 0, W * 0.7, H);
skyGrad.addColorStop(0.0, '#5c84a4');
skyGrad.addColorStop(0.35, '#719ab9');
skyGrad.addColorStop(0.75, '#90b4cf');
skyGrad.addColorStop(1.0, '#b1d1e6');
ctx.fillStyle = skyGrad;
ctx.fillRect(0, 0, W, H);

// Billowing Cloud Generator with warm cel-shading and crisp comic inking
function drawComicCloud(cx, cy, scale, flipped = false) {
  ctx.save();
  ctx.translate(cx, cy);
  if (flipped) ctx.scale(-scale, scale);
  else ctx.scale(scale, scale);

  // Cloud Shadow Mass (warm tan/grey)
  ctx.fillStyle = '#c7b497';
  ctx.beginPath();
  ctx.arc(-35, 15, 35, 0, Math.PI * 2);
  ctx.arc(10, 25, 42, 0, Math.PI * 2);
  ctx.arc(60, 20, 36, 0, Math.PI * 2);
  ctx.arc(105, 8, 28, 0, Math.PI * 2);
  ctx.arc(45, -15, 46, 0, Math.PI * 2);
  ctx.arc(-10, -22, 38, 0, Math.PI * 2);
  ctx.fill();

  // Cloud Highlight Mass (warm ivory-cream)
  ctx.fillStyle = '#f5efe3';
  ctx.beginPath();
  ctx.arc(-38, 8, 33, 0, Math.PI * 2);
  ctx.arc(6, 16, 39, 0, Math.PI * 2);
  ctx.arc(55, 12, 33, 0, Math.PI * 2);
  ctx.arc(100, 2, 25, 0, Math.PI * 2);
  ctx.arc(42, -22, 44, 0, Math.PI * 2);
  ctx.arc(-14, -28, 36, 0, Math.PI * 2);
  ctx.fill();

  // Comic Contour Lines
  ctx.strokeStyle = '#2c1c12';
  ctx.lineWidth = 1.5;

  ctx.beginPath();
  ctx.arc(-14, -28, 36, 3.1, 5.8);
  ctx.stroke();

  ctx.beginPath();
  ctx.arc(42, -22, 44, 3.8, 0.4);
  ctx.stroke();

  ctx.beginPath();
  ctx.arc(100, 2, 25, 4.8, 1.4);
  ctx.stroke();

  ctx.beginPath();
  ctx.arc(-38, 8, 33, 2.2, 4.2);
  ctx.stroke();

  ctx.restore();
}

// Background Cloud Formations
drawComicCloud(50, 95, 1.3);
drawComicCloud(140, 40, 1.0);
drawComicCloud(390, 125, 1.4, true);
drawComicCloud(430, 220, 1.1, true);

// =========================================================================
// 2. LAYER 1: SHIP RIGGING & WOODEN SPAR/MAST
// =========================================================================
// Diagonal Heavy Wooden Mast / Yard
ctx.save();
ctx.fillStyle = '#7a563b';
ctx.strokeStyle = '#1a0c05';
ctx.lineWidth = 2.4;

ctx.beginPath();
ctx.moveTo(195, -20);
ctx.lineTo(245, -20);
ctx.lineTo(465, 275);
ctx.lineTo(432, 308);
ctx.closePath();
ctx.fill();
ctx.stroke();

// Deep wood shadow along underside
ctx.fillStyle = '#3e2110';
ctx.beginPath();
ctx.moveTo(218, -20);
ctx.lineTo(245, -20);
ctx.lineTo(465, 275);
ctx.lineTo(448, 292);
ctx.closePath();
ctx.fill();

// Sunlit wood highlight along top edge
ctx.strokeStyle = '#a87f5d';
ctx.lineWidth = 2.2;
ctx.beginPath();
ctx.moveTo(198, -20);
ctx.lineTo(436, 280);
ctx.stroke();

ctx.restore();

// Helical-twisted rope shroud helper
function drawRope(x1, y1, x2, y2, thickness = 5.0, coils = 25) {
  ctx.save();
  const dx = x2 - x1;
  const dy = y2 - y1;
  const len = Math.hypot(dx, dy);
  const angle = Math.atan2(dy, dx);

  ctx.translate(x1, y1);
  ctx.rotate(angle);

  // Tan rope body
  ctx.fillStyle = '#cbb294';
  ctx.fillRect(0, -thickness / 2, len, thickness);

  // Helical coil shadows
  const seg = len / coils;
  ctx.strokeStyle = '#6f4122';
  ctx.lineWidth = thickness * 0.28;
  for (let i = 0; i < coils; i++) {
    const px = i * seg;
    ctx.beginPath();
    ctx.moveTo(px, -thickness / 2);
    ctx.lineTo(px + seg * 0.75, thickness / 2);
    ctx.stroke();
  }

  // Top sunlit edge highlight
  ctx.strokeStyle = '#e2d6c3';
  ctx.lineWidth = 1.0;
  ctx.beginPath();
  ctx.moveTo(0, -thickness / 2 + 0.5);
  ctx.lineTo(len, -thickness / 2 + 0.5);
  ctx.stroke();

  // Dark comic outline
  ctx.strokeStyle = '#180a04';
  ctx.lineWidth = 1.4;
  ctx.beginPath();
  ctx.moveTo(0, -thickness / 2);
  ctx.lineTo(len, -thickness / 2);
  ctx.moveTo(0, thickness / 2);
  ctx.lineTo(len, thickness / 2);
  ctx.stroke();

  ctx.restore();
}

// Shroud Ropes spanning the background
drawRope(240, -15, 80, 360, 5.5, 34);
drawRope(290, -15, 150, 380, 5.2, 32);
drawRope(345, -15, 240, 380, 5.0, 30);
drawRope(400, 15, 335, 380, 6.0, 28);
drawRope(438, 75, 375, 380, 6.2, 26);

// Ratlines & Knots
function drawRatline(x1, y1, x2, y2, coils = 12) {
  drawRope(x1, y1, x2, y2, 4.0, coils);

  ctx.fillStyle = '#7e4d27';
  ctx.strokeStyle = '#180a04';
  ctx.lineWidth = 1.4;
  [[x1, y1], [x2, y2]].forEach(([kx, ky]) => {
    ctx.beginPath();
    ctx.arc(kx, ky, 4.2, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();
  });
}

drawRatline(190, 60, 380, 42, 14);
drawRatline(150, 130, 410, 102, 16);
drawRatline(115, 200, 435, 165, 18);
drawRatline(95, 270, 450, 235, 20);

// Extra cross rigging knots
function drawKnot(kx, ky) {
  ctx.fillStyle = '#7a4a24';
  ctx.strokeStyle = '#180a04';
  ctx.lineWidth = 1.4;
  ctx.beginPath();
  ctx.arc(kx, ky, 4.5, 0, Math.PI * 2);
  ctx.fill();
  ctx.stroke();
}

drawKnot(272, 50);
drawKnot(335, 45);
drawKnot(245, 118);
drawKnot(325, 110);
drawKnot(215, 185);
drawKnot(310, 175);
drawKnot(390, 170);

// =========================================================================
// 3. LAYER 2: TRAILING BANDANA TAILS & REAR HAIR
// =========================================================================
// Deep Rear Hair Mass under the ponytail
ctx.save();
ctx.fillStyle = '#65230e';
ctx.beginPath();
ctx.moveTo(150, 160);
ctx.bezierCurveTo(115, 180, 105, 230, 135, 265);
ctx.bezierCurveTo(155, 280, 175, 265, 180, 245);
ctx.bezierCurveTo(185, 205, 175, 175, 150, 160);
ctx.closePath();
ctx.fill();
ctx.restore();

// Trailing Bandana Tails (streaming to the left in high wind)
// Top Tail (Flapping ribbon)
ctx.save();
ctx.fillStyle = '#3a4a55';
ctx.strokeStyle = '#121920';
ctx.lineWidth = 2.0;

ctx.beginPath();
ctx.moveTo(165, 195);
ctx.bezierCurveTo(120, 185, 60, 200, 15, 225);
ctx.bezierCurveTo(0, 232, -10, 235, -5, 242);
ctx.bezierCurveTo(15, 240, 50, 225, 95, 220);
ctx.bezierCurveTo(130, 218, 160, 225, 175, 225);
ctx.closePath();
ctx.fill();
ctx.stroke();

// Top Tail highlight & fold crease
ctx.fillStyle = '#546675';
ctx.beginPath();
ctx.moveTo(165, 195);
ctx.bezierCurveTo(120, 185, 60, 200, 15, 225);
ctx.bezierCurveTo(45, 215, 100, 205, 165, 208);
ctx.closePath();
ctx.fill();

// Bottom Tail
ctx.fillStyle = '#29353e';
ctx.beginPath();
ctx.moveTo(170, 220);
ctx.bezierCurveTo(130, 225, 80, 245, 30, 280);
ctx.bezierCurveTo(20, 288, 10, 292, 18, 295);
ctx.bezierCurveTo(45, 285, 95, 265, 140, 255);
ctx.bezierCurveTo(165, 250, 180, 245, 185, 240);
ctx.closePath();
ctx.fill();
ctx.stroke();

ctx.restore();

// =========================================================================
// 4. LAYER 3: DYNAMIC PONYTAIL MASS & VOLUMINOUS CURLS
// =========================================================================
ctx.save();

// Deep Shadow under ponytail
ctx.fillStyle = '#561908';
ctx.beginPath();
ctx.moveTo(145, 165);
ctx.bezierCurveTo(110, 140, 60, 150, 20, 185);
ctx.bezierCurveTo(5, 200, 0, 215, 15, 220);
ctx.bezierCurveTo(45, 210, 80, 225, 115, 240);
ctx.bezierCurveTo(145, 250, 170, 235, 175, 210);
ctx.closePath();
ctx.fill();

// Main Ponytail Volume - Base Auburn & Multi-Tier Curls
ctx.fillStyle = '#d66730';
ctx.strokeStyle = '#1a0904';
ctx.lineWidth = 2.4;

// Lock 1: Big top whipping lock
ctx.beginPath();
ctx.moveTo(140, 145);
ctx.bezierCurveTo(105, 95, 65, 90, 25, 125);
ctx.bezierCurveTo(15, 135, 12, 145, 22, 145);
ctx.bezierCurveTo(45, 130, 80, 125, 115, 145);
ctx.bezierCurveTo(135, 155, 145, 165, 150, 170);
ctx.closePath();
ctx.fill();
ctx.stroke();

// Lock 1 Shadow
ctx.fillStyle = '#893215';
ctx.beginPath();
ctx.moveTo(140, 145);
ctx.bezierCurveTo(105, 95, 65, 90, 25, 125);
ctx.bezierCurveTo(45, 115, 85, 110, 120, 135);
ctx.closePath();
ctx.fill();

// Lock 1 Highlight sheen
ctx.fillStyle = '#f39460';
ctx.beginPath();
ctx.moveTo(115, 125);
ctx.bezierCurveTo(80, 112, 50, 120, 30, 132);
ctx.bezierCurveTo(55, 122, 90, 120, 120, 132);
ctx.closePath();
ctx.fill();

// Lock 2: Upper curly cluster
ctx.fillStyle = '#d66730';
ctx.beginPath();
ctx.moveTo(135, 135);
ctx.bezierCurveTo(115, 75, 75, 65, 45, 85);
ctx.bezierCurveTo(35, 92, 35, 102, 45, 102);
ctx.bezierCurveTo(65, 85, 95, 85, 120, 115);
ctx.closePath();
ctx.fill();
ctx.stroke();

// Lock 3: Outer sweeping curls
ctx.fillStyle = '#d66730';
ctx.beginPath();
ctx.moveTo(125, 155);
ctx.bezierCurveTo(80, 135, 35, 145, 5, 180);
ctx.bezierCurveTo(-5, 192, -2, 202, 10, 198);
ctx.bezierCurveTo(35, 175, 70, 170, 105, 185);
ctx.bezierCurveTo(125, 195, 140, 205, 145, 210);
ctx.closePath();
ctx.fill();
ctx.stroke();

// Lock 3 Shadow
ctx.fillStyle = '#893215';
ctx.beginPath();
ctx.moveTo(105, 170);
ctx.bezierCurveTo(65, 155, 25, 170, 5, 188);
ctx.bezierCurveTo(30, 175, 65, 172, 100, 186);
ctx.closePath();
ctx.fill();

// Lock 4: Bottom curly hook
ctx.fillStyle = '#d66730';
ctx.beginPath();
ctx.moveTo(115, 195);
ctx.bezierCurveTo(75, 190, 35, 215, 30, 245);
ctx.bezierCurveTo(28, 255, 38, 258, 45, 250);
ctx.bezierCurveTo(60, 230, 90, 225, 125, 235);
ctx.closePath();
ctx.fill();
ctx.stroke();

// Ponytail Band / Wrap
ctx.fillStyle = '#26343e';
ctx.strokeStyle = '#12181e';
ctx.lineWidth = 2.0;
ctx.beginPath();
ctx.ellipse(145, 170, 10, 16, -0.4, 0, Math.PI * 2);
ctx.fill();
ctx.stroke();

ctx.restore();

// =========================================================================
// 5. LAYER 4: SOLID CROWN HAIR MASS WITH NATURAL VOLUMETRIC LOCKS
// =========================================================================
ctx.save();
ctx.fillStyle = '#d66730';
ctx.strokeStyle = '#1a0904';
ctx.lineWidth = 2.4;

// 1. Solid Base Mass with 3 Proportional Organic Crest Waves
ctx.beginPath();
ctx.moveTo(150, 165);
ctx.bezierCurveTo(140, 115, 170, 65, 210, 45);
ctx.bezierCurveTo(235, 35, 260, 40, 280, 58);
ctx.bezierCurveTo(305, 45, 335, 65, 350, 95);
ctx.bezierCurveTo(365, 125, 365, 150, 355, 165);
ctx.bezierCurveTo(330, 150, 305, 145, 275, 152);
ctx.bezierCurveTo(240, 160, 210, 180, 185, 202);
ctx.bezierCurveTo(168, 190, 155, 178, 150, 165);
ctx.closePath();
ctx.fill();
ctx.stroke();

// 2. Sculpted Lock Divisions & Cel-Shadow Planes
ctx.fillStyle = '#893215';

// Top crest shadow groove
ctx.beginPath();
ctx.moveTo(205, 50);
ctx.bezierCurveTo(238, 45, 268, 55, 290, 78);
ctx.bezierCurveTo(265, 68, 235, 62, 205, 68);
ctx.closePath();
ctx.fill();

// Mid crown wave shadow groove
ctx.beginPath();
ctx.moveTo(175, 92);
ctx.bezierCurveTo(215, 75, 265, 78, 310, 105);
ctx.bezierCurveTo(275, 95, 230, 88, 190, 105);
ctx.closePath();
ctx.fill();

// Lower crown wave shadow groove
ctx.beginPath();
ctx.moveTo(195, 122);
ctx.bezierCurveTo(240, 105, 290, 108, 335, 140);
ctx.bezierCurveTo(300, 125, 252, 120, 210, 138);
ctx.closePath();
ctx.fill();

// 3. Highlight Sheens along curl crests
ctx.fillStyle = '#f39460';

ctx.beginPath();
ctx.moveTo(215, 44);
ctx.bezierCurveTo(242, 38, 272, 46, 295, 66);
ctx.bezierCurveTo(270, 54, 242, 46, 215, 52);
ctx.closePath();
ctx.fill();

ctx.beginPath();
ctx.moveTo(235, 72);
ctx.bezierCurveTo(268, 65, 298, 75, 322, 98);
ctx.bezierCurveTo(298, 85, 270, 76, 235, 82);
ctx.closePath();
ctx.fill();

// 4. Sweeping comic inking lines through hair mass
ctx.strokeStyle = '#1a0904';
ctx.lineWidth = 1.8;
ctx.beginPath();
ctx.moveTo(205, 58);
ctx.bezierCurveTo(238, 52, 268, 60, 295, 80);
ctx.moveTo(178, 98);
ctx.bezierCurveTo(218, 80, 262, 82, 312, 112);
ctx.moveTo(200, 128);
ctx.bezierCurveTo(242, 110, 285, 114, 332, 148);
ctx.stroke();

ctx.restore();

// =========================================================================
// 6. LAYER 5: BANDANA / HEADBAND
// =========================================================================
ctx.save();
ctx.fillStyle = '#42525e';
ctx.strokeStyle = '#141d24';
ctx.lineWidth = 2.2;

// Main Bandana Ribbon wrapping head diagonally
ctx.beginPath();
ctx.moveTo(165, 195);
ctx.bezierCurveTo(180, 165, 210, 142, 255, 128);
ctx.bezierCurveTo(280, 120, 310, 128, 330, 148);
ctx.bezierCurveTo(305, 155, 275, 152, 240, 168);
ctx.bezierCurveTo(205, 185, 180, 212, 172, 228);
ctx.closePath();
ctx.fill();
ctx.stroke();

// Bandana Upper Highlight Stripe
ctx.fillStyle = '#617585';
ctx.beginPath();
ctx.moveTo(172, 188);
ctx.bezierCurveTo(190, 160, 220, 138, 258, 128);
ctx.bezierCurveTo(280, 124, 305, 132, 325, 145);
ctx.bezierCurveTo(300, 136, 275, 132, 245, 142);
ctx.bezierCurveTo(210, 156, 185, 180, 175, 198);
ctx.closePath();
ctx.fill();

// Bandana Tension Creases (inking)
ctx.strokeStyle = '#141d24';
ctx.lineWidth = 1.5;
ctx.beginPath();
ctx.moveTo(210, 162);
ctx.bezierCurveTo(225, 155, 240, 152, 255, 148);
ctx.moveTo(235, 170);
ctx.bezierCurveTo(255, 160, 275, 155, 295, 152);
ctx.stroke();

ctx.restore();

// =========================================================================
// 7. LAYER 6: FACE ANATOMY, JAW, BROAD NECK & EAR
// =========================================================================
ctx.save();
ctx.fillStyle = '#f8c89d';
ctx.strokeStyle = '#180904';
ctx.lineWidth = 2.2;

// Anatomical Face & Continuous Broad Neck Silhouette
ctx.beginPath();
ctx.moveTo(235, 172);
ctx.bezierCurveTo(265, 158, 295, 162, 318, 180);
ctx.bezierCurveTo(330, 190, 338, 198, 342, 208); // Brow / eye socket
ctx.bezierCurveTo(338, 218, 336, 228, 342, 236); // Nose bridge
ctx.bezierCurveTo(350, 242, 348, 248, 340, 252); // Nose tip
ctx.bezierCurveTo(332, 256, 330, 262, 332, 268); // Upper lip
ctx.bezierCurveTo(334, 276, 330, 285, 332, 292); // Lower lip
ctx.bezierCurveTo(334, 305, 326, 318, 314, 322); // Chin
ctx.bezierCurveTo(285, 324, 255, 302, 235, 285); // Jawline
ctx.bezierCurveTo(245, 305, 260, 340, 280, 380); // Front of neck down to base
ctx.lineTo(165, 380);                             // Full base of neck
ctx.bezierCurveTo(175, 330, 180, 295, 180, 260); // Back of neck
ctx.bezierCurveTo(172, 250, 170, 230, 178, 220); // Ear bottom
ctx.bezierCurveTo(185, 210, 198, 210, 202, 220); // Ear top
ctx.bezierCurveTo(205, 198, 220, 182, 235, 172);
ctx.closePath();
ctx.fill();

// Skin Highlight Planes (Forehead, Cheekbone, Nose Bridge, Chin)
ctx.fillStyle = '#fde2cc';
// Cheek & Nose Highlight
ctx.beginPath();
ctx.moveTo(292, 218);
ctx.bezierCurveTo(315, 212, 332, 222, 336, 236);
ctx.bezierCurveTo(326, 248, 306, 254, 286, 240);
ctx.bezierCurveTo(282, 230, 285, 222, 292, 218);
ctx.closePath();
ctx.fill();

// Forehead Highlight
ctx.beginPath();
ctx.moveTo(255, 172);
ctx.bezierCurveTo(280, 168, 305, 178, 318, 190);
ctx.bezierCurveTo(298, 196, 270, 190, 255, 172);
ctx.closePath();
ctx.fill();

// Chin Highlight
ctx.beginPath();
ctx.ellipse(316, 312, 9, 6, 0.2, 0, Math.PI * 2);
ctx.fill();

// Skin Cel-Shadow Planes (Warm Tan & Deep Ochre)
ctx.fillStyle = '#df9160';

// Jaw & Continuous Broad Neck Cast Shadow
ctx.beginPath();
ctx.moveTo(195, 230);
ctx.bezierCurveTo(210, 250, 235, 270, 270, 288);
ctx.bezierCurveTo(300, 302, 318, 314, 314, 322);
ctx.bezierCurveTo(285, 324, 255, 302, 235, 285);
ctx.bezierCurveTo(245, 305, 260, 340, 280, 380);
ctx.lineTo(165, 380);
ctx.bezierCurveTo(175, 330, 180, 295, 180, 260);
ctx.closePath();
ctx.fill();

// Deep throat shadow
ctx.fillStyle = '#a65326';
ctx.beginPath();
ctx.moveTo(175, 305);
ctx.bezierCurveTo(205, 315, 235, 335, 255, 380);
ctx.lineTo(165, 380);
ctx.closePath();
ctx.fill();

// Under-nose and eye-socket cel shadows
ctx.fillStyle = '#a65326';
// Nose shadow
ctx.beginPath();
ctx.moveTo(322, 240);
ctx.lineTo(338, 248);
ctx.lineTo(320, 252);
ctx.closePath();
ctx.fill();

// Under lower lip shadow
ctx.beginPath();
ctx.moveTo(308, 292);
ctx.bezierCurveTo(320, 292, 328, 289, 332, 287);
ctx.bezierCurveTo(328, 298, 314, 301, 304, 296);
ctx.closePath();
ctx.fill();

// Ear Anatomy & Inking
ctx.fillStyle = '#f8c89d';
ctx.beginPath();
ctx.moveTo(180, 218);
ctx.bezierCurveTo(170, 225, 168, 248, 180, 262);
ctx.bezierCurveTo(190, 272, 200, 262, 202, 246);
ctx.bezierCurveTo(205, 232, 195, 215, 180, 218);
ctx.closePath();
ctx.fill();
ctx.stroke();

// Inner ear concha shadow
ctx.fillStyle = '#7a361a';
ctx.beginPath();
ctx.moveTo(182, 228);
ctx.bezierCurveTo(175, 235, 178, 246, 185, 252);
ctx.bezierCurveTo(190, 246, 188, 235, 182, 228);
ctx.closePath();
ctx.fill();

// Golden Hoop Earring
// Top clip/stud
ctx.fillStyle = '#e8bd4b';
ctx.strokeStyle = '#261a04';
ctx.lineWidth = 1.4;
ctx.beginPath();
ctx.arc(192, 248, 3.5, 0, Math.PI * 2);
ctx.fill();
ctx.stroke();

// Hanging golden hoop
ctx.lineWidth = 3.5;
ctx.strokeStyle = '#e8bd4b';
ctx.beginPath();
ctx.arc(180, 264, 12, 0.4, Math.PI * 2 + 0.2);
ctx.stroke();

// Hoop dark inner outline
ctx.lineWidth = 1.2;
ctx.strokeStyle = '#261a04';
ctx.beginPath();
ctx.arc(180, 264, 13.8, 0, Math.PI * 2);
ctx.stroke();
ctx.beginPath();
ctx.arc(180, 264, 10.2, 0, Math.PI * 2);
ctx.stroke();

// Hoop Specular Highlight
ctx.strokeStyle = '#fff5b0';
ctx.lineWidth = 1.8;
ctx.beginPath();
ctx.arc(180, 264, 12, 2.8, 3.8);
ctx.stroke();

ctx.restore();

// =========================================================================
// 8. LAYER 7: FACIAL FEATURES & EXPRESSION
// =========================================================================
ctx.save();

// --- Eyebrows (Determined arched comic brows) ---
ctx.fillStyle = '#261006';
ctx.strokeStyle = '#261006';
ctx.lineWidth = 1.5;

// Near Eyebrow (Right brow on face)
ctx.beginPath();
ctx.moveTo(246, 194);
ctx.bezierCurveTo(265, 180, 285, 180, 304, 194);
ctx.bezierCurveTo(285, 186, 265, 186, 246, 194);
ctx.closePath();
ctx.fill();
ctx.stroke();

// Far Eyebrow
ctx.beginPath();
ctx.moveTo(328, 190);
ctx.bezierCurveTo(342, 184, 355, 186, 362, 196);
ctx.bezierCurveTo(352, 190, 338, 190, 328, 190);
ctx.closePath();
ctx.fill();
ctx.stroke();

// --- Eyes ---
// Near Eye Sclera (White)
ctx.fillStyle = '#eae4d8';
ctx.beginPath();
ctx.moveTo(262, 208);
ctx.bezierCurveTo(272, 200, 286, 200, 296, 210);
ctx.bezierCurveTo(286, 218, 272, 218, 262, 208);
ctx.closePath();
ctx.fill();

// Slate Blue Iris & Pupil
ctx.fillStyle = '#3a5468';
ctx.beginPath();
ctx.arc(282, 209, 5.5, 0, Math.PI * 2);
ctx.fill();

// Black Pupil
ctx.fillStyle = '#0c1015';
ctx.beginPath();
ctx.arc(283, 209, 3.0, 0, Math.PI * 2);
ctx.fill();

// Specular Catchlight
ctx.fillStyle = '#ffffff';
ctx.beginPath();
ctx.arc(281, 207, 1.5, 0, Math.PI * 2);
ctx.fill();

// Upper Eyelash / Liner (Bold comic inking)
ctx.strokeStyle = '#0c1015';
ctx.lineWidth = 3.2;
ctx.beginPath();
ctx.moveTo(259, 209);
ctx.bezierCurveTo(270, 199, 286, 199, 299, 210);
ctx.stroke();

// Lower Eyelid
ctx.lineWidth = 1.2;
ctx.beginPath();
ctx.moveTo(266, 214);
ctx.bezierCurveTo(276, 219, 288, 218, 295, 212);
ctx.stroke();

// Upper Eyelid Crease
ctx.lineWidth = 1.2;
ctx.beginPath();
ctx.moveTo(264, 196);
ctx.bezierCurveTo(274, 192, 289, 194, 296, 200);
ctx.stroke();

// Far Eye (Lashes & Glimpse of iris)
ctx.fillStyle = '#eae4d8';
ctx.beginPath();
ctx.moveTo(338, 208);
ctx.bezierCurveTo(344, 202, 352, 204, 358, 211);
ctx.bezierCurveTo(352, 216, 344, 214, 338, 208);
ctx.closePath();
ctx.fill();

ctx.fillStyle = '#3a5468';
ctx.beginPath();
ctx.arc(346, 209, 4.0, 0, Math.PI * 2);
ctx.fill();

ctx.fillStyle = '#0c1015';
ctx.beginPath();
ctx.arc(347, 209, 2.2, 0, Math.PI * 2);
ctx.fill();

ctx.fillStyle = '#ffffff';
ctx.beginPath();
ctx.arc(345, 207, 1.2, 0, Math.PI * 2);
ctx.fill();

ctx.strokeStyle = '#0c1015';
ctx.lineWidth = 2.8;
ctx.beginPath();
ctx.moveTo(336, 209);
ctx.bezierCurveTo(344, 201, 354, 203, 359, 212);
ctx.stroke();

// --- Nose Contours & Inking ---
ctx.strokeStyle = '#180904';
ctx.lineWidth = 1.6;
ctx.beginPath();
ctx.moveTo(310, 204);
ctx.bezierCurveTo(322, 214, 332, 230, 336, 240);
ctx.bezierCurveTo(334, 246, 327, 250, 318, 248);
ctx.stroke();

// Nostril groove
ctx.fillStyle = '#180904';
ctx.beginPath();
ctx.ellipse(323, 246, 1.8, 1.1, -0.3, 0, Math.PI * 2);
ctx.fill();

// --- Lips & Open Mouth Expression ---
// Upper Lip
ctx.fillStyle = '#6e201c';
ctx.beginPath();
ctx.moveTo(292, 274);
ctx.bezierCurveTo(304, 266, 318, 264, 330, 269);
ctx.bezierCurveTo(318, 271, 304, 272, 292, 274);
ctx.closePath();
ctx.fill();

// Mouth Interior Cavity
ctx.fillStyle = '#220706';
ctx.beginPath();
ctx.moveTo(294, 274);
ctx.bezierCurveTo(306, 270, 320, 269, 328, 271);
ctx.bezierCurveTo(322, 282, 306, 282, 294, 274);
ctx.closePath();
ctx.fill();

// Upper Row of Teeth
ctx.fillStyle = '#eae5da';
ctx.beginPath();
ctx.moveTo(298, 273);
ctx.lineTo(324, 271);
ctx.lineTo(322, 275);
ctx.lineTo(300, 276);
ctx.closePath();
ctx.fill();

// Lower Lip
ctx.fillStyle = '#c04d46';
ctx.beginPath();
ctx.moveTo(294, 275);
ctx.bezierCurveTo(306, 282, 320, 282, 328, 272);
ctx.bezierCurveTo(324, 288, 309, 289, 294, 275);
ctx.closePath();
ctx.fill();

// Lower Lip Highlight
ctx.fillStyle = '#e87871';
ctx.beginPath();
ctx.ellipse(312, 282, 5.5, 1.8, 0, 0, Math.PI * 2);
ctx.fill();

// Mouth Lip Contours (Inking)
ctx.strokeStyle = '#180904';
ctx.lineWidth = 1.6;
ctx.beginPath();
ctx.moveTo(290, 274);
ctx.bezierCurveTo(304, 266, 318, 264, 330, 269);
ctx.moveTo(292, 275);
ctx.bezierCurveTo(306, 289, 322, 288, 330, 271);
ctx.stroke();

// Signature Beauty Mark / Freckle on lower jaw
ctx.fillStyle = '#2a1106';
ctx.beginPath();
ctx.arc(270, 302, 1.8, 0, Math.PI * 2);
ctx.fill();

ctx.restore();

// =========================================================================
// 9. LAYER 8: FOREHEAD CURLS, TEMPLE WISPS & FRONT HAIR
// =========================================================================
ctx.save();
ctx.fillStyle = '#d66730';
ctx.strokeStyle = '#1a0904';
ctx.lineWidth = 2.2;

// Signature S-Curve Forehead Wave & Hook
ctx.beginPath();
ctx.moveTo(240, 135);
ctx.bezierCurveTo(265, 110, 295, 115, 305, 138);
ctx.bezierCurveTo(312, 155, 302, 168, 288, 170);
ctx.bezierCurveTo(278, 172, 274, 165, 278, 158);
ctx.bezierCurveTo(286, 146, 296, 138, 288, 126);
ctx.bezierCurveTo(278, 118, 255, 122, 235, 145);
ctx.closePath();
ctx.fill();
ctx.stroke();

// Forehead Wave Shadow & Highlight
ctx.fillStyle = '#893215';
ctx.beginPath();
ctx.moveTo(260, 118);
ctx.bezierCurveTo(285, 115, 300, 125, 305, 138);
ctx.bezierCurveTo(295, 132, 280, 128, 260, 132);
ctx.closePath();
ctx.fill();

ctx.fillStyle = '#f39460';
ctx.beginPath();
ctx.moveTo(270, 112);
ctx.bezierCurveTo(288, 112, 302, 122, 305, 132);
ctx.bezierCurveTo(295, 122, 282, 118, 270, 118);
ctx.closePath();
ctx.fill();

// Secondary Forehead Hook curl
ctx.fillStyle = '#d66730';
ctx.beginPath();
ctx.moveTo(290, 148);
ctx.bezierCurveTo(310, 142, 330, 156, 326, 172);
ctx.bezierCurveTo(322, 180, 312, 178, 314, 168);
ctx.bezierCurveTo(316, 158, 304, 152, 290, 155);
ctx.closePath();
ctx.fill();
ctx.stroke();

// Temple & Side Locks in front of ear
ctx.beginPath();
ctx.moveTo(215, 185);
ctx.bezierCurveTo(205, 210, 195, 235, 175, 245);
ctx.bezierCurveTo(168, 248, 165, 240, 172, 232);
ctx.bezierCurveTo(185, 220, 195, 200, 205, 185);
ctx.closePath();
ctx.fill();
ctx.stroke();

ctx.restore();

// =========================================================================
// 10. LAYER 9: LINEN SHIRT COLLAR & NAVY WOOL COAT
// =========================================================================
ctx.save();

// --- Popped Linen Shirt Collar ---
// Left Collar Wing (Dramatic pointed flap meeting jaw)
ctx.fillStyle = '#e8e3cb';
ctx.strokeStyle = '#180904';
ctx.lineWidth = 2.2;

ctx.beginPath();
ctx.moveTo(200, 315);
ctx.bezierCurveTo(160, 305, 110, 295, 75, 308);
ctx.bezierCurveTo(100, 335, 140, 360, 175, 380);
ctx.lineTo(215, 380);
ctx.closePath();
ctx.fill();
ctx.stroke();

// Left Collar Shadow & Underside Fold
ctx.fillStyle = '#9d9675';
ctx.beginPath();
ctx.moveTo(135, 312);
ctx.bezierCurveTo(105, 308, 85, 308, 75, 308);
ctx.bezierCurveTo(100, 335, 135, 355, 160, 370);
ctx.bezierCurveTo(148, 348, 140, 330, 135, 312);
ctx.closePath();
ctx.fill();

// Right Collar Wing
ctx.fillStyle = '#e8e3cb';
ctx.beginPath();
ctx.moveTo(240, 328);
ctx.bezierCurveTo(275, 324, 318, 328, 348, 338);
ctx.bezierCurveTo(325, 355, 290, 370, 255, 380);
ctx.closePath();
ctx.fill();
ctx.stroke();

// Right Collar Shadow
ctx.fillStyle = '#9d9675';
ctx.beginPath();
ctx.moveTo(280, 330);
ctx.bezierCurveTo(312, 332, 336, 335, 348, 338);
ctx.bezierCurveTo(325, 355, 298, 365, 280, 368);
ctx.closePath();
ctx.fill();

// --- Navy Wool Coat & Shoulders ---
// Left Shoulder
ctx.fillStyle = '#263440';
ctx.strokeStyle = '#0e141a';
ctx.lineWidth = 2.4;

ctx.beginPath();
ctx.moveTo(0, 310);
ctx.bezierCurveTo(35, 320, 70, 335, 105, 365);
ctx.lineTo(105, 380);
ctx.lineTo(0, 380);
ctx.closePath();
ctx.fill();
ctx.stroke();

// Left Shoulder Crease & Shadow
ctx.fillStyle = '#141d24';
ctx.beginPath();
ctx.moveTo(0, 345);
ctx.bezierCurveTo(30, 350, 60, 360, 85, 380);
ctx.lineTo(0, 380);
ctx.closePath();
ctx.fill();

// Right Shoulder / Coat Lapel
ctx.fillStyle = '#263440';
ctx.beginPath();
ctx.moveTo(348, 338);
ctx.bezierCurveTo(378, 334, 415, 330, 447, 335);
ctx.lineTo(447, 380);
ctx.lineTo(330, 380);
ctx.closePath();
ctx.fill();
ctx.stroke();

// Right Shoulder Highlight
ctx.fillStyle = '#445666';
ctx.beginPath();
ctx.moveTo(380, 336);
ctx.bezierCurveTo(410, 333, 435, 336, 447, 340);
ctx.lineTo(447, 348);
ctx.bezierCurveTo(425, 344, 400, 343, 380, 348);
ctx.closePath();
ctx.fill();

ctx.restore();

// =========================================================================
// 11. LAYER 10: MASTER COMIC INK HATCHING & LINEWORK PASS
// =========================================================================
ctx.save();
ctx.strokeStyle = '#180904';
ctx.lineWidth = 1.1;

// Neck shadow crosshatching
function drawHatching(x1, y1, x2, y2, count, dx, dy) {
  for (let i = 0; i < count; i++) {
    ctx.beginPath();
    ctx.moveTo(x1 + i * dx, y1 + i * dy);
    ctx.lineTo(x2 + i * dx, y2 + i * dy);
    ctx.stroke();
  }
}

// Neck hatching lines
drawHatching(212, 335, 218, 355, 8, 3.5, -0.8);
drawHatching(225, 345, 230, 365, 5, 3.2, -0.6);

// Collar fold hatching lines
drawHatching(155, 340, 160, 365, 6, 4.0, 1.2);
drawHatching(265, 355, 275, 372, 6, 3.8, 0.5);

// Hair lock groove accents
ctx.lineWidth = 1.3;
ctx.beginPath();
ctx.moveTo(235, 60);
ctx.bezierCurveTo(255, 48, 280, 50, 305, 75);
ctx.moveTo(200, 120);
ctx.bezierCurveTo(225, 105, 255, 105, 280, 130);
ctx.moveTo(110, 160);
ctx.bezierCurveTo(80, 145, 50, 155, 25, 175);
ctx.stroke();

ctx.restore();

// Return the rendered canvas for headless evaluation
cv;

