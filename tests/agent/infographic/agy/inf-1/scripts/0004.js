Stage.begin('Forms');

Stage.note('Form Selection per Manual 13 §1:');
Stage.note('1. Question: Over time? How did milestones unfold across 53 years? -> Form: Continuous linearly-scaled vertical timeline spine with milestone badges.');
Stage.note('2. Question: Compared to what? How do the quiet waiting periods compare in duration? -> Form: Horizontal zero-based bar chart of the 6 gap intervals.');
Stage.note('3. Question: What share? How much of the 53 years was spent in the 2 giant lulls (66%) vs the 18-year burst (34%)? -> Form: Proportional segmented block / 53-year grid matrix.');
Stage.note('4. Question: How big? (Single key figures: 53 years, 20-yr lull, 2 months to 100M) -> Form: Heavy neo-brutalist big-number callouts.');
Stage.note('Rejected forms: Donut/Pie charts (anti-pattern in neo-brutalist language; soft circular arcs look out of place); Equal-spaced card timeline (fails the fundamental argument by erasing the scale of the gaps); Radial/Spider chart (poor precision for gap comparison).');

log('=== STAGE 2: FORM SELECTION ===');
const formChoices = [
  {
    dataGroup: '7 Historical Milestones (1969-2022)',
    question: 'Over time? (Temporal distribution)',
    chosenForm: 'Continuous Linear Vertical Timeline Spine',
    rationale: 'Physical Y-distance matches elapsed calendar years precisely via Scale.linear(1969, 2022).'
  },
  {
    dataGroup: '6 Gap Periods (20y, 4y, 5y, 6y, 3y, 15y)',
    question: 'Compared to what? (Relative duration ranking)',
    chosenForm: 'Horizontal Zero-Based Bar Chart',
    rationale: 'Allows instant visual comparison of the 20-year and 15-year droughts against the 3-6 year bursts.'
  },
  {
    dataGroup: 'Lulls vs Cluster (35y / 66% vs 18y / 34%)',
    question: 'What share? (Part-to-whole ratio of 53 years)',
    chosenForm: '53-Year Segmented Block Matrix / Waffle',
    rationale: 'Brutalist discrete units (1 block = 1 year) proving that 2/3 of internet history was waiting.'
  },
  {
    dataGroup: 'Hero Totals & Extremes (53 Years, 20-Yr Lull, 2 Mo)',
    question: 'How big? (Hero single values)',
    chosenForm: 'Neo-Brutalist Big-Number Callouts',
    rationale: 'High-contrast oversized numerals anchored in solid bordered badge blocks.'
  }
];

table(formChoices);
