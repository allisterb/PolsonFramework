// One subject, drawn at its native size so nothing is resampled before the detector sees it.
const SUBJECT = __SUBJECT__;
const photo = await Photo.of(SUBJECT.name, { width: SUBJECT.width || 800 });
if (!photo.success) { error(photo.remedy); exit(photo.failureName); }

const bmp = Skia.Image.fromDataUrl(photo.toDataUri());
const canvas = createCanvas(bmp.width, bmp.height);
const ctx = canvas.getContext('2d');
ctx.drawImage(bmp, 0, 0);
log(`${SUBJECT.name}: ${bmp.width}x${bmp.height}, ${photo.creditLine()}`);
canvas;
