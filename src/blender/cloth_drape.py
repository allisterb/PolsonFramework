"""Drapes a garment with Blender's cloth simulation while the body moves from rest into a pose.

SPIKE. Not wired into `BlenderDriver` yet; the cloth probe (`SkeletonFitProbeTests.DrapeTheCoatWithBlenderCloth`)
runs it. Like `build_prop.py` it is a fixed script taking data, never agent-written Python, and Blender runs it
headless with autoexec off and factory settings:

    blender --background --factory-startup --disable-autoexec --python-exit-code 1 \
        --python cloth_drape.py -- in.json out.json

Pass `--python-exit-code 1`: without it Blender exits 0 when this script raises, and a caller reads the
previous run's output as this one's.

in.json:
    triangles  [[a, b, c], ...]
    mask       [bool per vertex]: the garment
    keys       [[[x, y, z] per vertex], ...]: keys[0] the rest pose, the last the target pose, any in between
               the path to it; glTF units, Y up
    top        the garment's top (glTF y), where the pinned band starts
    settings   optional: stepsPerKey 6, settle 30, quality 10, mass 0.4, stretch 40, shear 40, bending 10,
               pinStiffness 15, pinBand 0.04 (a share of 1.8 m), distance 0.006 m, collisionQuality 5,
               thickness 0.004 m, friction 8, culling false, collide true, selfCollision false, trace false

out.json: indices (every garment vertex, seam duplicates included) and positions (where each settled, glTF units),
plus counts and timings.

The body, every triangle not wholly in the garment, is a collider. The garment is cloth, pinned where it meets
the body and along a band at its top, fading out below, so those vertices follow the rigged pose and the rest
hangs. Everything above `top` is fully pinned. The figure is scaled to 1.8 m, so the physical defaults behave.

Three traps, all silent, found building this:
  1. Writing a shape key's value on the cloth object each frame resets its simulation, so the cloth restarts
     from rest every frame and only the pinned band moves. The values are keyframed up front instead.
  2. TRELLIS splits vertices along UV seams; simulated unwelded, the cloth tears into loose islands. Vertices
     are welded by position first and mapped back through the weld.
  3. Blender exits 0 when a --python script raises (see above).
"""
import json
import sys
import time

import bpy
from mathutils.bvhtree import BVHTree

argv = sys.argv[sys.argv.index('--') + 1:]
src, dst = argv[0], argv[1]
with open(src, encoding='utf8') as f:
    d = json.load(f)
tris, mask, keys = d['triangles'], d['mask'], d['keys']
opt = d.get('settings', {})
steps_per_key = int(opt.get('stepsPerKey', 6))
settle = int(opt.get('settle', 30))

ys = [p[1] for p in keys[0]]
S = 1.8 / (max(ys) - min(ys))           # glTF units to metres, for a figure 1.8 m tall


def to_b(p):                            # glTF (Y up) to Blender (Z up)
    return (p[0] * S, -p[2] * S, p[1] * S)


def to_g(co):
    return [co.x / S, co.z / S, -co.y / S]


cloth_tris = [t for t in tris if mask[t[0]] and mask[t[1]] and mask[t[2]]]
body_tris = [t for t in tris if not (mask[t[0]] and mask[t[1]] and mask[t[2]])]

scene = bpy.context.scene
for ob in list(scene.objects):
    bpy.data.objects.remove(ob, do_unlink=True)

# Weld (trap 2): each vertex maps to the first vertex at its position.
canon, first_at = {}, {}
for i, p in enumerate(keys[0]):
    canon[i] = first_at.setdefault(tuple(round(c * S, 5) for c in p), i)


def build(name, tri_list):
    """A mesh object over the welded vertices these triangles use, with one shape key per pose key."""
    tri_list = [tuple(canon[i] for i in t) for t in tri_list]
    tri_list = [t for t in tri_list if len(set(t)) == 3]
    used = sorted({i for t in tri_list for i in t})
    local = {g: i for i, g in enumerate(used)}
    me = bpy.data.meshes.new(name)
    me.from_pydata([to_b(keys[0][g]) for g in used], [], [[local[i] for i in t] for t in tri_list])
    me.update()
    ob = bpy.data.objects.new(name, me)
    scene.collection.objects.link(ob)
    ob.shape_key_add(name='Basis', from_mix=False)
    for k in range(1, len(keys)):
        sk = ob.shape_key_add(name=f'k{k}', from_mix=False)
        for li, g in enumerate(used):
            sk.data[li].co = to_b(keys[k][g])
    return ob, used


body, body_used = build('body', body_tris)
cloth, cloth_used = build('cloth', cloth_tris)
on_body = set(body_used)

if opt.get('collide', True):
    body.modifiers.new('Collision', 'COLLISION')
    body.collision.thickness_outer = float(opt.get('thickness', 0.004))
    body.collision.damping = 0.8
    body.collision.cloth_friction = float(opt.get('friction', 8.0))
    body.collision.use_culling = bool(opt.get('culling', False))

# Pin: every vertex shared with the body fully; the band along the top, fading out below it.
top = d['top'] * S
band = float(opt.get('pinBand', 0.04)) * 1.8
pin = cloth.vertex_groups.new(name='pin')
pin_w = []
for li, g in enumerate(cloth_used):
    z = to_b(keys[0][g])[2]
    w = 1.0 if g in on_body else max(0.0, min(1.0, 1.0 - (top - z) / band))
    pin_w.append(w)
    if w > 0:
        pin.add([li], w, 'REPLACE')
pinned = sum(1 for w in pin_w if w > 0)

mod = cloth.modifiers.new('Cloth', 'CLOTH')
s = mod.settings
s.quality = int(opt.get('quality', 10))
s.mass = float(opt.get('mass', 0.4))
s.tension_stiffness = s.compression_stiffness = float(opt.get('stretch', 40))
s.shear_stiffness = float(opt.get('shear', 40))
s.bending_stiffness = float(opt.get('bending', 10))
s.air_damping = 1.0
s.vertex_group_mass = 'pin'
s.pin_stiffness = float(opt.get('pinStiffness', 15.0))   # [0, 50]; Blender's default of 1 lets the pinned band sag
c = mod.collision_settings
c.collision_quality = int(opt.get('collisionQuality', 5))
c.distance_min = float(opt.get('distance', 0.006))
c.use_self_collision = bool(opt.get('selfCollision', False))
c.self_distance_min = 0.004
total = 1 + steps_per_key * (len(keys) - 1) + settle
mod.point_cache.frame_start = 1
mod.point_cache.frame_end = total
scene.frame_start, scene.frame_end = 1, total


def key_values(f):
    """Linear between consecutive keys, one segment of steps_per_key frames each, then held."""
    t = (f - 1) / steps_per_key
    seg = min(int(t), len(keys) - 2)
    u = min(1.0, t - seg)
    return {k: (u if k == seg + 1 else 1.0 - u if k == seg else 0.0) for k in range(1, len(keys))}


# Keyframed, never written per frame (trap 1).
for ob in (body, cloth):
    blocks = ob.data.shape_keys.key_blocks
    for f in range(1, total + 1):
        for k, v in key_values(f).items():
            blocks[f'k{k}'].value = v
            blocks[f'k{k}'].keyframe_insert('value', frame=f)

trace = bool(opt.get('trace', False))
t0 = time.time()
for f in range(1, total + 1):
    scene.frame_set(f)
    if trace and (f == 1 or f % steps_per_key == 0):
        dg = bpy.context.evaluated_depsgraph_get()
        bm = body.evaluated_get(dg).to_mesh()
        tree = BVHTree.FromPolygons([v.co.copy() for v in bm.vertices], [list(p.vertices) for p in bm.polygons])
        body.evaluated_get(dg).to_mesh_clear()
        cm = cloth.evaluated_get(dg).to_mesh()
        dists = sorted(tree.find_nearest(v.co)[3] for v in cm.vertices)
        free = [v.co.z for li, v in enumerate(cm.vertices) if pin_w[li] == 0] or [0.0]
        cloth.evaluated_get(dg).to_mesh_clear()
        print(f"trace f{f}: {sum(1 for x in dists if x < 0.01)} within 1 cm of the body, "
              f"median {dists[len(dists) // 2] * 100:.1f} cm; free vertices z {min(free):.2f}..{max(free):.2f} m")
sim = time.time() - t0

dg = bpy.context.evaluated_depsgraph_get()
ev = cloth.evaluated_get(dg)
me = ev.to_mesh()
slot = {g: li for li, g in enumerate(cloth_used)}
garment_ids = [i for i in range(len(mask)) if mask[i] and canon[i] in slot]
out = {
    'indices': garment_ids,
    'positions': [to_g(me.vertices[slot[canon[i]]].co) for i in garment_ids],
    'frames': total,
    'simSeconds': round(sim, 2),
    'clothVertices': len(cloth_used),
    'clothTriangles': len(cloth_tris),
    'bodyTriangles': len(body_tris),
    'pinned': pinned,
    'metresPerUnit': S,
}
ev.to_mesh_clear()
with open(dst, 'w', encoding='utf8') as f:
    json.dump(out, f)
print(f"cloth: {len(cloth_used)} vertices ({pinned} pinned), {len(cloth_tris)} triangles; "
      f"body {len(body_tris)} triangles; {total} frames in {sim:.1f} s")
