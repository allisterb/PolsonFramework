// ═══════════════════════════════════════════════════════════════════════════
//  Flying superhero — recreation of reference_images/comic2.png
//  Polson Graphics MCP · Snap.svg vector build · 1024 x 1024 · transparent bg
//
//  Construction notes
//  ------------------
//  All coordinates are in the reference image's own 1024x1024 pixel space, so
//  every landmark below is a *measured* value (sampled from the reference with
//  Skia.Image.load + getPixel), not an eyeballed one. Key measurements:
//
//    head      crown y=62, chin y=276, face 526-670 x 132-276  -> 1 head ~ 200px
//    eyes      (561,195) and (635,193), r=11;  nose (597,220)
//    shoulders y=300, x 476-688      belt y 486-540      hips y 530-570
//    raised arm   shoulder (650,315) -> elbow (785,205) -> fist (676,45)
//    left arm     shoulder (486,306) -> elbow (446,448) -> fist (338,316)
//    cape         tip (196,517), right edge x=770 @ y=450, hem y~840
//
//  The figure measures ~5 head units along its action line, not the 8-head
//  heroic canon of Studio Manual 08 — the reference is an emoji-proportioned
//  figure with a deliberately oversized head. The canon was therefore used as a
//  measuring system (everything below is expressed against the measured head
//  unit) rather than as a generator.
//
//  Layer order is back-to-front: cape -> legs -> torso -> arms -> belt/emblem/
//  collar -> head. Overlapping capsules that share a userSpaceOnUse gradient
//  join seamlessly, which is how the bent limbs are built.
// ═══════════════════════════════════════════════════════════════════════════

const W = 1024, H = 1024;

// ── Gradient definitions ────────────────────────────────────────────────────
// The Snap adapter has no gradient factory, and element.attr({fill:'url(#id)'})
// rewrites the reference into a bogus colour. Seeding the document through
// Snap.parse() and setting the raw `style` string is the only route that
// survives serialisation. See findings.md.
const DEFS = `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}"><defs>
<linearGradient id="gCape" gradientUnits="userSpaceOnUse" x1="300" y1="300" x2="360" y2="836">
 <stop offset="0" stop-color="#E24032"/><stop offset="0.55" stop-color="#E8412F"/><stop offset="1" stop-color="#EE4335"/></linearGradient>
<linearGradient id="gCapeR" gradientUnits="userSpaceOnUse" x1="726" y1="330" x2="648" y2="786">
 <stop offset="0" stop-color="#7E2414"/><stop offset="0.3" stop-color="#8E2919"/><stop offset="0.55" stop-color="#AC3123"/><stop offset="0.78" stop-color="#CE3D2E"/><stop offset="1" stop-color="#E8412F"/></linearGradient>
<linearGradient id="gFold" gradientUnits="userSpaceOnUse" x1="440" y1="310" x2="360" y2="750">
 <stop offset="0" stop-color="#B23628"/><stop offset="0.4" stop-color="#9A2E1D"/><stop offset="0.8" stop-color="#B8352A"/><stop offset="1" stop-color="#E24032"/></linearGradient>
<linearGradient id="gTorso" gradientUnits="userSpaceOnUse" x1="480" y1="300" x2="700" y2="545">
 <stop offset="0" stop-color="#4BABF6"/><stop offset="1" stop-color="#2E94EB"/></linearGradient>
<linearGradient id="gShade" gradientUnits="userSpaceOnUse" x1="505" y1="335" x2="565" y2="435">
 <stop offset="0" stop-color="#2B91EA"/><stop offset="1" stop-color="#3FA3F3"/></linearGradient>
<linearGradient id="gLeg" gradientUnits="userSpaceOnUse" x1="420" y1="545" x2="645" y2="800">
 <stop offset="0" stop-color="#45A8F5"/><stop offset="1" stop-color="#2A90E9"/></linearGradient>
<linearGradient id="gBoot" gradientUnits="userSpaceOnUse" x1="545" y1="780" x2="385" y2="1005">
 <stop offset="0" stop-color="#E8412F"/><stop offset="1" stop-color="#D53D2D"/></linearGradient>
<linearGradient id="gGloveL" gradientUnits="userSpaceOnUse" x1="452" y1="456" x2="312" y2="296">
 <stop offset="0" stop-color="#B4342A"/><stop offset="1" stop-color="#E64536"/></linearGradient>
<linearGradient id="gGloveR" gradientUnits="userSpaceOnUse" x1="800" y1="206" x2="666" y2="30">
 <stop offset="0" stop-color="#B0342A"/><stop offset="1" stop-color="#E64536"/></linearGradient>
<linearGradient id="gArmR" gradientUnits="userSpaceOnUse" x1="650" y1="340" x2="800" y2="185">
 <stop offset="0" stop-color="#3199EE"/><stop offset="1" stop-color="#5DB6F8"/></linearGradient>
</defs></svg>`;

const paper = Snap.parse(DEFS);

const g    = id => ({ style: `fill:url(#${id})` });   // paint-server reference
const flat = c  => ({ style: `fill:${c}` });          // flat colour
const P    = (d, a) => paper.path(d).attr(a);

// Closed Catmull-Rom spline through measured silhouette anchors -> cubic path.
// `t` slackens the tangents; lower values tighten corners (used for the small
// faceted shapes such as the belt buckle and the chest emblem).
const smooth = (pts, t = 1) => {
    const n = pts.length, Q = i => pts[(i % n + n) % n];
    let d = `M${Q(0)[0]},${Q(0)[1]}`;
    for (let i = 0; i < n; i++) {
        const [p0, p1, p2, p3] = [Q(i - 1), Q(i), Q(i + 1), Q(i + 2)];
        d += ` C${p1[0] + (p2[0] - p0[0]) / 6 * t},${p1[1] + (p2[1] - p0[1]) / 6 * t}`
           + ` ${p2[0] - (p3[0] - p1[0]) / 6 * t},${p2[1] - (p3[1] - p1[1]) / 6 * t} ${p2[0]},${p2[1]}`;
    }
    return d + ' Z';
};

// Round-capped tapered capsule between two joints, written as explicit cubics.
// (Offsetting a Catmull-Rom spine instead produces wild overshoot at the caps,
// because the cap anchors sit far closer together than the spine anchors.)
// Bent limbs are two capsules sharing a joint; a shared userSpaceOnUse gradient
// makes the overlap invisible.
const capsule = (a, b, wa, wb) => {
    const dx = b[0] - a[0], dy = b[1] - a[1], m = Math.hypot(dx, dy) || 1;
    const ux = dx / m, uy = dy / m, nx = -uy, ny = ux, K = 4 / 3;   // 4/3·r ≈ semicircle
    const A1 = [a[0] + nx * wa, a[1] + ny * wa], A2 = [a[0] - nx * wa, a[1] - ny * wa];
    const B1 = [b[0] + nx * wb, b[1] + ny * wb], B2 = [b[0] - nx * wb, b[1] - ny * wb];
    const kb = K * wb, ka = K * wa;
    return `M${A1[0]},${A1[1]} L${B1[0]},${B1[1]}`
         + ` C${B1[0] + ux * kb},${B1[1] + uy * kb} ${B2[0] + ux * kb},${B2[1] + uy * kb} ${B2[0]},${B2[1]}`
         + ` L${A2[0]},${A2[1]}`
         + ` C${A2[0] - ux * ka},${A2[1] - uy * ka} ${A1[0] - ux * ka},${A1[1] - uy * ka} ${A1[0]},${A1[1]} Z`;
};

// ── CAPE ────────────────────────────────────────────────────────────────────
// One bright base for the whole garment, then the two shadow masses painted
// over it. The reference's cape is predominantly lit (#E8412F); building it
// dark-first and lightening reads as a slab, which is what the earlier drafts
// got wrong.

// Full cape silhouette: collar -> left sail (tip at 196,517) -> hem -> right
// wing (max reach x=770 at y=450) -> back to the collar.
P(smooth([[452,264],[398,290],[364,338],[356,398],[342,436],[298,462],[240,492],[196,517],
          [214,548],[252,566],[286,594],[306,634],[313,684],[306,736],[311,784],[340,818],
          [406,842],[512,836],[584,818],[638,784],[682,728],[716,660],[746,588],[764,518],
          [770,450],[764,384],[746,326],[706,282],[648,266],[560,286]]), g('gCape'));

// Right wing turned away from the light: near-maroon at the shoulder, resolving
// to full lit red by the hem (sampled #7E2414 at y=420 -> #E34032 at y=800).
P(smooth([[560,286],[648,266],[706,282],[746,326],[764,384],[770,450],[764,518],[746,588],
          [716,660],[688,716],[640,762],[592,782],[558,742],[532,652],[520,542],[522,432],[536,340]]),
  g('gCapeR'));

// The long fold running down the inside of the left sail — the single feature
// that makes the cape read as cloth rather than a flat shape.
P(smooth([[440,300],[410,344],[386,398],[390,456],[396,508],[374,560],[348,614],[332,660],
          [344,700],[368,734],[398,752],[440,742],[456,672],[462,596],[466,524],[470,444],[464,366],[456,312]]),
  g('gFold'));

// Deepest occlusion where the cape tucks behind the body.
P(smooth([[450,462],[468,496],[468,590],[456,672],[438,708],[426,668],[430,586],[436,502]], 0.7),
  flat('#7B2417'));

// ── LEGS & BOOTS ────────────────────────────────────────────────────────────
// Trailing leg: thigh swings back and down, shin carries through to the ankle.
P(capsule([582,548], [622,646], 44, 40), g('gLeg'));
P(capsule([622,642], [566,782], 40, 30), g('gLeg'));

// Far foot, tucked into the cape's shadow, so it is painted a step darker.
P(capsule([492,744], [414,784], 26, 23), flat('#CE3C2D'));

// Lead leg: thigh drives forward-left, knee foreshortened toward the viewer so
// the calf disappears behind the cape — hence the rounded stump, not a shin.
P(capsule([496,534], [440,624], 44, 40), g('gLeg'));
P(capsule([440,620], [464,676], 40, 34), g('gLeg'));

// Inner-thigh separation.
P(smooth([[543,552],[557,560],[550,632],[542,646],[535,594]], 0.6), flat('#1E85DD'));

// Knee-high boot sweeping down-left to the toe at (392,1000).
P(capsule([544,784], [462,900], 32, 28), g('gBoot'));
P(capsule([462,896], [406,986], 28, 28), g('gBoot'));

// ── TORSO ───────────────────────────────────────────────────────────────────
P(smooth([[572,278],[624,282],[666,302],[688,352],[692,412],[680,466],[672,518],[666,556],
          [560,568],[494,554],[492,508],[482,468],[474,414],[476,352],[492,312],[528,286]]),
  g('gTorso'));
P(smooth([[504,320],[530,346],[524,430],[514,510],[500,552],[490,502],[480,460],[474,414],
          [476,352]], 0.85), g('gShade'));               // core shadow down the near side
P(smooth([[500,382],[514,412],[512,460],[502,486],[490,462],[490,412]], 0.7), flat('#66271D'));
                                                        // contact shadow: arm against ribs

// ── ARMS ────────────────────────────────────────────────────────────────────
// Raised arm: upper arm out to a high elbow, forearm folding back over the head.
P(capsule([652,312], [790,200], 46, 36), g('gArmR'));
P(capsule([788,198], [686,54],  36, 40), g('gGloveR'));

// Near arm: elbow low and out, forearm angling back up across the chest.
P(capsule([486,306], [444,446], 36, 27), g('gTorso'));
P(capsule([446,448], [354,332], 27, 32), g('gGloveL'));
paper.circle(338, 316, 40).attr(g('gGloveL'));          // gloved fist

// ── BELT, EMBLEM, COLLAR ────────────────────────────────────────────────────
P(smooth([[492,486],[566,504],[666,506],[666,538],[566,540],[492,516]], 0.4), flat('#F24236'));
P(smooth([[583,494],[607,518],[583,542],[559,518]], 0.35), flat('#FDD835'));   // buckle
P(smooth([[552,338],[620,328],[686,330],[662,378],[622,408],[584,376]], 0.5), flat('#FDD835'));
P(smooth([[580,352],[621,346],[660,350],[636,378],[614,394],[594,372]], 0.5), flat('#E23936'));
P(smooth([[446,264],[490,246],[548,264],[578,296],[600,304],[622,296],[654,262],[702,250],
          [714,284],[678,316],[618,334],[558,330],[498,314],[448,296]]), flat('#ED4132'));

// ── HEAD ────────────────────────────────────────────────────────────────────
P(smooth([[572,258],[624,258],[630,282],[600,308],[572,282]], 0.5), flat('#F0B429'));  // neck

// Hair mass behind the face (measured extent x 498-697, y 62-268).
P(smooth([[598,60],[616,62],[634,72],[652,84],[668,96],[682,112],[692,136],[697,168],
          [694,200],[684,228],[680,250],[664,264],[642,268],[618,266],[598,266],[574,266],
          [550,264],[528,258],[514,244],[512,226],[502,208],[498,182],[500,154],[508,126],
          [522,98],[546,76],[572,64]]), flat('#543930'));

// Face: egg, not a circle — widest at the eye line, tapering to the chin.
P(smooth([[598,132],[634,140],[660,160],[670,192],[670,222],[656,252],[630,270],[598,276],
          [566,270],[542,252],[528,222],[526,192],[536,160],[562,140]]), flat('#FFCA28'));

// Fringe over the face; inner edge is the measured hairline, peaking at (615,127).
P(smooth([[508,222],[498,182],[500,154],[508,126],[522,98],[546,76],[572,64],[598,60],
          [622,66],[646,78],[666,94],[682,112],[692,136],[697,168],[694,200],[688,220],
          [670,206],[668,180],[660,152],[640,134],[615,127],[590,133],[565,150],[540,166],
          [531,190],[527,212]], 0.9), flat('#543930'));

// Features.
P(smooth([[534,178],[556,161],[586,166],[588,177],[558,174],[538,187]], 0.6), flat('#795548'));
P(smooth([[662,177],[640,159],[610,164],[608,175],[638,172],[658,185]], 0.6), flat('#795548'));
paper.circle(561, 195, 11).attr(flat('#404040'));
paper.circle(635, 193, 11).attr(flat('#404040'));
paper.ellipse(597, 220, 10, 6).attr(flat('#F0A81D'));
P(smooth([[564,240],[598,246],[634,238],[624,261],[598,269],[572,261]], 0.6), flat('#795548'));

paper;
