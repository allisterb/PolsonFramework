// STAGE 5 - decisions and measurements recorded against render 06.
Stage.begin('Palette & Type');

Stage.note('PALETTE: ink #0D0D0D, paper #F3F0E3, green-bar #C8DFBB as the printed ground, and three ' +
    'saturated flats - red #F5301B, blue #1B34DE, yellow #FFC400. No tints of one hue, no gradient ' +
    'anywhere, per the language. The green-bar band is the one pale colour and it is the GROUND rather ' +
    'than a data colour, which is what the place requires; every mark that carries a number is at full ' +
    'chroma.');

Stage.note('COLOUR CARRIES ONE MEANING ONLY: red = a wait LONGER than the mean interval (8.83 yr), ' +
    'blue = shorter. The same rule drives the timeline ribbons and the ranked bars, which is what makes ' +
    'them one system instead of two charts sharing a palette. Yellow is not a data colour at all - it ' +
    'marks the two hero milestones and the two callouts, i.e. emphasis, never quantity.');

Stage.note('SHADOWS: one offset vector (+9, +9) for the whole piece, drawn as a solid ink rectangle ' +
    'behind each block. ctx.shadowBlur is never used - the language wants geometry, and a blurred edge ' +
    'reads as a flat design with a mistake rather than as this style.');

Stage.note('CONTRAST MEASURED off the rendered pixels, not taken from the swatches. Worst text pairing ' +
    'is ink on the yellow hero block at 12.17:1, against AA body\'s 4.5 - so the "full-chroma flats ' +
    'behind black text can fail legibility" risk the brief names does not bite here. It does not bite ' +
    'because no text is ever set on the red or the blue: emphasis in the gap labels is carried by SIZE ' +
    '(40px vs 20px) rather than by colour, which was a deliberate change from the earlier drafts where ' +
    'the big gap labels were red on cream.');

Stage.note('PROBE FAILURE WORTH RECORDING: the first run of this stage threw "legibility failure: ink ' +
    'on green bar at 1.00:1". The palette was fine - the probe point had landed on a glyph of the kicker ' +
    'text, so it compared ink against ink. A sample point that drifts onto ink returns a plausible ' +
    'number and a wrong conclusion, so rev B guards every text probe: if the sampled pixel is within 12 ' +
    'units of the ink colour it reports "PROBE ON INK - MOVE IT" rather than a ratio. Verification that ' +
    'can fail silently is worth no more than not verifying.');

Stage.note('RED vs BLUE separate by only 2.05:1 in luminance. Accepted, because no information depends ' +
    'on telling them apart: every ribbon carries its year count in ink beside it, and every bar carries ' +
    'its value at the bar end. The colour is a second, redundant channel for a fact that is already ' +
    'written. A reader who cannot distinguish the two hues loses nothing.');

Stage.note('TYPE: Impact for display (the only heavy grotesque installed - Arial Black, Anton, Archivo ' +
    'Black and Oswald were all checked with Skia.Font.has and are absent), Arial bold for labels. ' +
    'Tracking comes from LogoType.computeWordmarkTracking at each size rather than from a guessed ' +
    'value, applied as the em fraction it returns. Size ladder: 209px wordmark / 56px hero year / 38px ' +
    'year / 27px label / 20px label / 13px chrome.');

Stage.note('DEFECT IN 06, to fix in Detail: the milestone label is placed at a FIXED x-offset after the ' +
    'year (132px hero, 96px standard), and Impact at 56px overruns it - hence "1969ARPANET", ' +
    '"2004FACEBOOK", "2022CHATGPT" running together. The fix is ctx.measureText on the year and placing ' +
    'the label from the measured advance, which is the same rule as measuring a paragraph before ' +
    'stacking under it. Also: "END OF JOB / SUMMARY" and "EVERY WAIT, LONGEST FIRST" start 2px inside ' +
    'the sheet and collide with the sprocket rule; they need to clear it.');

null;
