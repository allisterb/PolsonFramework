// STAGE 3 - composition decisions and the s5 audit, recorded against render 04.
Stage.begin('Composition');

Stage.note('PATTERN: BLEED (Manual 13 s4, pattern 2). The hero element - the fanfold printout carrying ' +
    'the time axis - is cropped by the canvas on three edges: the wordmark overruns the right edge by a ' +
    'measured 95px, the two hero milestone blocks (1969, 2022) run off the same edge, and the summary ' +
    'footer runs off the bottom. The reader is looking at a window onto a longer sheet. s4 says Bleed is ' +
    '"best for making one value feel enormous", and the value here is the 20-year silence. Rejected: ' +
    'Specimen sheet (a strict grid of seven items would space the milestones evenly, which is the exact ' +
    'lie Stage 1 corrected); Big Object (the sheet is the ground, not an object at 40-70%); Diagonal ' +
    'drive (the language allows one committed diagonal, but a printer is a machine of right angles and ' +
    'a diagonal would fight the place).');

Stage.note('KEY ENCODING CONSTRAINT discovered while blocking, and it drove the layout: NOTHING may be ' +
    'placed inside a time gap. Anything positioned on a linear time axis reads as occurring at that ' +
    'date, so parking the ranked bars or a callout in the roomy 20-year or 15-year gaps would silently ' +
    'assert they happened then. That is why the summary forms live BELOW A TEAR PERFORATION instead - ' +
    'off-axis by construction, and diegetically right for the place, since a line printer ends a job and ' +
    'then prints its summary. The empty gaps stay empty, which also supplies s5\'s breathing zone.');

Stage.note('CLUSTER DE-COLLISION: the tick stays at true time position; only the block\'s X OFFSET and ' +
    'HEIGHT vary. Neither encodes anything, so both are free. Vertical nudging was available and refused ' +
    '- it would move a date, which is the one thing this piece cannot do. 2004 and 2007 clear each other ' +
    'by 9.9px at 22.64 px/year, which is tight and is meant to be: the crowding is the argument.');

Stage.note('s5 TENSION AUDIT, measured off render 04 rather than judged by eye. ' +
    '(1) Dense/empty: the 20-year silence is 453px = 23.6% of canvas height, against the packed ' +
    '1989-2007 cluster and the footer - passes the ~15% minimum. ' +
    '(2) Three sizes: 217px wordmark / 46px hero year / 22px label / 15px body = 14.5x body, passes the ' +
    '8x minimum. ' +
    '(3) Crossing a boundary: three crossings, all measured, not assumed. ' +
    '(4) Ground: green-bar fanfold stripes, sprocket columns both edges, tear perforation - not flat. ' +
    '(5) Rotation: nothing is rotated. Taking s5\'s explicit strict-grid exception, where tension comes ' +
    'from scale instead - correct for a printer, and the language asks for right angles.');

Stage.note('DEFECTS STILL OPEN after rev B, to fix in Encode/Type: (a) the subhead is clipped by the ' +
    '1969 block - it needs to move above the wordmark as a kicker, since the space beside the block is ' +
    'only 326px and the line is far wider; (b) the non-hero blocks still end flush at one x, which reads ' +
    'listy - the hard offset shadows of Stage 5 should break that up, and if they do not, the block ' +
    'widths need varying; (c) nothing crosses the tear perforation yet - a badge stamped over it is the ' +
    'plan for Stage 6, and badges are safe there because they carry no positional encoding.');

null;
