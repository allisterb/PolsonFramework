const candidates = ['Courier New','Courier','Inconsolata','Source Code Pro','JetBrains Mono','IBM Plex Mono','Roboto Mono','Impact','Arial Black','Inter Tight','Inter','Helvetica Neue','Helvetica','Franklin Gothic Heavy','Barlow Condensed','Oswald','Arial'];
const avail = [];
const miss = [];
for (const f of candidates) {
  if (Skia.Font.has(f)) avail.push(f); else miss.push(f);
}
log('AVAILABLE: ' + avail.join(', '));
log('MISSING: ' + miss.join(', '));
