Stage.begin('Palette & Type');
Stage.note('Stage 5: Testing available fonts and establishing neo-brutalist type & palette tokens.');

const candidateFonts = [
  'Impact', 'Arial Black', 'Trebuchet MS', 'Arial', 'Helvetica', 'Segoe UI',
  'Courier New', 'Consolas', 'Lucida Console', 'Franklin Gothic Heavy',
  'Inter', 'Roboto', 'Montserrat', 'Public Sans', 'Liberation Sans'
];

const available = candidateFonts.filter(f => Skia.Font.has(f));
log('Available fonts on system: ' + available.join(', '));

// Check font pairing according to Manual 11
const pairing = LogoType.evaluateFontPairing('sans', 'sansSerif');
log('Pairing relationship: ' + pairing.relationship + ', score: ' + pairing.score);

// Palette definitions (Neo-brutalist saturated flats)
const palette = {
  ink: '#000000',
  paper: '#fdfbf7',
  ground: '#f4f0e6',
  yellow: '#ffe600',       // High-voltage brutalist yellow
  pink: '#ff2a6d',         // Saturated electric pink / magenta
  blue: '#05d9e8',         // Saturated electric cyan / blue
  green: '#00e676',        // Acid / neo green
  orange: '#ff5e00',       // Safety orange
  purple: '#d600ff',       // Vivid brutalist violet
  darkSlate: '#111111',
  shadowOffset: 6          // 6px hard offset
};

log('Palette tokens established: ' + JSON.stringify(palette));
