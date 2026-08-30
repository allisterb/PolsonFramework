// Check available fonts for monospace / grotesque faces
const candidates = [
  'Courier New', 'Courier', 'Inconsolata', 'Source Code Pro', 'JetBrains Mono',
  'IBM Plex Mono', 'Roboto Mono',
  'Impact', 'Arial Black', 'Inter Tight', 'Inter', 'Helvetica Neue', 'Helvetica',
  'Franklin Gothic Heavy', 'Barlow Condensed', 'Oswald',
];
const available = candidates.filter(f => Skia.Font.has(f));
const missing = candidates.filter(f => !Skia.Font.has(f));
log('Available:', available.join(', '));
log('Missing:', missing.join(', '));
