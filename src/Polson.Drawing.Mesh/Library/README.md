# Library — stock bodies and pose clips

**The `.glb` files are not committed.** `py -3.13 tools/bootstrap.py` (or `--only library`) fetches them from
GitHub at a pinned Mesh2Motion commit and verifies each by SHA-256. From here the build copies them next to the
`Polson.Drawing.Mesh` assembly, so `Character.stock(...)` and `Character.retarget(character, 'clip name')` work in
any build or published container made after the fetch. A project's own `stock/` and `poses/`
folders, and `models/stock/` / `models/poses/` (or the `Characters:Stock` / `Poses:Library` settings), are searched
first; a file of the same name there is the one used.

All five files come unmodified from Mesh2Motion (`github.com/Mesh2Motion/mesh2motion-app`, `static/`, commit
`faaebc8c`), checked by git blob hash through GitHub's API and byte-identical to the copy in
`reference/projects/mesh2motion-app-main/`.

| File | Source | Author | Licence |
| :--- | :--- | :--- | :--- |
| `stock/male.glb` | `static/models-variation/human/male.glb` | Quaternius | CC0 1.0 |
| `stock/female.glb` | `static/models-variation/human/female.glb` | Quaternius | CC0 1.0 |
| `poses/human-base-animations.glb` | `static/animations/human-base-animations.glb` | Scott Petrovic | CC0 1.0 |
| `poses/human-addon-animations.glb` | `static/animations/human-addon-animations.glb` | Scott Petrovic | CC0 1.0 |
| `rigs/rig-human.glb` | `static/rigs/rig-human.glb` | Scott Petrovic | CC0 1.0 |

`rigs/rig-human.glb` is a skeleton with no mesh: the one the clips were recorded on, which the solver rig
(`SolverRig`) fits into a reconstructed body so a character built without the GPU rig service can still be posed.

The per-model authors and licences are Mesh2Motion's own, from its `src/lib/RigModelVariations.ts`; the clips and
the rig are covered by its `LICENSE-CC0.MD` ("All 3d models, blend files, rigs, animations"). CC0 asks for no credit; this
table is kept so the provenance stays with the files.

**Deliberately not shipped.** Mesh2Motion's `human-mocap-animations.glb` and `CarnegieMellonAnimations/` state no
source or come from a third party's CMU mocap sample whose terms were not read, and its CC-BY and CC-BY-SA
variations (`sophia`, `jay`, `sintel`, `bunny`) oblige a credit on anything drawn over them. Put any of those in
`models/stock/` or a project's `stock/` folder deliberately, where `Character.stocks()` reports their terms.
