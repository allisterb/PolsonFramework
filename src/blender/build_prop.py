"""Builds a prop from a JSON op list. Ops on stdin, artifacts into a directory we are handed.

Invoked as a separate process by ``BlenderDriver``, never linked::

    blender --background --factory-startup --disable-autoexec \\
            --python build_prop.py -- <workdir>   < ops.json

**No caller-authored code reaches this, and no caller-supplied path reaches it at all.** The op list
is data, dispatched through explicit dicts -- never ``getattr`` on a caller string -- and the output
directory is chosen by the host, so a caller cannot name a file to write. That is the same property
that makes the potrace boundary hold, and it is why this sits inside code mode rather than beside
it. See ``docs/internal/blender-prop-pipeline.md`` sections 3 and 6.

**Results go to <workdir>/result.json rather than to stdout, and that is measured rather than
cautious.** Blender writes its own banner to stdout and the interleaving is not ordered: on a probe
this script's own ``print`` came out *before* Blender's version line. stdout here is diagnostics
only.

**Names are single-assignment.** A name is bound once by the op that creates it and never rebound;
``join`` yields a fresh name rather than writing into one of its inputs. That is what makes
``on: "seat"`` mean the same thing everywhere in a list -- see section 9 of the design doc for the
bug it prevents, where a bevel on ``seat`` rounded the legs because ``join`` had rebound ``seat`` to
mean the whole chair.

**The name analysis is deliberately duplicated in BlenderDriver.** The driver runs it as a pre-flight
so a bad list is refused before a 4.5 s Blender startup is paid for; this copy is the authoritative
one, because it is what executes, and it keeps the script correct when run directly from a probe.
Both are checked against the same corpus in ``BlenderDriverTests`` -- change one rule and change it
in three places.
"""
import json
import os
import sys
import time

import bpy


# The op list carries dimensions, not Blender's own primitive conventions, so every primitive is
# built at unit extent and `scale` is then the object's actual size in metres. A cube of size 1, a
# cylinder of radius 0.5 by depth 1 and a cone of radius 0.5 by depth 1 all occupy the same 1x1x1
# box, so `scale: [0.5, 0.5, 0.04]` means the same thing whichever shape it names.
PRIMS = {
    "cube": lambda sides: bpy.ops.mesh.primitive_cube_add(size=1.0),
    "cylinder": lambda sides: bpy.ops.mesh.primitive_cylinder_add(
        vertices=sides, radius=0.5, depth=1.0),
    "sphere": lambda sides: bpy.ops.mesh.primitive_uv_sphere_add(
        radius=0.5, segments=sides, ring_count=max(3, sides // 2)),
    "cone": lambda sides: bpy.ops.mesh.primitive_cone_add(
        vertices=sides, radius1=0.5, radius2=0.0, depth=1.0),
}

BOOLEANS = {"difference": "DIFFERENCE", "union": "UNION", "intersect": "INTERSECT"}

UV_PROJECTIONS = ("smart", "cube")


class OpError(Exception):
    """A refusal that names the op it came from, so the driver can report an index."""

    def __init__(self, message, index=None, code="error"):
        super().__init__(message)
        self.index = index
        #: A stable identifier for the rule that refused, so `BlenderDriver`'s pre-flight copy of
        #: this analysis can be tested against this one without comparing prose.
        self.code = code


class Registry:
    """Name to Blender object, kept on our side rather than read back from Blender.

    Blender appends ``.001`` to a colliding name, so a scheme that trusts ``bpy.data.objects[name]``
    is one collision away from operating on the wrong thing -- silently, because the wrong object
    builds and exports perfectly well. Single assignment means collisions should not arise; this is
    the belt to that pair of braces, and it is what blendify does for the same reason
    (``reference/projects/blendify-main``, ledgered 2026-09-21).
    """

    def __init__(self):
        self._live = {}
        self._consumed = {}

    def bind(self, name, obj, index):
        """Bind a fresh name. Rebinding anything, live or consumed, is refused."""
        if not isinstance(name, str) or not name:
            raise OpError("a name must be a non-empty string", index, "missing-name")
        if name in self._live:
            raise OpError(
                "name '%s' is already bound; names are single-assignment, so this op needs a fresh "
                "one" % name, index, "name-rebound")
        if name in self._consumed:
            raise OpError(
                "name '%s' was consumed by the %s at op %d and cannot be rebound"
                % (name, self._consumed[name][0], self._consumed[name][1]), index, "name-rebound")
        self._live[name] = obj
        return obj

    def get(self, name, index):
        """Resolve a live name, distinguishing 'never existed' from 'used up'."""
        if name in self._live:
            return self._live[name]
        if name in self._consumed:
            raise OpError(
                "name '%s' was consumed by the %s at op %d, so it no longer refers to anything"
                % (name, self._consumed[name][0], self._consumed[name][1]), index, "name-consumed")
        raise OpError(
            "name '%s' is not bound; known names are %s"
            % (name, ", ".join(sorted(self._live)) or "(none)"), index, "name-unbound")

    def consume(self, name, by, index):
        """Mark a name used up. The object is gone from the scene after this."""
        self.get(name, index)
        del self._live[name]
        self._consumed[name] = (by, index)

    @property
    def live(self):
        return dict(self._live)


# ---------------------------------------------------------------------------- the operator dances

def _select_only(obj):
    """Make `obj` the one selected, active object.

    Every `bpy.ops` call reads the selection and the active object from a hidden context, which is
    the whole reason raw bpy scripting is so easy to get subtly wrong. Nothing below touches
    selection directly; it goes through here, and each op's whole dance is atomic inside one
    function -- the convention blendify uses, where all 62 of its `bpy.ops` sites sit behind a
    `_blender_*` method.
    """
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def op_prim(reg, op, index):
    """Create a primitive at unit extent, then place and size it."""
    shape = op.get("shape")
    if shape not in PRIMS:
        raise OpError(
            "unknown shape '%s'; known shapes are %s"
            % (shape, ", ".join(sorted(PRIMS))), index, "unknown-shape")

    sides = int(op.get("sides", 32))
    if sides < 3:
        raise OpError("sides must be at least 3, got %d" % sides, index, "bad-sides")

    PRIMS[shape](sides)
    obj = bpy.context.object
    obj.name = op["name"]
    obj.location = tuple(op.get("loc", (0.0, 0.0, 0.0)))
    obj.scale = tuple(op.get("scale", (1.0, 1.0, 1.0)))
    obj.rotation_mode = "XYZ"
    obj.rotation_euler = tuple(_radians(d) for d in op.get("rotDeg", (0.0, 0.0, 0.0)))
    reg.bind(op["name"], obj, index)


def op_bevel(reg, op, index):
    """Add a bevel modifier. The name keeps meaning the same object -- this mutates, not rebinds."""
    obj = reg.get(op["on"], index)
    mod = obj.modifiers.new(name="polson_bevel", type="BEVEL")
    mod.width = float(op.get("width", 0.01))
    mod.segments = int(op.get("segments", 2))
    mod.limit_method = "ANGLE"


def op_boolean(reg, op, index):
    """Cut, merge or intersect. `on` is modified in place; `with` is consumed."""
    how = op.get("how")
    if how not in BOOLEANS:
        raise OpError(
            "unknown boolean '%s'; known are %s"
            % (how, ", ".join(sorted(BOOLEANS))), index, "unknown-boolean")

    target = reg.get(op["on"], index)
    cutter = reg.get(op["with"], index)
    if op["on"] == op["with"]:
        raise OpError("a boolean cannot take the same object as both operands", index, "same-operands")

    mod = target.modifiers.new(name="polson_boolean", type="BOOLEAN")
    mod.operation = BOOLEANS[how]
    mod.object = cutter

    # Unlink rather than delete: the modifier holds the datablock alive, so the cutter still
    # resolves while no longer being part of the scene. Export is limited to the selection anyway,
    # but an unlinked operand cannot be exported by accident under any later change to that.
    for coll in list(cutter.users_collection):
        coll.objects.unlink(cutter)
    reg.consume(op["with"], "boolean", index)


def _apply_modifiers(obj):
    """Bake an object's modifier stack into its mesh, in order.

    **This is the second half of section 9's finding, and it is not the naming half.** Blender's
    `join` merges the selection into the ACTIVE object, and the active object keeps its modifier
    stack -- which then applies to the merged result. So a bevel on `seat` rounds the legs too, and
    a boolean on a wheel rim eats the spokes that were joined into it.

    Measured, before this existed: one bevelled cube is 96 verts and a plain one is 8, so a correctly
    scoped join is 104. Joining them gave **192**, exactly twice the bevelled cube.

    Single assignment does not help here. It stops `seat` meaning two different objects; it says
    nothing about whose modifiers reach the merged geometry. Section 9 named both remedies -- "apply
    modifiers before the join, or scope them explicitly" -- and this is the first.
    """
    if not obj.modifiers:
        return
    _select_only(obj)
    for modifier in list(obj.modifiers):
        bpy.ops.object.modifier_apply(modifier=modifier.name)


def op_join(reg, op, index):
    """Merge several objects into one, under a FRESH name.

    This is the op section 9's bug came from, in both of its halves. Blender's own `join` merges the
    selection into the active object, so the result really is one of the inputs -- but the name it is
    bound to here is new, every input is consumed, and `on: "seat"` therefore cannot start meaning
    the whole chair halfway down a list.

    Every input's modifiers are baked first; see `_apply_modifiers` for why that is not optional.
    """
    names = op.get("names")
    if not isinstance(names, list) or len(names) < 2:
        raise OpError("join needs a list of at least two names", index, "join-arity")

    objs = [reg.get(n, index) for n in names]
    if len(set(names)) != len(names):
        raise OpError("join names must be distinct", index, "join-duplicate")

    for obj in objs:
        _apply_modifiers(obj)

    bpy.ops.object.select_all(action="DESELECT")
    for obj in objs:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()

    merged = bpy.context.view_layer.objects.active
    for n in names:
        reg.consume(n, "join", index)
    merged.name = op["name"]
    reg.bind(op["name"], merged, index)


def op_uv(reg, op, index):
    """Unwrap. Costs vertices: smart projection tripled the chair, 48 to 144, exactly."""
    how = op.get("how", "smart")
    if how not in UV_PROJECTIONS:
        raise OpError(
            "unknown uv projection '%s'; known are %s"
            % (how, ", ".join(UV_PROJECTIONS)), index, "unknown-uv")

    obj = reg.get(op["on"], index)
    _select_only(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    if how == "smart":
        bpy.ops.uv.smart_project(angle_limit=_radians(66.0), island_margin=0.02)
    else:
        bpy.ops.uv.cube_project(cube_size=float(op.get("size", 1.0)))
    bpy.ops.object.mode_set(mode="OBJECT")


OPS = {
    "prim": op_prim,
    "bevel": op_bevel,
    "boolean": op_boolean,
    "join": op_join,
    "uv": op_uv,
}

# Which field of an op binds a fresh name. Everything else that names something is a reference.
BINDS = {"prim": "name", "join": "name"}


def _radians(degrees):
    return float(degrees) * 0.017453292519943295


def _reset_scene():
    """Start from genuinely nothing.

    `--factory-startup` skips the user's preferences and auto-running startup scripts, which is what
    we want, but it still loads the factory *scene* -- a cube, a camera and a light. Exporting the
    selection hides that, and a probe reading the object count would still be wrong. Reading an empty
    homefile is what blendify does (`scene.py:66`) and is cheaper than deleting.
    """
    bpy.ops.wm.read_homefile(use_empty=True)


def _measure_mesh(obj):
    """Counts of the evaluated MESH -- the modelling truth, and not what the artifact carries.

    A bevel or a boolean lives in a modifier, so the object's own mesh still holds the pre-modifier
    counts: the chair is 104 verts whether or not it is bevelled, while the GLB goes 12,104 to
    138,740 bytes. Reading the evaluated mesh is what makes this agree with `export_apply`.

    It is still not the number in the file. See `_measure_glb`, and prefer that one.
    """
    deps = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(deps)
    mesh = evaluated.to_mesh()
    try:
        mesh.calc_loop_triangles()
        return len(mesh.vertices), len(mesh.loop_triangles)
    finally:
        evaluated.to_mesh_clear()


def _measure_glb(path):
    """Counts as the ARTIFACT carries them, which is the pair that matters.

    glTF stores one attribute set per vertex, so a vertex shared by faces with different normals or
    different UV islands is split on export. Measured on this interpreter: a cube goes 8 -> 24, the
    chair 104 -> 312, the bevelled chair 992 -> 3,936. Three to four times the mesh count, every
    time.

    **This is the count that hits a ceiling.** `MeshGltf` refuses a mesh over 65,535 vertices by
    name because the index buffer is 16-bit, and it is the exported count that reaches it -- so
    reporting the mesh count would understate the limit roughly fourfold. Section 9's "smart UV
    triples the vertex count, exactly" is this same splitting seen from the other side.

    Reads the GLB's own JSON chunk: 12-byte header, then a 4-byte chunk length at offset 12 and the
    chunk data at 20. Returns (None, None) rather than raising if anything is unexpected, because
    the export itself has already succeeded by this point and a count is not worth failing over.
    """
    try:
        with open(path, "rb") as f:
            raw = f.read()
        if len(raw) < 20 or raw[:4] != b"glTF":
            return None, None
        length = int.from_bytes(raw[12:16], "little")
        doc = json.loads(raw[20:20 + length].decode("utf-8"))
        accessors = doc.get("accessors", [])
        verts = tris = 0
        for mesh in doc.get("meshes", []):
            for prim in mesh.get("primitives", []):
                position = prim.get("attributes", {}).get("POSITION")
                if position is not None:
                    verts += accessors[position].get("count", 0)
                indices = prim.get("indices")
                if indices is not None:
                    tris += accessors[indices].get("count", 0) // 3
        return verts, tris
    except (OSError, ValueError, KeyError, IndexError):
        return None, None


def _export(obj, path):
    """Write one object as GLB.

    **`export_apply` defaults to False and silently drops every modifier** -- section 9's first
    finding, and the reason both chairs first exported at exactly 6,076 bytes with the bevel present
    in the JSON and no error anywhere. It is passed explicitly here and must stay that way.

    `use_selection` limits the write to this object, so consumed join inputs and unlinked boolean
    cutters cannot reach the file even if something later leaves one linked.
    """
    _select_only(obj)
    bpy.ops.export_scene.gltf(
        filepath=path,
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_yup=True)


def build(request, workdir):
    """Run the op list and export. Returns the result dict written to result.json."""
    ops = request.get("ops")
    if not isinstance(ops, list) or not ops:
        raise OpError("the request carries no ops", None, "no-ops")

    _reset_scene()
    reg = Registry()

    for index, op in enumerate(ops):
        if not isinstance(op, dict):
            raise OpError("an op must be an object", index, "not-an-op")
        name = op.get("op")
        if name not in OPS:
            raise OpError(
                "unknown op '%s'; known ops are %s"
                % (name, ", ".join(sorted(OPS))), index, "unknown-op")

        binds = BINDS.get(name)
        if binds and not op.get(binds):
            raise OpError("a %s needs a '%s'" % (name, binds), index, "missing-name")

        OPS[name](reg, op, index)

    live = reg.live
    target = request.get("export")
    if target is None:
        if len(live) != 1:
            raise OpError(
                "the request does not say what to export and %d objects are live: %s"
                % (len(live), ", ".join(sorted(live))), None, "export-ambiguous")
        target = next(iter(live))
    if target not in live:
        raise OpError(
            "cannot export '%s'; live objects are %s"
            % (target, ", ".join(sorted(live)) or "(none)"), None, "export-unknown")

    obj = live[target]
    path = os.path.join(workdir, "prop.glb")
    mesh_verts, mesh_tris = _measure_mesh(obj)
    _export(obj, path)
    verts, tris = _measure_glb(path)

    return {
        "ok": True,
        "artifact": os.path.basename(path),
        "bytes": os.path.getsize(path),
        # The artifact's own counts, so a caller can see the 65,535 ceiling coming.
        "verts": verts,
        "tris": tris,
        # The modelling counts, which are three to four times smaller and are the ones that make a
        # prop's construction legible. Kept distinct rather than reconciled: they measure different
        # things and both are worth having.
        "meshVerts": mesh_verts,
        "meshTris": mesh_tris,
        "exported": target,
        "live": sorted(live),
    }


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if not argv:
        sys.stderr.write("usage: blender ... --python build_prop.py -- <workdir>  (ops on stdin)\n")
        return 2

    workdir = argv[0]
    started = time.time()
    try:
        raw = sys.stdin.buffer.read()
        if not raw:
            raise OpError("no op list arrived on stdin")
        result = build(json.loads(raw.decode("utf-8")), workdir)
        result["ms"] = int((time.time() - started) * 1000)
        code = 0
    except OpError as e:
        result = {"ok": False, "error": str(e), "op": e.index, "code": e.code,
                  "ms": int((time.time() - started) * 1000)}
        code = 1
    except Exception as e:                                    # noqa: BLE001 - reported, not hidden
        result = {"ok": False, "error": "%s: %s" % (type(e).__name__, e), "op": None,
                  "code": "blender", "ms": int((time.time() - started) * 1000)}
        code = 1

    # Written before the exit code is returned, so a failure is legible even when Blender's own
    # teardown says something else on stderr.
    with open(os.path.join(workdir, "result.json"), "w", encoding="utf-8") as f:
        json.dump(result, f)
    if not result["ok"]:
        sys.stderr.write("build_prop: %s\n" % result["error"])
    return code


if __name__ == "__main__":
    sys.exit(main())
