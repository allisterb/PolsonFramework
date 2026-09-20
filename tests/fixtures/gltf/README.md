# glTF test fixtures

Third-party assets, committed deliberately. All three are from the Khronos Group's
[glTF-Sample-Assets](https://github.com/KhronosGroup/glTF-Sample-Assets) and are
**Creative Commons Zero v1.0 Universal (CC0)** — a public-domain dedication, so committing and
redistributing them carries no obligation.

They are here because a glTF reader cannot be tested against a mesh the toolkit generates: the
whole point of the format is the skeleton and the UV atlas, and `MeshToolkit` has no writer.

| file | committed? | what it is | why this one |
| :--- | :--- | :--- | :--- |
| `SimpleSkin.gltf` | **yes**, 3.5 KB | 2 joints, a bending square, embedded base64 | Minimal skinning. Small enough to verify by hand against the Khronos skinning tutorial, so a wrong answer is legible rather than merely wrong. |
| `RiggedFigure.glb` | no, 49 KB | a humanoid rig, binary container | A real skeleton at realistic joint counts, and the GLB path rather than the JSON one. |
| `CesiumMan.glb` | no, 428 KB | a **textured** humanoid with a skin and an animation | The only one that exercises everything at once: GLB, skinning, a UV atlas and an embedded image. |

## Fetching the two that are not committed

```bash
python tests/fixtures/gltf/fetch.py
```

Idempotent, and it checks the delivered size so a truncated download fails here rather than as a
baffling parse error inside the reader.

**CC0 permits committing them; only their size argues against it** — the same bargain `bin/` strikes
for potrace and `models/` for the mediapipe weights, except that those are also licence decisions and
this is purely about bytes. `SimpleSkin.gltf` stays in the repository so the reader tests always
assert *something* on a fresh clone.

**Without them, three of the seven tests are INERT rather than skipped**, because xunit 2.9.2 has no
skip mechanism and this project does not add packages casually. They run to a green tick having
asserted nothing. `WhichFixturesArePresentIsRecorded` always runs and prints which happened, so the
loss is at least legible.

## `_synthetic-textured.gltf` — generated, never committed

`MeshGltfTests` writes this on every run: a unit quad, four texture coordinates and a 4×4 two-band
PNG, all specified byte-for-byte by the test. It exists so that **the UV-flip check never depends on
a fetched fixture** — that was the defect most likely to ship unnoticed, since a mis-flipped texture
renders perfectly and merely looks like a broken asset.

It is **hand-written JSON rather than produced by SharpGLTF's writer**, deliberately: a fixture
round-tripped through the same library that reads it would still pass if writer and reader shared a
convention error, which is precisely what this is testing for. The coordinates avoid `v = 0.5`,
where a flip is a no-op, and every `u` is distinct so a vertex is identifiable without assuming the
reader preserves file order.

Verified to fail when the flip is removed from `MeshGltf`, which is the only evidence that a passing
test means anything.

**Not covered here:** a mesh above the 65,535-vertex ceiling `FaceMesh`'s `ushort` index buffer
imposes. `MeshGltf` refuses those by name rather than wrapping silently, and that refusal is tested
with a synthetic case rather than by committing a large asset.
