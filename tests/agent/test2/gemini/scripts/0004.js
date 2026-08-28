Stage.begin('Brief');
Stage.note('Client: Sailboat Tours (romantic sunset/moonlight sailboat cruises). Needs: master vector mark, wordmark lockup, brand presentation sheet.');

const fonts = Skia.Font.families();
console.log('Available font count: ' + fonts.length);
const romanticCandidates = ['Georgia', 'Garamond', 'Palatino', 'Baskerville', 'Playfair Display', 'Cinzel', 'Cormorant Garamond', 'Trajan Pro', 'Bodoni MT', 'Didot', 'Times New Roman', 'Montserrat', 'Lato', 'Cinzel Decorative'];
const availableRomantic = romanticCandidates.filter(f => Skia.Font.has(f));
console.log('Available romantic font candidates: ' + JSON.stringify(availableRomantic));
