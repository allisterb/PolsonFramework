// STAGE 4 - encoding decisions recorded against render 05.
Stage.begin('Encode');

Stage.note('TIME AXIS: Scale.linear(1969, 2022, AXIS_TOP, AXIS_BOT), 21.13 px/year. Linearity is ' +
    'asserted in every script that draws it - two probes ten years apart at opposite ends of the range ' +
    'must map to the same pixel distance, or the script throws. s2 permits a non-zero origin here ' +
    'because a timeline encodes POSITION, not length; what it may not do is bend the spacing, and that ' +
    'is the thing being checked. isZeroBased is deliberately NOT asserted on this scale - it is false, ' +
    'and correctly so.');

Stage.note('DECADE RULER added (1970-2020, ten-year steps, round numbers per s3). Strictly optional - ' +
    'every value on the canvas is already directly labelled - but without it the claim "every gap drawn ' +
    'to scale" is something the reader has to take on trust. Six evenly spaced decade marks are the ' +
    'visible evidence that the axis is not bent. Set in grey at 13px so it stays chrome and does not ' +
    'compete with the milestone years.');

Stage.note('RANKED BARS: Scale.linear(0, Scale.nice(0,20).max, ...) with scale.isZeroBased asserted ' +
    'before anything is drawn. Bandwidth 30.6px, inside s3\'s 24-32px. Sorted descending, every value ' +
    'labelled at the bar end, category labels in ink rather than in the series colour (s3).');

Stage.note('COLOUR THRESHOLD MADE PRINCIPLED. In render 05 the two highlighted bars were picked by ' +
    '"years >= 15", which is a number I chose by eye - an arbitrary cut dressed as an encoding. ' +
    'Replacing it with the MEAN INTERVAL, 53/6 = 8.83 yr, which is already a derived row in brief.md. ' +
    'That splits the six waits cleanly: 20 and 15 above, 6/5/4/3 below - the same two bars, but now the ' +
    'colour means "longer than the average wait" and a reader can check it. Same threshold drives the ' +
    'ribbon colour on the timeline, which is what ties the two forms into one system.');

Stage.note('CALLOUTS: two, not four. 100M ChatGPT users in 2 months (with the 50M/month division shown) ' +
    'and 47 of 54 years without a milestone (with 54 - 7 = 47 shown). s1 warns that more than about ' +
    'four callouts becomes a wall of shouting; two is the whole of the non-year data plus the one ' +
    'count that supports the claim directly. Wording is "HAD NO MILESTONE HERE", never "nothing ' +
    'happened" - the table supports the first and not the second.');

Stage.note('DEFECT FOUND IN 05: cluster clearance fell to 5.4px once AXIS_TOP was derived from the ' +
    'measured wordmark rather than guessed (the measured underside sat 78px lower than rev B assumed, ' +
    'shortening the axis). 2004 and 2007 now read as touching, which looks like a mistake rather than ' +
    'like deliberate crowding. Fix in Stage 5: standard block height 58 -> 52, giving 11.4px. The blocks ' +
    'shrink; the ticks do not move.');

Stage.note('NOT FIXED, AND WHY: the footer no longer bleeds off the bottom - it fills to the edge ' +
    'instead. Letting it bleed would clip the last ranked bar\'s value label, and s3 is unconditional ' +
    'that a label is never clipped. The s5 boundary-crossing requirement is already met three times over ' +
    'at the right edge, so this costs nothing.');

null;
