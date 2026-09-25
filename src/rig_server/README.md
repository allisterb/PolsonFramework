# rig_server — UniRig, resident, over HTTP

`unirig_server.py` keeps UniRig's two models loaded and rigs meshes on request, so the studio's
`GenerateCharacter` tool can rig a character in seconds instead of paying UniRig's per-run start-up
(Python, a multi-gigabyte checkpoint, Blender) for every mesh. Not built by MSBuild; not in the
solution — the underscore in the name says so, as for `adk_agent`.

It runs **on the GPU machine**, inside the Python environment your UniRig install already uses. It
needs nothing UniRig does not: the HTTP server is the standard library's.

## Run it

From the UniRig venv on the workstation:

```bash
python unirig_server.py --unirig C:\path\to\UniRig-windows-rtx5090-support --port 8001
```

- `--unirig` is the UniRig checkout: the directory holding its `run.py`, `configs/` and `src/`.
  The server imports that tree's own `run.py` for its start-up fixes (the CUDA DLL directory, the
  `Box` allowlist for `torch.load`), so a tree that works from the command line works here.
- It answers `/v1/health/live` at once and `/v1/health/ready` once both checkpoints are loaded —
  about a minute. Requests before then get `503`.
- `--keep` keeps each request's intermediate files under `--work` (default: the temp directory) for
  debugging. `--host` defaults to `0.0.0.0`, so the studio machine can reach it; open the port in the
  firewall if it cannot.

Then on the studio machine, in `bin/cli/appsettings.json`:

```json
"Characters": { "RigUrl": "http://<workstation>:8001" },
"Trellis":    { "BaseUrl": "http://<workstation>:8000" }
```

## The contract

The same shape as a NIM container, so it reads like TRELLIS:

| | |
| :--- | :--- |
| `GET /v1/health/ready` | 200 when loaded; 503 while loading or if loading failed (`detail` has the traceback) |
| `GET /v1/metadata` | the UniRig root, the checkpoints, the torch version and the GPU |
| `GET /openapi.json` | the schema |
| `POST /v1/infer` | `{ "mesh": <base64 .glb>, "seed"?, "faces_target_count"? }` → `{ "artifacts": [{ "base64", "format", "finishReason", "seed" }], "rig": {…}, "timing_ms": {…} }` |

`rig` is measured from the predicted weights: `joints`, `weighted_joints` (joints carrying a weight
above 0.05 somewhere), `vertices`, `influences` (vertices with 1–4 joints), `names`, `parents`.

## What it does per request

Exactly UniRig's four stages, in one process: **extract** (Blender reads the mesh), **skeleton**
(the autoregressive model), **skin** (the weights), **merge** (Blender writes the rigged mesh with its
original texture). The stages hand each other `.npz` files, as UniRig's own batch merge does, so no
intermediate FBX is written and the GPU stages never call back into Blender.

Two things it does that UniRig's scripts do not, both from failures recorded in
`src/UniRig-main/POLSON-INGEST.md`:

- **It forces flash attention off for the skin model** (`enable_flash: False`), the fix for the
  all-NaN skin (UniRig #26/#27, PR #70), so a stock tree is safe too.
- **It refuses a dead rig.** Non-finite weights, or fewer than three joints carrying any weight, is a
  `422` naming the cause — never a structurally perfect file that deforms nothing.

UniRig's extract and merge report failure by printing and returning; the server checks for each
stage's output file instead of trusting that.

## Without a GPU

`--echo` serves the same contract with no UniRig, returning the mesh unrigged; `--echo-file
rigged.glb` answers every request with that file instead. The second is how the studio's
`CharacterGenerationLiveTests` runs every stage after rigging on a machine that cannot rig.
