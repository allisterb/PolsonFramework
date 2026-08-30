Stage.begin('Colorist');
Stage.note('Refinement pass driven by the edge-delta report, not by eye. Six measured defects fixed: (1) the collar was a solid band across the chest, but the reference splits it — blue shows from x496 at y288 in a wedge between the shoulder lobe and the front flap, so the collar gets a notch; (2) right shoulder stopped at x730, reference carries to x754 at y320 and x738 at y336; (3) the blue upper arm started at y208 leaving a gap under the gauntlet, reference has blue at 810-826 by y192 — the gauntlet/arm boundary is one shared diagonal, so both edges now use the same points; (4) the torso lower-left followed my invented curve rather than the measured blue boundary, which jumps from x432 at y464 to x528 at y480 where the arm shadow takes over; (5) the emblems inner red was a curved band, reference is a clean triangle (582,357)-(650,357)-(616,403), verified against four scanlines; (6) the forehead showed no skin at y128 where the reference has it at 604-626.');

const canvas = createCanvas(1024, 1024);
const ctx = canvas.getContext('2d');

const PAL = {
    capeCore: '#6d2414', capeDeep: '#8f2a1a', capeMid: '#c9382a', capeLit: '#ee4534',
    capeShadow: '#532823', capeShadow2: '#6b2d27',
    capeCollarLit: '#b03a2c', capeCollarDeep: '#8a2c22',
    suitLit: '#48a9f7', suitDeep: '#1d86e3', suitLeg: '#1782e1',
    armDeep: '#3f96de', armLit: '#44afff',
    gauntletLit: '#e74a3e', gauntletDeep: '#a32613',
    bootLit: '#f54739', bootDeep: '#a12b1a',
    beltLit: '#f24337', beltDeep: '#e33832',
    skin: '#ffca28', skinShade: '#e59600',
    hairLit: '#5d433a', hairDeep: '#543930',
    gold: '#fbd736', emblemRed: '#d03b3f',
    eye: '#404040', brow: '#6d4c41', mouth: '#795548'
};
function path(pts) {
    ctx.beginPath();
    ctx.moveTo(pts[0][0], pts[0][1]);
    for (let i = 1; i < pts.length - 1; i++) {
        const mx = (pts[i][0] + pts[i + 1][0]) / 2, my = (pts[i][1] + pts[i + 1][1]) / 2;
        ctx.quadraticCurveTo(pts[i][0], pts[i][1], mx, my);
    }
    ctx.lineTo(pts[pts.length - 1][0], pts[pts.length - 1][1]);
    ctx.closePath();
}
function fillShape(pts, style) { path(pts); ctx.fillStyle = style; ctx.fill(); }

// === 1. CAPE ===============================================================
const CAPE = [
    [440,296],[424,310],[412,332],[400,356],[386,382],[364,404],[350,420],[342,428],[332,436],
    [320,444],[308,452],[296,460],[282,468],[266,476],[250,484],[232,492],[216,500],[202,508],
    [194,516],[194,524],[206,532],[224,540],[240,548],[254,556],[266,564],[276,572],[284,580],
    [290,588],[296,596],[302,604],[306,612],[310,620],[312,628],[314,640],[314,656],[314,672],
    [313,688],[312,700],[310,708],[308,716],[306,724],[304,740],[304,756],[306,764],[308,772],
    [312,780],[316,788],[322,796],[328,804],[338,812],[348,820],[364,830],[390,840],
    [430,828],[470,818],[508,808],[536,800],[552,792],[572,780],[596,788],[624,800],[650,812],[668,818],
    [678,800],[684,784],[690,768],[696,752],[702,736],[708,720],[714,704],[720,688],[724,672],
    [730,656],[734,640],[740,624],[744,608],[748,592],[752,576],[756,552],[760,528],[762,504],
    [764,480],[764,456],[764,432],[762,400],[760,376],[756,360],[752,344],[748,332],[736,316],
    [716,300],[700,258],[660,244],[600,238],[540,244],[500,256],[468,274],[446,290]
];
const capeGrad = ctx.createRadialGradient(560, 480, 40, 560, 480, 360);
capeGrad.addColorStop(0.00, PAL.capeCore);
capeGrad.addColorStop(0.42, PAL.capeDeep);
capeGrad.addColorStop(0.72, PAL.capeMid);
capeGrad.addColorStop(0.88, PAL.capeLit);
capeGrad.addColorStop(1.00, '#f04836');
fillShape(CAPE, capeGrad);

// Occlusion under the bent arm. Traced from the reference's dark band, which
// runs (500,410) -> (420,514) hugging the torso's left edge, not a blob at 430.
ctx.save();
ctx.filter = Skia.ImageFilter.blur(7, 7);
fillShape([[498,404],[512,416],[516,432],[516,450],[512,466],[504,480],[492,492],[476,502],
           [456,512],[436,520],[420,520],[412,510],[418,496],[430,482],[444,466],[458,450],
           [470,434],[482,418]], PAL.capeShadow);
ctx.restore();
ctx.save();
ctx.globalAlpha = 0.55;
ctx.filter = Skia.ImageFilter.blur(11, 11);
fillShape([[492,470],[520,478],[530,492],[524,506],[500,514],[470,516],[448,512],[444,498],
           [458,484],[474,476]], PAL.capeShadow2);
ctx.restore();

// === 2. BLUE SUIT ==========================================================
// Left edge follows the measured blue boundary, including the jump at y464->480
// where the arm shadow cuts across it.
const TORSO = [
    [478,302],[466,312],[456,326],[450,342],[444,354],[440,366],[434,378],[430,390],[434,400],
    [442,410],[448,424],[450,434],[448,446],[444,454],[432,464],[460,472],[500,478],[528,482],
    [556,490],[580,496],[620,502],[668,504],[672,492],[676,482],[680,472],[684,462],[688,450],
    [692,440],[696,432],[700,420],[703,410],[706,396],[709,384],[712,372],[718,362],[728,350],
    [740,338],[752,324],[758,314],[740,302],[700,296],[660,300],[620,306],[600,310],[560,304],
    [520,300]
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
    [494,784],[516,796],[534,806],
    [548,800],[554,788],[560,776],[568,764],[576,752],[582,740],[590,728],[598,716],[604,704],
    [612,692],[618,680],[626,668],[632,656],[640,644],[646,632],[650,620],[654,608],[658,596],
    [662,584],[664,572],[666,552],[668,520],[668,498]
];
const legGrad = ctx.createLinearGradient(634, 540, 520, 812);
legGrad.addColorStop(0, PAL.suitLit);
legGrad.addColorStop(1, PAL.suitLeg);
fillShape(LEGS, legGrad);

// === 3. RAISED ARM =========================================================
// Arm top edge and gauntlet bottom edge are the SAME diagonal (744,210)->(828,188).
const UPPERARM = [
    [744,212],[770,206],[796,198],[818,190],[828,188],[830,202],[830,216],[830,222],[828,228],
    [824,234],[820,240],[816,246],[812,252],[806,258],[802,264],[796,270],[792,276],[786,282],
    [780,288],[776,294],[770,300],[764,306],[758,312],[744,318],[712,318],[684,312],[676,302],
    [682,292],[690,284],[698,276],[706,268],[714,258],[722,248],[730,238],[736,226]
];
const armGrad = ctx.createLinearGradient(681, 222, 839, 288);
armGrad.addColorStop(0, PAL.armDeep);
armGrad.addColorStop(1, PAL.armLit);
fillShape(UPPERARM, armGrad);

const GAUNTLET = [
    [662,22],[676,17],[690,22],[700,28],[710,34],[718,40],[726,46],[732,52],[740,58],[746,64],
    [752,70],[758,76],[762,82],[768,88],[772,94],[778,100],[782,106],[786,112],[790,118],
    [794,124],[798,130],[802,136],[804,142],[808,148],[812,154],[814,160],[818,166],[822,176],
    [828,188],[818,190],[796,198],[770,206],[744,212],[740,196],[740,178],[738,166],
    [736,154],[732,142],[730,130],[726,118],[722,106],[718,94],[712,82],[708,76],[692,72],
    [672,70],[654,64],[650,56],[650,46],[654,36],[658,28]
];
const gauntGrad = ctx.createLinearGradient(706, 4, 769, 221);
gauntGrad.addColorStop(0, PAL.gauntletLit);
gauntGrad.addColorStop(1, PAL.gauntletDeep);
fillShape(GAUNTLET, gauntGrad);

// === 4. BENT ARM ===========================================================
ctx.save();
ctx.globalAlpha = 0.9;
fillShape([[354,350],[392,342],[440,408],[472,456],[454,480],[418,450],[380,398]], '#c93a2b');
ctx.restore();
fillShape([[362,290],[380,294],[390,304],[394,318],[394,334],[392,350],[388,362],[380,372],
           [366,378],[352,376],[342,368],[336,356],[332,342],[330,328],[332,314],[338,302],[348,293]],
          (() => { const g = ctx.createLinearGradient(340, 300, 396, 372);
                   g.addColorStop(0, '#e04537'); g.addColorStop(1, '#c1372a'); return g; })());

// === 5. EMBLEM and BELT ====================================================
fillShape([[617,406],[588,398],[572,380],[560,366],[552,357],[552,354],[566,346],[586,340],
           [605,337],[620,336],[636,338],[656,343],[672,349],[684,356],[680,366],[666,380],[648,394]],
          PAL.gold);
// Inner red: straight-sided triangle, checked against y360/368/384/392 scanlines.
ctx.beginPath();
ctx.moveTo(582, 357); ctx.lineTo(650, 357); ctx.lineTo(616, 403); ctx.closePath();
ctx.fillStyle = PAL.emblemRed; ctx.fill();

const beltGrad = ctx.createLinearGradient(598, 476, 577, 566);
beltGrad.addColorStop(0, PAL.beltDeep);
beltGrad.addColorStop(1, PAL.beltLit);
fillShape([[488,480],[500,478],[540,488],[600,504],[660,510],[678,512],[676,552],[660,550],
           [600,550],[540,532],[500,512],[488,500]], beltGrad);
fillShape([[584,500],[594,508],[601,520],[601,532],[592,544],[584,548],[574,540],[568,528],
           [568,516],[576,506]], PAL.gold);

// === 6. CAPE COLLAR — notched, so the chest shows between the lobes ========
const COLLAR = [
    [424,308],[436,284],[456,266],[484,252],[524,242],[576,238],[628,238],[672,240],[702,248],
    [714,272],[704,294],[676,308],[660,308],[640,306],[620,306],[600,310],[580,306],[560,304],
    [536,296],[510,286],[490,280],[470,288],[452,300]
];
const collarGrad = ctx.createLinearGradient(430, 260, 700, 300);
collarGrad.addColorStop(0, PAL.capeCollarLit);
collarGrad.addColorStop(0.5, PAL.capeCollarDeep);
collarGrad.addColorStop(1, PAL.capeCollarLit);
fillShape(COLLAR, collarGrad);

// === 7. HEAD ===============================================================
const hairGrad = ctx.createLinearGradient(583, 44, 637, 270);
hairGrad.addColorStop(0, PAL.hairDeep);
hairGrad.addColorStop(1, PAL.hairLit);
fillShape([[600,64],[588,68],[560,70],[530,76],[526,86],[520,98],[510,104],[506,116],[504,126],
           [502,140],[498,152],[499,164],[498,176],[498,188],[500,200],[504,210],[510,218],
           [516,226],[522,236],[518,244],[514,250],[524,258],[540,264],[558,270],[576,272],
           [600,268],[624,272],[642,266],[656,258],[668,250],[676,240],[680,230],[686,216],
           [690,206],[694,194],[696,182],[696,170],[694,158],[692,146],[692,134],[690,122],
           [686,110],[680,104],[674,98],[664,92],[654,86],[644,76],[632,68],[616,64]], hairGrad);

const neckGrad = ctx.createLinearGradient(0, 262, 0, 308);
neckGrad.addColorStop(0, PAL.skin);
neckGrad.addColorStop(1, PAL.skinShade);
fillShape([[566,262],[630,262],[624,274],[620,286],[612,298],[601,308],[590,298],[580,286],[576,274]],
          neckGrad);

// Face top raised to y=126 — the reference shows forehead skin at 604-626 by y128.
fillShape([[610,126],[598,129],[588,136],[580,142],[572,148],[560,154],[548,160],[534,166],
           [534,174],[532,182],[516,190],[514,198],[514,208],[518,214],[522,220],[530,226],
           [534,232],[536,238],[540,244],[546,250],[552,256],[560,262],[570,268],[576,274],
           [578,282],[582,290],[590,298],[601,306],[604,304],[612,298],[618,290],[621,282],
           [624,274],[626,268],[636,262],[644,256],[650,250],[656,244],[660,238],[662,232],
           [664,226],[668,220],[676,214],[680,208],[682,200],[682,192],[668,184],[666,176],
           [664,166],[662,160],[660,154],[658,148],[652,142],[634,136],[624,128]], PAL.skin);

fillShape([[544,177],[548,170],[558,166],[568,167],[576,172],[576,179],[566,174],[554,174]], PAL.brow);
fillShape([[620,176],[622,168],[632,164],[644,165],[650,171],[650,178],[638,172],[628,172]], PAL.brow);
ctx.fillStyle = PAL.eye;
ctx.beginPath(); ctx.ellipse(561, 195, 10, 11, 0, 0, Math.PI * 2); ctx.fill();
ctx.beginPath(); ctx.ellipse(635, 193, 10, 11, 0, 0, Math.PI * 2); ctx.fill();
fillShape([[588,215],[606,215],[606,220],[602,224],[597,227],[592,224],[588,220]], PAL.skinShade);
ctx.fillStyle = PAL.mouth;
ctx.beginPath();
ctx.moveTo(574, 236);
ctx.quadraticCurveTo(598, 241, 622, 236);
ctx.quadraticCurveTo(612, 254, 598, 253);
ctx.quadraticCurveTo(584, 254, 574, 236);
ctx.closePath(); ctx.fill();

// === 8. BOOT ===============================================================
const bootGrad = ctx.createLinearGradient(555, 895, 360, 920);
bootGrad.addColorStop(0, PAL.bootDeep);
bootGrad.addColorStop(1, PAL.bootLit);
fillShape([[456,810],[476,808],[510,806],[540,812],[546,824],[544,834],[534,848],[520,860],
           [506,876],[490,894],[476,912],[464,930],[452,948],[440,970],[430,988],[414,1002],
           [386,1004],[368,988],[372,970],[382,952],[396,934],[412,916],[426,898],[436,874],
           [446,850],[452,828]], bootGrad);

canvas;
