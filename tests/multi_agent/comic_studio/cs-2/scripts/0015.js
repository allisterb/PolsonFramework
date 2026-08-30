Stage.begin('Colorist');
Stage.note('Painting the cel on the Pencilers anchors. Lighting decision, derived from the plane fits rather than chosen: the suit ramps vertically (bright #45a7f6 at the shoulders to #1e87e4 at the waist) so the key reads as coming from above; the cape does NOT ramp linearly — its four sampled lobes are all bright at their OUTER edges (d=275-430px from (560,480)) and dark near the body (d=97-163), which is a radial falloff, not a directional one. So the cape gets a radial gradient centred on the torso: dark #6d2414 where it tucks behind the figure, #ee4534 at the flowing edges. Skin and hair measured almost flat (1.8 and 3.7 per 100px) and are painted flat — inventing a shading ramp there would be my lighting, not the references.');

const A = JSON.parse(Session['ANCHORS']);
const canvas = createCanvas(1024, 1024);
const ctx = canvas.getContext('2d');

// ---------------------------------------------------------------------------
// PALETTE — every value measured off the reference (colour census + plane fits)
// ---------------------------------------------------------------------------
const PAL = {
    capeCore: '#6d2414', capeDeep: '#8f2a1a', capeMid: '#c9382a', capeLit: '#ee4534',
    capeShadow: '#532823', capeCollarLit: '#b03a2c', capeCollarDeep: '#8a2c22',
    suitLit: '#48a9f7', suitMid: '#2c93eb', suitDeep: '#1d86e3', suitLeg: '#1782e1',
    armDeep: '#3f96de', armLit: '#44afff',
    gauntletLit: '#e74a3e', gauntletDeep: '#a32613',
    bootLit: '#f54739', bootDeep: '#a12b1a',
    beltLit: '#f24337', beltDeep: '#e33832',
    skin: '#ffca28', skinShade: '#e59600',
    hairLit: '#5d433a', hairDeep: '#543930',
    gold: '#fbd736', goldDeep: '#f0c72a', emblemRed: '#d03b3f',
    eye: '#404040', brow: '#6d4c41', mouth: '#795548'
};

// Smooth closed path through measured points — quadratic midpoint interpolation,
// so a traced silhouette reads as drawn curve instead of a staircase.
function path(pts, close) {
    ctx.beginPath();
    ctx.moveTo(pts[0][0], pts[0][1]);
    for (let i = 1; i < pts.length - 1; i++) {
        const mx = (pts[i][0] + pts[i + 1][0]) / 2, my = (pts[i][1] + pts[i + 1][1]) / 2;
        ctx.quadraticCurveTo(pts[i][0], pts[i][1], mx, my);
    }
    ctx.lineTo(pts[pts.length - 1][0], pts[pts.length - 1][1]);
    if (close !== false) ctx.closePath();
}
function fillShape(pts, style) { path(pts, true); ctx.fillStyle = style; ctx.fill(); }

// ===========================================================================
// 1. CAPE — behind everything. Radial falloff centred on the torso.
// ===========================================================================
const CAPE = [
    [440,296],[424,310],[412,332],[400,356],[386,382],[364,404],[350,420],[342,428],[332,436],
    [320,444],[308,452],[296,460],[282,468],[266,476],[250,484],[232,492],[216,500],[202,508],
    [194,516],[194,524],[206,532],[224,540],[240,548],[254,556],[266,564],[276,572],[284,580],
    [290,588],[296,596],[302,604],[306,612],[310,620],[312,628],[314,640],[314,656],[314,672],
    [313,688],[312,700],[310,708],[308,716],[306,724],[304,740],[304,756],[306,764],[308,772],
    [312,780],[316,788],[322,796],[328,804],[338,812],[348,820],[364,830],[390,840],
    [430,828],[470,818],[508,808],[536,800],[552,792],[572,780],[596,786],[624,796],[652,804],[676,808],
    [684,784],[690,768],[696,752],[702,736],[708,720],[714,704],[720,688],[724,672],[730,656],
    [734,640],[740,624],[744,608],[748,592],[752,576],[756,552],[760,528],[762,504],[764,480],
    [764,456],[764,432],[762,400],[760,376],[756,360],[752,344],[748,332],[736,316],[716,300],
    [700,258],[660,244],[600,238],[540,244],[500,256],[468,274],[446,290]
];
const capeGrad = ctx.createRadialGradient(560, 480, 40, 560, 480, 360);
capeGrad.addColorStop(0.00, PAL.capeCore);
capeGrad.addColorStop(0.42, PAL.capeDeep);
capeGrad.addColorStop(0.72, PAL.capeMid);
capeGrad.addColorStop(0.88, PAL.capeLit);
capeGrad.addColorStop(1.00, '#f04836');
fillShape(CAPE, capeGrad);

// Deep occlusion where the bent arm overhangs the cape — measured #532823/#6b2d27.
ctx.save();
ctx.globalAlpha = 0.9;
ctx.filter = Skia.ImageFilter.blur(9, 9);
fillShape([[500,406],[512,420],[512,448],[506,472],[492,492],[470,506],[446,510],[430,500],
           [428,480],[440,458],[458,436],[478,416]], PAL.capeShadow);
ctx.restore();

// ===========================================================================
// 2. BLUE SUIT — torso, then legs. Vertical ramp: key light from above.
// ===========================================================================
const TORSO = [
    [478,302],[466,312],[456,326],[450,342],[444,354],[440,366],[434,378],[430,390],[434,400],
    [442,410],[448,424],[450,434],[448,446],[442,458],[440,470],[452,484],[478,496],[510,506],
    [560,510],[620,510],[668,504],[672,492],[676,482],[680,472],[684,462],[688,450],[692,440],
    [696,432],[700,420],[703,410],[706,396],[709,384],[712,372],[718,360],[726,350],[730,338],
    [730,320],[730,304],[700,298],[660,300],[620,306],[600,310],[560,304],[520,300]
];
const torsoGrad = ctx.createLinearGradient(0, 300, 0, 510);
torsoGrad.addColorStop(0, PAL.suitLit);
torsoGrad.addColorStop(1, PAL.suitDeep);
fillShape(TORSO, torsoGrad);

const LEGS = [
    [462,498],[458,520],[456,545],[452,572],[446,584],[438,596],[432,608],[426,620],[422,632],
    [420,644],[418,652],[420,662],[424,672],[432,682],[446,690],
    [468,678],[486,670],[506,650],[524,626],[540,602],[548,584],[550,574],
    [550,600],[542,624],[532,648],[524,672],[514,696],[506,720],[496,744],[486,768],
    [496,782],[518,792],[536,802],
    [548,798],[554,788],[560,776],[568,764],[576,752],[582,740],[590,728],[598,716],[604,704],
    [612,692],[618,680],[626,668],[632,656],[640,644],[646,632],[650,620],[654,608],[658,596],
    [662,584],[664,572],[666,552],[668,520],[668,498]
];
const legGrad = ctx.createLinearGradient(634, 540, 520, 812);
legGrad.addColorStop(0, PAL.suitLit);
legGrad.addColorStop(1, PAL.suitLeg);
fillShape(LEGS, legGrad);

// ===========================================================================
// 3. RAISED ARM — blue upper arm, then the red gauntlet over the forearm.
// ===========================================================================
const UPPERARM = [
    [748,208],[740,220],[732,228],[726,234],[718,240],[716,246],[712,252],[708,258],[704,264],
    [700,270],[696,276],[692,282],[686,288],[678,296],[676,310],[700,316],[740,318],[760,312],
    [770,300],[780,288],[790,276],[800,262],[810,250],[820,238],[828,224],[830,210]
];
const armGrad = ctx.createLinearGradient(681, 222, 839, 288);
armGrad.addColorStop(0, PAL.armDeep);
armGrad.addColorStop(1, PAL.armLit);
fillShape(UPPERARM, armGrad);

const GAUNTLET = [
    [662,22],[676,17],[690,22],[700,28],[710,34],[718,40],[726,46],[732,52],[740,58],[746,64],
    [752,70],[758,76],[762,82],[768,88],[772,94],[778,100],[782,106],[786,112],[790,118],
    [794,124],[798,130],[802,136],[804,142],[808,148],[812,154],[814,160],[818,166],[820,172],
    [822,178],[816,186],[806,196],[792,204],[770,208],[750,204],[742,194],[740,178],[738,166],
    [736,154],[732,142],[730,130],[726,118],[722,106],[718,94],[712,82],[708,76],[692,72],
    [672,70],[654,64],[650,56],[650,46],[654,36],[658,28]
];
const gauntGrad = ctx.createLinearGradient(706, 4, 769, 221);
gauntGrad.addColorStop(0, PAL.gauntletLit);
gauntGrad.addColorStop(1, PAL.gauntletDeep);
fillShape(GAUNTLET, gauntGrad);

// ===========================================================================
// 4. BENT ARM — forearm band, then the fist over it.
// ===========================================================================
ctx.save();
ctx.globalAlpha = 0.85;
fillShape([[356,352],[392,344],[440,410],[470,458],[452,478],[420,452],[382,398]], '#c93a2b');
ctx.restore();
const BENTFIST = [
    [362,290],[380,294],[390,304],[394,318],[394,334],[392,350],[388,362],[380,372],[366,378],
    [352,376],[342,368],[336,356],[332,342],[330,328],[332,314],[338,302],[348,293]
];
const fistGrad = ctx.createLinearGradient(340, 300, 396, 372);
fistGrad.addColorStop(0, '#e04537');
fistGrad.addColorStop(1, '#c1372a');
fillShape(BENTFIST, fistGrad);

// ===========================================================================
// 5. EMBLEM and BELT on the torso.
// ===========================================================================
const EMBLEM = [
    [617,406],[588,398],[572,380],[560,366],[552,357],[552,354],[566,346],[586,340],[605,337],
    [620,336],[636,338],[656,343],[672,349],[684,356],[680,366],[666,380],[648,394]
];
fillShape(EMBLEM, PAL.gold);
fillShape([[582,358],[600,364],[617,366],[634,364],[650,358],[640,380],[628,394],[617,402],
           [606,394],[594,380]], PAL.emblemRed);

const BELT = [
    [488,480],[500,478],[540,488],[600,504],[660,510],[678,512],[676,552],[660,550],[600,550],
    [540,532],[500,512],[488,500]
];
const beltGrad = ctx.createLinearGradient(598, 476, 577, 566);
beltGrad.addColorStop(0, PAL.beltDeep);
beltGrad.addColorStop(1, PAL.beltLit);
fillShape(BELT, beltGrad);
// Buckle: rounded diamond, measured x568-601 y500-548.
fillShape([[584,500],[594,508],[601,520],[601,532],[592,544],[584,548],[574,540],[568,528],
           [568,516],[576,506]], PAL.gold);

// ===========================================================================
// 6. CAPE COLLAR — in front of the shoulders, behind the head.
// ===========================================================================
const COLLAR = [
    [424,308],[436,284],[456,266],[484,252],[524,242],[576,238],[628,238],[672,240],[702,248],
    [714,272],[704,294],[676,308],[640,304],[600,310],[560,306],[520,306],[470,308]
];
const collarGrad = ctx.createLinearGradient(430, 260, 700, 300);
collarGrad.addColorStop(0, PAL.capeCollarLit);
collarGrad.addColorStop(0.5, PAL.capeCollarDeep);
collarGrad.addColorStop(1, PAL.capeCollarLit);
fillShape(COLLAR, collarGrad);

// ===========================================================================
// 7. HEAD — hair mass, then neck, face, features.
// ===========================================================================
const HAIR = [
    [600,64],[588,68],[560,70],[530,76],[526,86],[520,98],[510,104],[506,116],[504,126],
    [502,140],[498,152],[499,164],[498,176],[498,188],[500,200],[504,210],[510,218],[516,226],
    [522,236],[518,244],[514,250],[524,258],[540,264],[558,270],[576,272],[600,268],[624,272],
    [642,266],[656,258],[668,250],[676,240],[680,230],[686,216],[690,206],[694,194],[696,182],
    [696,170],[694,158],[692,146],[692,134],[690,122],[686,110],[680,104],[674,98],[664,92],
    [654,86],[644,76],[632,68]
];
const hairGrad = ctx.createLinearGradient(583, 44, 637, 270);
hairGrad.addColorStop(0, PAL.hairDeep);
hairGrad.addColorStop(1, PAL.hairLit);
fillShape(HAIR, hairGrad);

// Neck / collar V, measured: 64px wide at y=266 tapering to a point at (601,306).
const neckGrad = ctx.createLinearGradient(0, 262, 0, 308);
neckGrad.addColorStop(0, PAL.skin);
neckGrad.addColorStop(1, PAL.skinShade);
fillShape([[566,262],[630,262],[624,274],[620,286],[612,298],[601,308],[590,298],[580,286],
           [576,274]], neckGrad);

// Face: traced silhouette including the ear bulges at y190-210.
const FACE = [
    [598,130],[588,136],[580,142],[572,148],[560,154],[548,160],[534,166],[534,174],[532,182],
    [516,190],[514,198],[514,208],[518,214],[522,220],[530,226],[534,232],[536,238],[540,244],
    [546,250],[552,256],[560,262],[570,268],[576,274],[578,282],[582,290],[590,298],[601,306],
    [604,304],[612,298],[618,290],[621,282],[624,274],[626,268],[636,262],[644,256],[650,250],
    [656,244],[660,238],[662,232],[664,226],[668,220],[676,214],[680,208],[682,200],[682,192],
    [668,184],[666,176],[664,166],[662,160],[660,154],[658,148],[652,142],[634,136],[628,130]
];
fillShape(FACE, PAL.skin);

// Features — all measured, none placed by eye.
ctx.fillStyle = PAL.brow;
fillShape([[544,177],[548,170],[558,166],[568,167],[576,172],[576,179],[566,174],[554,174]], PAL.brow);
fillShape([[620,176],[622,168],[632,164],[644,165],[650,171],[650,178],[638,172],[628,172]], PAL.brow);
ctx.fillStyle = PAL.eye;
ctx.beginPath(); ctx.ellipse(561, 195, 10, 11, 0, 0, Math.PI * 2); ctx.fill();
ctx.beginPath(); ctx.ellipse(635, 193, 10, 11, 0, 0, Math.PI * 2); ctx.fill();
// Nose: flat top at y215, rounding to a point at (597,226).
fillShape([[588,215],[606,215],[606,220],[602,224],[597,227],[592,224],[588,220]], PAL.skinShade);
// Mouth: smile crescent, corners (574,236)/(622,236), base (598,253).
ctx.fillStyle = PAL.mouth;
ctx.beginPath();
ctx.moveTo(574, 236);
ctx.quadraticCurveTo(598, 241, 622, 236);
ctx.quadraticCurveTo(612, 254, 598, 253);
ctx.quadraticCurveTo(584, 254, 574, 236);
ctx.closePath(); ctx.fill();

// ===========================================================================
// 8. BOOT — in front of the cape.
// ===========================================================================
const BOOT = [
    [456,808],[476,806],[510,804],[540,810],[546,822],[544,832],[534,846],[520,858],[506,874],
    [490,892],[476,910],[464,928],[452,946],[440,970],[430,988],[414,1002],[386,1004],[368,988],
    [372,970],[382,952],[396,934],[412,916],[426,898],[436,874],[446,850],[452,828]
];
const bootGrad = ctx.createLinearGradient(555, 895, 360, 920);
bootGrad.addColorStop(0, PAL.bootDeep);
bootGrad.addColorStop(1, PAL.bootLit);
fillShape(BOOT, bootGrad);

canvas;
