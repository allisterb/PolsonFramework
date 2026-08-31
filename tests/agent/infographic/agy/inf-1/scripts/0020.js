Stage.begin('Forms');
Stage.note('Form Selection based on Manual 13 §1 and brief.md data');

const questionsAndForms = [
  {
    target: 'Full 53-year span (1969-2022) chronological progression',
    question: 'Over time? (How are events spaced across history?)',
    chosen: 'Vertical continuous timeline track with true linear time mapping (Scale.linear)',
    rejected: 'Discrete step-cards (rejected: destroys the visual proof of 20-year & 15-year gaps)'
  },
  {
    target: 'Deltas between milestones (20y, 4y, 5y, 6y, 3y, 15y)',
    question: 'Compared to what? (How do the waiting periods compare to the clustering?)',
    chosen: 'Comparative horizontal gap duration bars & dimension brackets with 0-based Scale.linear',
    rejected: 'Donut / Pie (rejected: sequential duration comparisons are not part-to-whole slices)'
  },
  {
    target: '7 Landmark Milestones',
    question: 'What happened? / Locate & Identify',
    chosen: 'Neo-brutalist milestone ticket badges with hard shadows, dense monospace readouts, and punchy punchlines',
    rejected: 'Plain bullet points (rejected: lacks tactile brutalist weight and visual hierarchy)'
  },
  {
    target: 'ChatGPT velocity (100M users in 2 months)',
    question: 'How big? (Extreme metric callout)',
    chosen: 'Big-number callout badge with area-proportional stamp (Scale.radiusFor)',
    rejected: 'Gauge / dial needle (rejected: anti-pattern per Manual 13 §1)'
  }
];

questionsAndForms.forEach(qf => {
  Stage.note(`[${qf.question}] Chosen: ${qf.chosen} | Rejected: ${qf.rejected}`);
  log(`QUESTION: ${qf.question}`);
  log(`  CHOSEN: ${qf.chosen}`);
  log(`  REJECTED: ${qf.rejected}`);
});

// Verification of form counts (must be >= 3 distinct forms)
Stage.note('Form count: 4 distinct forms selected (Continuous vertical timeline, Zero-based duration bars, Neo-brutalist ticket badges, Proportional big-number callout). Exceeds minimum of 3.');

const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f4f0e6';
ctx.fillRect(0, 0, 1080, 1920);

ctx.fillStyle = '#111';
ctx.font = '700 32px monospace';
ctx.fillText('STAGE 2: FORMS SELECTED', 60, 100);

canvas;
