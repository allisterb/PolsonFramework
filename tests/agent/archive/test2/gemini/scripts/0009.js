const d = "M170 360 C145 250 190 150 260 110 C220 190 215 280 235 360 Z";
const bbox = Snap.path.getBBox(d);
const len = Snap.path.getTotalLength(d);
console.log('BBox: ' + JSON.stringify(bbox));
console.log('Total Length: ' + len);
