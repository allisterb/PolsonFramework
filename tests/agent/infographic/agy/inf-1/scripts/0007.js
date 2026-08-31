Stage.begin('Composition');

Stage.note('Named Composition Pattern: Overlap Stack (Manual 13 §4 #3) with Editorial Vertical Platen Flow.');
Stage.note('Place: At a line printer, watching continuous fanfold paper feed through the tractor platen.');
Stage.note('Tension Rule 1 (Dense vs Breathing): Dense middle timeline core (Y: 480–1520) with milestone cards and interval metrics; breathing upper marquee (Y: 60–440) and lower platen summary zone (Y: 1540–1860).');
Stage.note('Tension Rule 2 (Three Sizes Minimum): Body type at 16–18px, Subheadings at 32–48px, Display Hero numerals at 144px (ratio > 8x body).');
Stage.note('Tension Rule 3 (Boundary Crossing): Tractor-feed perforated margins bleed off left/right canvas edges; milestone cards break the timeline central axis; summary stats box crosses column partitions.');
Stage.note('Tension Rule 4 (Non-uniform Ground): Continuous fanfold paper tone #F5F0E6 with greenbar/zebra accounting bands, perforation stitch lines, and platen mechanical markers.');
Stage.note('Tension Rule 5 (Single Rotated System): Distinct neo-brutalist caution / status stamps rotated at -3.5° angle.');

const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');

// Base background
ctx.fillStyle = '#18191a'; // Dark chassis surround
ctx.fillRect(0, 0, 1080, 1920);

// Paper sheet
const paperMarginX = 36;
const paperWidth = 1080 - paperMarginX * 2;
const page = Layout.rect(paperMarginX, 0, paperWidth, 1920);

// Fanfold paper ground
ctx.fillStyle = '#f8f4ec';
ctx.fillRect(page.x, page.y, page.width, page.height);

// Tractor feed holes along margins
const drawSprockets = (x) => {
    ctx.fillStyle = '#18191a';
    for (let y = 30; y < 1920; y += 40) {
        ctx.beginPath();
        ctx.arc(x, y, 9, 0, Math.PI * 2);
        ctx.fill();
        ctx.strokeStyle = '#d5cebe';
        ctx.lineWidth = 2;
        ctx.stroke();
    }
};
drawSprockets(page.x + 22);
drawSprockets(page.x2 - 22);

// Structural zones via Layout
const [headerZone, mainZone, footerZone] = Layout.rows(
    Layout.inset(page, 56, 56, 20, 20),
    [22, 60, 18],
    24
);

// Draw Zone Wireframes / Armatures
ctx.lineWidth = 3;
ctx.setLineDash([8, 6]);

// 1. Header Zone
ctx.strokeStyle = '#ff3366';
ctx.strokeRect(headerZone.x, headerZone.y, headerZone.width, headerZone.height);
ctx.fillStyle = '#ff3366';
ctx.font = '700 20px "Courier New", monospace';
ctx.fillText('ZONE 1: HERO HEADER & CLAIM MARQUEE (BREATHING & IMPACT)', headerZone.x + 10, headerZone.y + 30);

// 2. Main Timeline Zone (Split into Track & Gap Comparison)
ctx.strokeStyle = '#0066ff';
ctx.strokeRect(mainZone.x, mainZone.y, mainZone.width, mainZone.height);
const [timelineCol, statsCol] = Layout.columns(mainZone, [62, 38], 24);

ctx.strokeStyle = '#00aa55';
ctx.strokeRect(timelineCol.x, timelineCol.y, timelineCol.width, timelineCol.height);
ctx.fillStyle = '#00aa55';
ctx.fillText('ZONE 2A: SCALED VERTICAL TIMELINE TRACK (DENSE)', timelineCol.x + 10, timelineCol.y + 30);

ctx.strokeStyle = '#ffaa00';
ctx.strokeRect(statsCol.x, statsCol.y, statsCol.width, statsCol.height);
ctx.fillStyle = '#d97706';
ctx.fillText('ZONE 2B: GAP BARS & COMPARISONS', statsCol.x + 10, statsCol.y + 30);

// 3. Footer Zone
ctx.strokeStyle = '#9333ea';
ctx.strokeRect(footerZone.x, footerZone.y, footerZone.width, footerZone.height);
ctx.fillStyle = '#9333ea';
ctx.fillText('ZONE 3: METRIC BADGES & PLATEN TEAR-OFF (SUMMARY)', footerZone.x + 10, footerZone.y + 30);

ctx.setLineDash([]);

// Legend overlay
ctx.fillStyle = '#000000';
ctx.fillRect(80, 1780, 920, 100);
ctx.fillStyle = '#ffffff';
ctx.font = '700 24px "Arial Black", Impact, sans-serif';
ctx.fillText('COMPOSITION ARMATURE: OVERLAP STACK / FANFOLD FEED', 100, 1820);
ctx.font = '400 16px "Courier New", monospace';
ctx.fillText('53-Year Scaled Continuum + Asymmetric Gap Comparator + Brutalist Badges', 100, 1855);

canvas;
