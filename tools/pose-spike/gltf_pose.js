// Does posing a skinned glTF actually move the mesh, and in the way a joint should?
//
// SimpleSkin is the fixture to prove it on: two joints, a bending square, and a documented
// expected result. If a rotation about the lower joint does not bend the top of the strip,
// nothing else here is worth looking at.
const mesh = Mesh.load('tests/fixtures/gltf/SimpleSkin.gltf');
log(`posable ${mesh.posable}, joints: ${mesh.joints.join(', ')}`);

const b = mesh.bounds;
log(`bind bounds ${b.width.toFixed(3)} x ${b.height.toFixed(3)} x ${b.depth.toFixed(3)}`);

// Vertex 9 is the top-right of the strip — furthest from the root, so the most moved by a bend.
const tip = mesh.vertexCount - 1;
const before = mesh.vertex(tip);
log(`tip at bind: ${before.x.toFixed(3)}, ${before.y.toFixed(3)}, ${before.z.toFixed(3)}`);

const rows = [];
for (const deg of [0, 15, 30, 45, 60, 90]) {
    const posed = mesh.pose({ [mesh.joints[1]]: { zDeg: deg } });
    const v = posed.vertex(tip);
    const moved = Math.hypot(v.x - before.x, v.y - before.y, v.z - before.z);
    rows.push({ zDeg: deg, x: +v.x.toFixed(3), y: +v.y.toFixed(3), moved: +moved.toFixed(3) });
}
table(rows);

// The property that matters: posing is measured from bind, never from where the mesh already is.
const once = mesh.pose({ [mesh.joints[1]]: { zDeg: 45 } });
const twice = once.pose({ [mesh.joints[1]]: { zDeg: 45 } });
const a = once.vertex(tip), c = twice.vertex(tip);
log(`pose(45) then pose(45) again -> ${Math.hypot(a.x - c.x, a.y - c.y).toFixed(6)} apart ` +
    `(0 means it did not accumulate)`);

// And the source is untouched.
const after = mesh.vertex(tip);
log(`source unchanged: ${Math.hypot(before.x - after.x, before.y - after.y) === 0}`);

// Refusals are NOT probed here. A bad joint name throws, and a thrown error ends the script rather
// than surfacing as a catchable JS error — the same contract `Drawing.createPerspectiveBox` states.
// So they are asserted in `MeshGltfTests`, where `Assert.Throws` can actually see them.

// The picture: bind against a bent pose, wireframe so the deformation is legible.
const canvas = createCanvas(720, 380);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f2efe8';
ctx.fillRect(0, 0, 720, 380);
ctx.textBaseline = 'top';

const shown = [{ label: 'bind', m: mesh }];
for (const deg of [30, 60, 90])
    shown.push({ label: `${deg}°`, m: mesh.pose({ [mesh.joints[1]]: { zDeg: deg } }) });

for (let i = 0; i < shown.length; i++) {
    const { label, m } = shown[i];
    Mesh.draw(ctx, m, { x: 90 + i * 180, y: 300, scale: 110, wireframe: true, lineWidth: 2 });
    ctx.fillStyle = '#1c2733';
    ctx.font = '600 13px sans-serif';
    ctx.fillText(label, 60 + i * 180, 16);
}
canvas;
