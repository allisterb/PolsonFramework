"""Drapes a garment with Blender's cloth simulation while the body moves from rest into a pose.

The fixed operation behind `Character.drape`, run by `BlenderDriver.Drape`. Like `build_prop.py` it is a fixed script
taking data, never agent-written Python, and Blender runs it headless with autoexec off and factory settings:

    blender --background --factory-startup --disable-autoexec --python-exit-code 1         --python cloth_drape.py -- <output directory>  < request.json

The answer is `result.json` in the output directory, written by this script and by nothing else: `{"ok": true, ...}`
or `{"ok": false, "error": "..."}`. The caller deletes it first, so a stale one cannot pass for this run.

The request:
    triangles  [[a, b, c], ...]
    mask       [bool per vertex]: the garment that hangs
    keys       [[[x, y, z] per vertex], ...]: keys[0] the rest pose, the last the target pose, any in between
               the path to it; glTF units, Y up
    top        the garment's top (glTF y), where the pinned band starts
    settings   optional, each a number or a bool, as SETTINGS below; an unknown one is refused

The result: indices (every garment vertex, seam duplicates included) and positions (where each settled, glTF units),
plus counts and timings.

The body, every triangle not wholly in the garment, is a collider. The garment is cloth, pinned along a band at its
top, fading out below, and where it meets the body within that band, so those vertices follow the rigged pose and
the rest hangs; `pinEdges: true` pins everywhere it meets the body, as the first version did. Everything above `top` is fully pinned. The figure is scaled to 1.8 m, so the physical defaults behave.

Three traps, all silent, found building this:
  1. Writing a shape key's value on the cloth object each frame resets its simulation, so the cloth restarts
     from rest every frame and only the pinned band moves. The values are keyframed up front instead.
  2. TRELLIS splits vertices along UV seams; simulated unwelded, the cloth tears into loose islands. Vertices
     are welded by position first and mapped back through the weld.
  3. Blender exits 0 when a --python script raises, unless given --python-exit-code 1.
"""
import json
import os
import sys
import time

import bpy
from mathutils.bvhtree import BVHTree

#: Every setting, with its default. Types come from the defaults: an int stays an int, a bool a bool.
SETTINGS = {
    'stepsPerKey': 6, 'settle': 30, 'quality': 10, 'mass': 0.4, 'stretch': 40.0, 'shear': 40.0, 'bending': 10.0,
    'pinStiffness': 15.0, 'pinBand': 0.04, 'distance': 0.006, 'collisionQuality': 5, 'thickness': 0.004,
    'friction': 8.0, 'culling': False, 'collide': True, 'selfCollision': False, 'trace': False, 'pinEdges': False,
}

out_dir = sys.argv[sys.argv.index('--') + 1]
result_path = os.path.join(out_dir, 'result.json')


def fail(message):
    with open(result_path, 'w', encoding='utf8') as f:
        json.dump({'ok': False, 'error': message}, f)
    sys.exit(1)


try:
    d = json.load(sys.stdin)
    tris, mask, keys = d['triangles'], d['mask'], d['keys']
    top_y = float(d['top'])
except (ValueError, KeyError, TypeError) as e:
    fail(f'the request is not a drape request: {e}')
if len(keys) < 2 or any(len(k) != len(mask) for k in keys):
    fail('keys must be at least a rest and a target pose, each with one position per vertex')
opt = dict(SETTINGS)
for k, v in (d.get('settings') or {}).items():
    if k not in SETTINGS:
        fail(f"unknown setting '{k}'; known are {', '.join(SETTINGS)}")
    kind = type(SETTINGS[k])
    if kind is bool and not isinstance(v, bool) or kind is not bool and (isinstance(v, bool) or not isinstance(v, (int, float))):
        fail(f"setting '{k}' must be a {kind.__name__}")
    opt[k] = kind(v)
steps_per_key = opt['stepsPerKey']
settle = opt['settle']

ys = [p[1] for p in keys[0]]
S = 1.8 / (max(ys) - min(ys))           # glTF units to metres, for a figure 1.8 m tall


def to_b(p):                            # glTF (Y up) to Blender (Z up)
    return (p[0] * S, -p[2] * S, p[1] * S)


def to_g(co):
    return [co.x / S, co.z / S, -co.y / S]


cloth_tris = [t for t in tris if mask[t[0]] and mask[t[1]] and mask[t[2]]]
if not cloth_tris:
    fail('the mask covers no whole triangle, so there is no cloth to simulate')

scene = bpy.context.scene
for ob in list(scene.objects):
    bpy.data.objects.remove(ob, do_unlink=True)

# Weld (trap 2): each vertex maps to the first vertex at its position.
canon, first_at = {}, {}
for i, p in enumerate(keys[0]):
    canon[i] = first_at.setdefault(tuple(round(c * S, 5) for c in p), i)

# Pinning (below) is by height: the band along the garment's top. A piece of cloth that reaches no part of the band
# has nothing holding it and falls for the whole simulation, which drew long spikes below Tomas; it stays with the body.
top = top_y * S
band = opt['pinBand'] * 1.8
parent = {}


def find(i):
    while parent.setdefault(i, i) != i:
        parent[i] = parent.setdefault(parent[i], parent[i])
        i = parent[i]
    return i


for t in cloth_tris:
    a, b, c = (canon[i] for i in t)
    parent[find(a)] = find(b)
    parent[find(b)] = find(c)
held = {find(canon[i]) for t in cloth_tris for i in t if keys[0][i][1] * S > top - band}
loose = [t for t in cloth_tris if find(canon[t[0]]) not in held]
cloth_tris = [t for t in cloth_tris if find(canon[t[0]]) in held]
if not cloth_tris:
    fail('no piece of the garment reaches the pinned band at its top, so all of it would fall')
cloth_set = {tuple(t) for t in cloth_tris}
body_tris = [t for t in tris if tuple(t) not in cloth_set]


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

if opt['collide']:
    body.modifiers.new('Collision', 'COLLISION')
    body.collision.thickness_outer = opt['thickness']
    body.collision.damping = 0.8
    body.collision.cloth_friction = opt['friction']
    body.collision.use_culling = opt['culling']

# Pin: the band along the top, fading out below it, and a vertex shared with the body fully while it is in the band.
# Shared vertices lower down are left free. A reconstructed coat is one watertight surface with the legs, so its hem
# and any hole in its labels share vertices with the body; pinned, they held the lastlight3 coats rigid (38% and 49% of
# their cloth pinned, down to 9% of the height), and the drape looked like the rigged pose.
pin = cloth.vertex_groups.new(name='pin')
pin_w = []
for li, g in enumerate(cloth_used):
    z = to_b(keys[0][g])[2]
    fade = max(0.0, min(1.0, 1.0 - (top - z) / band))
    w = 1.0 if g in on_body and (opt['pinEdges'] or fade > 0) else fade
    pin_w.append(w)
    if w > 0:
        pin.add([li], w, 'REPLACE')
pinned = sum(1 for w in pin_w if w > 0)

mod = cloth.modifiers.new('Cloth', 'CLOTH')
s = mod.settings
s.quality = opt['quality']
s.mass = opt['mass']
s.tension_stiffness = s.compression_stiffness = opt['stretch']
s.shear_stiffness = opt['shear']
s.bending_stiffness = opt['bending']
s.air_damping = 1.0
s.vertex_group_mass = 'pin'
s.pin_stiffness = opt['pinStiffness']   # [0, 50]; Blender's default of 1 lets the pinned band sag
c = mod.collision_settings
c.collision_quality = opt['collisionQuality']
c.distance_min = opt['distance']
c.use_self_collision = opt['selfCollision']
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

trace = opt['trace']
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
    'ok': True,
    'indices': garment_ids,
    'positions': [to_g(me.vertices[slot[canon[i]]].co) for i in garment_ids],
    'frames': total,
    'simSeconds': round(sim, 2),
    'clothVertices': len(cloth_used),
    'clothTriangles': len(cloth_tris),
    'bodyTriangles': len(body_tris),
    'pinned': pinned,
    'looseTriangles': len(loose),
    'metresPerUnit': S,
}
ev.to_mesh_clear()
with open(result_path, 'w', encoding='utf8') as f:
    json.dump(out, f)
print(f"cloth: {len(cloth_used)} vertices ({pinned} pinned), {len(cloth_tris)} triangles; "
      f"body {len(body_tris)} triangles; {total} frames in {sim:.1f} s")
