Stage.begin('Data');

const claim = "Progress is constant — it never stopped coming, but it never once came on schedule.";
const reader = "General. Knows all seven events already; has never seen the gaps between them drawn to scale.";
const place = "At a line printer, watching fanfold paper feed through the platen";

log('CLAIM:   ' + claim);
log('READER:  ' + reader);
log('PLACE:   ' + place);

Stage.note('CLAIM: ' + claim);
Stage.note('READER: ' + reader);
Stage.note('PLACE: ' + place);

// Verified data table from brief.md:
// 1969 / ARPANET sends its first message (it crashed after "LO")
// 1989 / Tim Berners-Lee proposes the World Wide Web
// 1993 / Mosaic makes the web visual
// 1998 / Google is founded
// 2004 / Facebook launches — the social era begins
// 2007 / The iPhone puts the web in every pocket
// 2022 / ChatGPT reaches 100M users in 2 months

// Derived deltas (gaps):
// 1989 - 1969 = 20 years (ARPANET -> Web)
// 1993 - 1989 = 4 years (Web -> Mosaic)
// 1998 - 1993 = 5 years (Mosaic -> Google)
// 2004 - 1998 = 6 years (Google -> Facebook)
// 2007 - 2004 = 3 years (Facebook -> iPhone)
// 2022 - 2007 = 15 years (iPhone -> ChatGPT)
// Total span: 2022 - 1969 = 53 years

Stage.note('Data table verified: 7 milestone events, 6 delta intervals (20, 4, 5, 6, 3, 15 yrs), 53-year total span.');

const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f4f0ea';
ctx.fillRect(0, 0, 1080, 1920);

ctx.fillStyle = '#111111';
ctx.font = '700 28px monospace';
ctx.fillText('STAGE 1: DATA & CLAIM INITIALIZED', 60, 100);
ctx.font = '400 18px monospace';
ctx.fillText('CLAIM: ' + claim, 60, 160);
ctx.fillText('PLACE: ' + place, 60, 200);
ctx.fillText('READER: ' + reader, 60, 240);

canvas;
