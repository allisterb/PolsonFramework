const testFonts = [
  'Arial', 'Arial Black', 'Impact', 'Courier New', 'Consolas', 'Lucida Console',
  'Trebuchet MS', 'Verdana', 'Tahoma', 'Georgia', 'Segoe UI', 'Segoe UI Black',
  'Segoe UI Semibold', 'Franklin Gothic Medium', 'Calibri', 'Calibri Bold', 'Century Gothic',
  'Lucida Sans Unicode', 'MS Gothic', 'Palatino Linotype', 'Comic Sans MS'
];
for (const f of testFonts) {
  log(f + ': ' + Skia.Font.has(f));
}
