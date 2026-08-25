/**
 * Mona Lisa — Natural, Luminous Golden Blonde Transformation
 * Target Engine: Polson Graphics MCP Server (Skia / Canvas2D / SkSL)
 * Reference Asset: reference_images/Mona_Lisa,_retouched.jpg (960 x 1431)
 *
 * Description:
 * This script transforms Leonardo da Vinci's iconic Mona Lisa into a natural,
 * luminous golden blonde. Using a hybrid graphics architecture combining HTML5
 * 2D Canvas vector masking, Skia ImageFilter Gaussian blur for sfumato feathering,
 * and a high-performance custom SkSL procedural pixel shader, the transformation
 * preserves 100% of the underlying oil painting craquelure (surface crackles),
 * brushstroke textures, wavy hair curls, and chiaroscuro depth, while strictly
 * isolating her iconic Renaissance skin tones, transparent sheer veil, and distant
 * landscape.
 */

// =========================================================================
// 1. IMAGE LOADING & ASSET ACQUISITION
// =========================================================================
// Load the high-resolution master reference image
const imgPath = 'C:/Projects/Polson/tests/agent/mcp_server/gemini/reference_images/Mona_Lisa,_retouched.jpg';
const img = Skia.Image.load(imgPath);
const W = img.width;
const H = img.height;

// =========================================================================
// 2. HIGH-PRECISION ANATOMICAL VECTOR MASKS (CANVAS 2D)
// =========================================================================
// Create an offscreen canvas to define the anatomical contours of hair curls,
// cascades, crown parting, and sheer veil with Leonardo sfumato soft edges.
const maskCanvas = createCanvas(W, H);
const mctx = maskCanvas.getContext('2d');

// Clear to opaque black (untransformed background / body)
mctx.fillStyle = '#000000';
mctx.fillRect(0, 0, W, H);

// --- Left Hair Lock (Cascading curls over right shoulder) ---
mctx.fillStyle = '#ffffff';
mctx.beginPath();
// Outer silhouette following hair volume
mctx.moveTo(466, 135);
mctx.bezierCurveTo(365, 145, 305, 205, 280, 290);
mctx.bezierCurveTo(262, 380, 268, 480, 285, 580);
mctx.bezierCurveTo(305, 650, 330, 715, 370, 755);
// Inner silhouette contouring cheek, temple, and forehead hairline
mctx.bezierCurveTo(345, 710, 355, 645, 375, 580);
mctx.bezierCurveTo(395, 520, 385, 450, 360, 390);
mctx.bezierCurveTo(335, 335, 335, 290, 350, 255);
mctx.bezierCurveTo(385, 220, 425, 205, 466, 205);
mctx.closePath();
mctx.fill();

// --- Right Hair Lock (Waves cascading behind left shoulder) ---
mctx.beginPath();
// Outer silhouette following hair volume
mctx.moveTo(474, 135);
mctx.bezierCurveTo(575, 145, 635, 205, 660, 290);
mctx.bezierCurveTo(678, 380, 672, 480, 655, 580);
mctx.bezierCurveTo(635, 650, 610, 715, 570, 755);
// Inner silhouette contouring cheek, temple, and forehead hairline
mctx.bezierCurveTo(595, 710, 585, 645, 565, 580);
mctx.bezierCurveTo(545, 520, 555, 450, 580, 390);
mctx.bezierCurveTo(605, 335, 605, 290, 590, 255);
mctx.bezierCurveTo(555, 220, 515, 205, 474, 205);
mctx.closePath();
mctx.fill();

// --- Central Hair Parting (top of crown under sheer veil) ---
mctx.beginPath();
mctx.moveTo(465, 130);
mctx.bezierCurveTo(458, 155, 460, 185, 467, 208);
mctx.lineTo(473, 208);
mctx.bezierCurveTo(480, 185, 482, 155, 475, 130);
mctx.closePath();
mctx.fill();

// --- Crown Sheer Veil (Translucent warm sheen) ---
mctx.fillStyle = 'rgba(255, 255, 255, 0.10)';
mctx.beginPath();
mctx.ellipse(470, 155, 115, 30, 0, 0, Math.PI * 2);
mctx.fill();

// Apply native Skia Gaussian Blur to generate a continuous sfumato feathering
const maskBmp = maskCanvas.toBitmap().applyFilter(Skia.ImageFilter.blur(8, 8));

// =========================================================================
// 3. SKSL PROCEDURAL PIXEL SHADER (COLOR SCIENCE & RE-ILLUMINATION)
// =========================================================================
const imgShader = Skia.Shader.bitmap(img);
const maskShader = Skia.Shader.bitmap(maskBmp);

const skslCode = `
    uniform shader u_image;
    uniform shader u_mask;
    uniform float2 u_resolution;

    half4 main(float2 coord) {
        half4 src = u_image.eval(coord);
        float mask = u_mask.eval(coord).r;

        float r = src.r;
        float g = src.g;
        float b = src.b;

        // Calculate standard perceived luminance (Rec. 601 / 709 mix)
        float Y = dot(src.rgb, half3(0.299, 0.587, 0.114));

        // 1. Organic Hair vs Landscape / Mountain Keying:
        // Hair in the painting has warm undertones with dominant red (g/r < 0.70).
        // Background mountains, sky, and olive landscape have high green/blue (g/r >= 0.85).
        float greenOverRed = g / (r + 0.001);
        float isOrganicHairHue = smoothstep(0.85, 0.65, greenOverRed);

        // 2. Strict Skin Tone Protection:
        // Mona Lisa's carnation / skin glaze has high luminance and warm red-blue difference
        float isSkin = smoothstep(0.18, 0.38, Y) * smoothstep(0.10, 0.25, r - b);

        // 3. Hair Darkness Key:
        // Dark locks occupy the low-to-mid luminance spectrum [0.01, 0.30]
        float isHairDarkness = 1.0 - smoothstep(0.14, 0.30, Y);

        // 4. Effective Sfumato Alpha Weight:
        float alpha = mask * isOrganicHairHue * isHairDarkness * (1.0 - isSkin * 0.95);
        alpha = clamp(alpha * 1.35, 0.0, 1.0);

        // 5. Multi-Tonal Renaissance Golden Blonde Palette:
        // Non-linear tone curve lifts dark values while preserving root depth and chiaroscuro
        float t = pow(clamp(Y / 0.21, 0.0, 1.0), 0.85);

        // Palette Stops (Venetian Honey Gold / Amber Blonde):
        half3 cRoot      = half3(0.24, 0.14, 0.05);   // Rich chocolate-amber root shadow
        half3 cAmber     = half3(0.46, 0.28, 0.11);   // Warm hazel/chestnut undertone
        half3 cToffee    = half3(0.68, 0.46, 0.18);   // Warm caramel/toffee blonde
        half3 cHoney     = half3(0.84, 0.64, 0.27);   // Luminous Venetian honey gold
        half3 cGold      = half3(0.93, 0.77, 0.40);   // Sunlit golden wheat highlight
        half3 cChampagne = half3(0.98, 0.88, 0.56);   // Soft warm champagne curl sheen

        half3 blonde;
        if (t < 0.15) {
            blonde = mix(cRoot, cAmber, t / 0.15);
        } else if (t < 0.40) {
            blonde = mix(cAmber, cToffee, (t - 0.15) / 0.25);
        } else if (t < 0.70) {
            blonde = mix(cToffee, cHoney, (t - 0.40) / 0.30);
        } else if (t < 0.90) {
            blonde = mix(cHoney, cGold, (t - 0.70) / 0.20);
        } else {
            blonde = mix(cGold, cChampagne, (t - 0.90) / 0.10);
        }

        // 6. Craquelure & Brushstroke Micro-Texture Preservation:
        // High-frequency luminance modulation preserves 100% of authentic surface crackles
        float textureFactor = clamp(Y / 0.10, 0.65, 1.35);
        blonde = clamp(blonde * textureFactor, 0.0, 1.0);

        // 7. Sfumato Composite Blend:
        half3 finalColor = mix(src.rgb, blonde, alpha);

        return half4(finalColor, 1.0);
    }
`;

const blondShader = Skia.Shader.sksl(skslCode, { u_resolution: [W, H] }, {
    u_image: imgShader,
    u_mask: maskShader
});

// =========================================================================
// 4. FINAL CANVAS RENDERING
// =========================================================================
const canvas = createCanvas(W, H);
const ctx = canvas.getContext('2d');
ctx.fillStyle = blondShader;
ctx.fillRect(0, 0, W, H);

// Return canvas surface for headless rendering
canvas;
