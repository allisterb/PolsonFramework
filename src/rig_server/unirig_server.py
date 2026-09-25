"""A resident UniRig server: load the two models once, rig meshes over HTTP.

UniRig ships as shell scripts that start Python, load a multi-gigabyte checkpoint, run
Blender, write files and exit -- per stage, per mesh. This keeps one process up with
both models loaded and Blender imported, and answers each request by running the same
four stages in-process:

    extract (bpy)  ->  skeleton (GPU)  ->  skin (GPU)  ->  merge (bpy)

The stages hand each other .npz files exactly as UniRig's own batch merge does, so no
intermediate FBX is written and the GPU stages never call back into Blender.

Endpoints mirror a NIM container's, so a client written for TRELLIS reads this too:

    GET  /v1/health/live       200 once the process is up
    GET  /v1/health/ready      200 once the models are loaded, 503 while loading
    GET  /v1/metadata          what is loaded, and from where
    GET  /openapi.json         the schema below
    POST /v1/infer             { "mesh": <base64 .glb>, "seed"?, "faces_target_count"? }
                               -> { "artifacts": [{ "base64", "format", "finishReason", "seed" }],
                                    "rig": { ... }, "timing_ms": { ... } }

Run it from the interpreter your UniRig install already uses -- it needs nothing that
UniRig does not (standard library HTTP, no FastAPI):

    python unirig_server.py --unirig C:\\path\\to\\UniRig-windows-rtx5090-support --port 8001

`--echo` serves the same contract without UniRig, returning the input mesh unrigged, so a
client can be tested on a machine with no GPU.

Not part of UniRig and not built by MSBuild. See README.md beside this file.
"""

import argparse
import base64
import importlib
import json
import os
import queue
import shutil
import sys
import tempfile
import threading
import time
import traceback
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

DEFAULT_SEED = 12345
DEFAULT_FACES = 50000
MAX_BODY = 256 * 1024 * 1024

OPENAPI = {
    "openapi": "3.1.0",
    "info": {"title": "UniRig (resident)", "version": "1.0.0",
             "description": "Automatic skeleton and skin weights for a mesh. Returns a rigged .glb."},
    "paths": {
        "/v1/health/live": {"get": {"responses": {"200": {"description": "up"}}}},
        "/v1/health/ready": {"get": {"responses": {"200": {"description": "models loaded"},
                                                   "503": {"description": "still loading, or failed"}}}},
        "/v1/metadata": {"get": {"responses": {"200": {"description": "what is loaded"}}}},
        "/v1/infer": {"post": {
            "requestBody": {"required": True, "content": {"application/json": {"schema": {
                "$ref": "#/components/schemas/RigRequest"}}}},
            "responses": {"200": {"description": "the rigged mesh",
                                  "content": {"application/json": {"schema": {
                                      "$ref": "#/components/schemas/RigResponse"}}}},
                          "422": {"description": "the mesh could not be rigged; `detail` says why"},
                          "503": {"description": "models not loaded yet"}}}},
    },
    "components": {"schemas": {
        "RigRequest": {"type": "object", "required": ["mesh"], "properties": {
            "mesh": {"type": "string", "contentEncoding": "base64",
                     "description": "A .glb (or the format named by `format`)."},
            "format": {"type": "string", "enum": ["glb", "gltf", "obj", "fbx"], "default": "glb"},
            "seed": {"type": "integer", "default": DEFAULT_SEED},
            "faces_target_count": {"type": "integer", "default": DEFAULT_FACES,
                                   "description": "Faces the mesh is decimated to before prediction."}}},
        "RigResponse": {"type": "object", "properties": {
            "artifacts": {"type": "array", "items": {"type": "object", "properties": {
                "base64": {"type": "string"}, "format": {"type": "string"},
                "finishReason": {"type": "string"}, "seed": {"type": "integer"}}}},
            "rig": {"type": "object", "description": "Measured from the predicted weights.",
                    "properties": {
                        "joints": {"type": "integer"},
                        "weighted_joints": {"type": "integer",
                                            "description": "Joints that carry a weight above 0.05 on some vertex."},
                        "vertices": {"type": "integer"},
                        "influences": {"type": "object",
                                       "description": "How many vertices have 1, 2, 3, 4 joints above 0.05."},
                        "names": {"type": "array", "items": {"type": "string"}},
                        "parents": {"type": "array", "items": {"type": ["integer", "null"]}}}},
            "timing_ms": {"type": "object"}}},
    }},
}


class RigFailure(Exception):
    """A request the models could not answer. The message is for the caller."""


class Echo:
    """The contract without the models, for client tests: returns the mesh as it came, or a fixed
    already-rigged file standing in for the answer, so the stages after rigging run for real."""

    def __init__(self, reply=None):
        self.ready = True
        self.error = None
        self.reply = reply

    def metadata(self):
        return {"backend": "echo", "reply": self.reply,
                "note": "no UniRig is loaded; answers with " + ("a fixed file" if self.reply else "the input, unrigged")}

    def rig(self, mesh, fmt, seed, faces):
        if self.reply:
            with open(self.reply, "rb") as f:
                mesh = f.read()
        return mesh, {"joints": 0, "weighted_joints": 0, "vertices": 0, "influences": {},
                      "names": [], "parents": []}, {"total": 0}


class UniRig:
    """Both models resident, Blender imported, one request at a time."""

    def __init__(self, root, work, keep):
        self.root = os.path.abspath(root)
        self.work = os.path.abspath(work)
        self.keep = keep
        self.ready = False
        self.error = None
        self.loaded = {}

    # ---- loading, once ---------------------------------------------------------------------

    def load(self):
        try:
            self._load()
            self.ready = True
            print("ready", flush=True)
        except Exception:
            self.error = traceback.format_exc()
            print(self.error, flush=True)

    def _load(self):
        # Every path in UniRig's configs is relative to its root, as its own scripts assume.
        os.chdir(self.root)
        if self.root not in sys.path:
            sys.path.insert(0, self.root)

        # **Import the tree's own run.py for its prelude, not for its main.** Each tree does
        # different things there before main: PR #70's adds the CUDA DLL directory spconv needs
        # on Windows, allowlists `Box` for torch.load, and gives segment_csr a CPU fallback.
        # Importing it applies whatever this tree carries, so the server does not have to know.
        run = importlib.import_module("run")
        self.run = run

        import torch
        import lightning as L
        from box import Box
        torch.set_float32_matmul_precision("high")

        self.torch, self.L = torch, L
        from src.inference.download import download
        from src.data.dataset import UniRigDatasetModule, DatasetConfig
        from src.data.datapath import Datapath
        from src.data.transform import TransformConfig
        from src.tokenizer.spec import TokenizerConfig
        from src.tokenizer.parse import get_tokenizer
        from src.model.parse import get_model
        from src.system.parse import get_system, get_writer
        from src.data.extract import extract_builtin
        from src.inference.merge import merge
        from src.data.raw_data import RawData, RawSkin

        self.UniRigDatasetModule, self.Datapath = UniRigDatasetModule, Datapath
        self.get_writer, self.extract_builtin, self.merge = get_writer, extract_builtin, merge
        self.RawData, self.RawSkin = RawData, RawSkin

        def stage(task_path, name):
            task = run.load("task", task_path)
            data_cfg = run.load("data", os.path.join("configs/data", task.components.data))
            transform_cfg = run.load("transform", os.path.join("configs/transform", task.components.transform))

            tokenizer_cfg = task.components.get("tokenizer", None)
            tokenizer = None
            if tokenizer_cfg is not None:
                tokenizer_cfg = TokenizerConfig.parse(
                    config=run.load("tokenizer", os.path.join("configs/tokenizer", tokenizer_cfg)))
                tokenizer = get_tokenizer(config=tokenizer_cfg)

            model_cfg = run.load("model", os.path.join("configs/model", task.components.model))
            self._fix_attention(model_cfg, name)
            model = get_model(tokenizer=tokenizer, **model_cfg)

            system_cfg = run.load("system", os.path.join("configs/system", task.components.system))
            system = get_system(**system_cfg, model=model,
                                optimizer_config=task.get("optimizer", None),
                                loss_config=task.get("loss", None),
                                scheduler_config=task.get("scheduler", None),
                                steps_per_epoch=1)

            ckpt = download(task.resume_from_checkpoint)
            t = time.time()
            # weights_only=True: the tree's run.py has allowlisted what the skin checkpoint needs.
            state = torch.load(ckpt, map_location="cpu", weights_only=True)
            system.load_state_dict(state["state_dict"])
            system.eval()
            print(f"{name}: loaded {ckpt} in {time.time() - t:.1f}s", flush=True)
            self.loaded[name] = ckpt

            predict_cfg = DatasetConfig.parse(config=data_cfg.predict_dataset_config).split_by_cls()
            transform = TransformConfig.parse(config=transform_cfg.predict_transform_config)
            trainer_cfg = dict(task.get("trainer", {}))
            return Box(task=task, model=model, system=system, tokenizer_cfg=tokenizer_cfg,
                       predict_cfg=predict_cfg, transform=transform, trainer_cfg=trainer_cfg)

        self.skeleton = stage("configs/task/quick_inference_skeleton_articulationxl_ar_256.yaml", "skeleton")
        self.skin = stage("configs/task/quick_inference_unirig_skin.yaml", "skin")

        # Blender is imported by extract/merge at their own import; say so if it is not there,
        # before the first request finds out after a GPU stage has already run.
        import bpy  # noqa: F401

    @staticmethod
    def _fix_attention(model_cfg, name):
        # The all-NaN skin (UniRig #26/#27, PR #70): PTv3 defaults `enable_flash` to True and
        # then asserts both fp32 upcasts off, so attention runs in bf16 unguarded. Forced off
        # here so a stock tree gets the fix too; a tree that already has it is unchanged.
        if name == "skin" and "mesh_encoder" in model_cfg:
            model_cfg.mesh_encoder.enable_flash = False
        # The skeleton LLM asks for flash_attention_2; without flash_attn importable, eager.
        if model_cfg.get("_attn_implementation") == "flash_attention_2":
            try:
                importlib.import_module("flash_attn")
            except Exception:
                model_cfg._attn_implementation = "eager"

    def metadata(self):
        torch = getattr(self, "torch", None)
        return {"backend": "unirig", "root": self.root, "ready": self.ready,
                "checkpoints": self.loaded,
                "torch": getattr(torch, "__version__", None),
                "device": torch.cuda.get_device_name(0) if torch and torch.cuda.is_available() else None,
                "error": self.error}

    # ---- one request -------------------------------------------------------------------------

    def rig(self, mesh, fmt, seed, faces):
        timing = {}
        job = tempfile.mkdtemp(prefix="rig-", dir=self.work)
        try:
            source = os.path.join(job, f"input.{fmt}")
            with open(source, "wb") as f:
                f.write(mesh)
            npz = os.path.join(job, "npz")

            t = time.time()
            # UniRig's extract logs failures and returns; the file it did or did not write is
            # the only reliable answer, so that is what is checked.
            self.extract_builtin(output_folder=npz, target_count=faces, num_runs=1, id=0,
                                 time=time.strftime("%Y%m%d-%H%M%S"), files=[(source, npz)])
            timing["extract"] = int((time.time() - t) * 1000)
            if not os.path.isfile(os.path.join(npz, "raw_data.npz")):
                raise RigFailure("Blender could not read the mesh, so nothing was extracted. "
                                 "Check that it is a valid .glb with at least one mesh.")

            t = time.time()
            self.L.seed_everything(seed, workers=True)
            self._predict(self.skeleton, npz, "raw_data.npz",
                          dict(self.skeleton.task.writer, export_npz="predict_skeleton",
                               export_fbx=None, export_obj=None, export_pc=None))
            timing["skeleton"] = int((time.time() - t) * 1000)
            if not os.path.isfile(os.path.join(npz, "predict_skeleton.npz")):
                raise RigFailure("The skeleton stage produced nothing for this mesh.")

            t = time.time()
            self._predict(self.skin, npz, "predict_skeleton.npz",
                          dict(self.skin.task.writer, export_npz="predict_skin", export_fbx=None))
            timing["skin"] = int((time.time() - t) * 1000)
            if not os.path.isfile(os.path.join(npz, "predict_skin.npz")):
                raise RigFailure("The skin stage produced nothing for this mesh.")

            skin = self.RawSkin.load(path=os.path.join(npz, "predict_skin.npz"))
            skel = self.RawData.load(path=os.path.join(npz, "predict_skeleton.npz"))
            stats = self._measure(skin, skel)

            t = time.time()
            out = os.path.join(job, "rigged.glb")
            self.merge(path=source, output_path=out, vertices=skin.vertices, joints=skin.joints,
                       skin=skin.skin, parents=skel.parents, names=skel.names, tails=skel.tails,
                       add_root=False, is_vrm=False)
            timing["merge"] = int((time.time() - t) * 1000)
            if not os.path.isfile(out):
                raise RigFailure("Blender could not write the rigged mesh.")

            with open(out, "rb") as f:
                return f.read(), stats, timing
        finally:
            if not self.keep:
                shutil.rmtree(job, ignore_errors=True)

    def _predict(self, stage, npz, data_name, writer_cfg):
        # **A fresh datamodule, writer and Trainer per request; the system is the resident part.**
        # The writer is bound to the Trainer at construction, and it carries this request's paths.
        writer_cfg = dict(writer_cfg, output_dir=None, npz_dir=os.path.dirname(npz), user_mode=False)
        writer = self.get_writer(**writer_cfg, order_config=stage.transform.order_config)
        data = self.UniRigDatasetModule(
            process_fn=stage.model._process_fn,
            predict_dataset_config=stage.predict_cfg,
            predict_transform_config=stage.transform,
            tokenizer_config=stage.tokenizer_cfg,
            debug=False, data_name=data_name,
            datapath=self.Datapath(files=[npz], cls=None), cls=None)
        trainer = self.L.Trainer(callbacks=[writer], logger=False, enable_progress_bar=False,
                                 enable_model_summary=False, **stage.trainer_cfg)
        trainer.predict(stage.system, datamodule=data, return_predictions=False)

    def _measure(self, skin, skel):
        # **A rig is verified by its weights, never by its structure.** A NaN skin passes every
        # structural check -- joint count, bind matrices, weights summing to 1 -- and binds the
        # whole mesh to one static bone. So the weights themselves are read, and refused.
        import numpy as np
        w = np.asarray(skin.skin, dtype=np.float32)
        if not np.all(np.isfinite(w)):
            bad = int(np.count_nonzero(~np.isfinite(w)))
            raise RigFailure(f"The predicted skin weights are not finite ({bad:,} of {w.size:,} values); "
                             "a rig built from them would not deform. This is UniRig #26/#27: check that "
                             "the skin model runs with flash attention off.")
        above = w > 0.05
        weighted = int(np.count_nonzero(above.any(axis=0)))
        if weighted < 3:
            raise RigFailure(f"Only {weighted} joint(s) carry any weight, so the rig would not deform. "
                             "The skeleton may not fit this mesh; try another seed.")
        per_vertex = above.sum(axis=1)
        influences = {str(k): int(np.count_nonzero(per_vertex == k)) for k in range(1, 5)}
        parents = [None if p is None else int(p) for p in list(skel.parents)]
        return {"joints": int(w.shape[1]), "weighted_joints": weighted, "vertices": int(w.shape[0]),
                "influences": influences, "names": [str(n) for n in list(skel.names)],
                "parents": parents}


class MainThread:
    """Runs work on the process's main thread, where Blender's `bpy` has a context.

    **`bpy` operators only work on the main thread.** Called from an HTTP handler's thread, the
    first rig failed inside Blender with `'Context' object has no attribute 'object'`: the context
    that an operator reads its active object from exists on the main thread and nowhere else. So the
    HTTP server runs on a background thread and hands every piece of model and Blender work -- the
    loading too -- to the main thread through this queue, one at a time.
    """

    def __init__(self):
        self.jobs = queue.Queue()

    def call(self, fn):
        done, box = threading.Event(), {}

        def job():
            try:
                box["value"] = fn()
            except BaseException as e:  # noqa: BLE001 -- re-raised on the caller's thread
                box["error"] = e
                box["trace"] = traceback.format_exc()
            finally:
                done.set()

        self.jobs.put(job)
        done.wait()
        if "error" in box:
            box["error"].trace = box["trace"]
            raise box["error"]
        return box["value"]

    def run_forever(self):
        while True:
            try:
                job = self.jobs.get(timeout=0.5)  # a timeout, so Ctrl-C is heard between jobs
            except queue.Empty:
                continue
            job()


def make_handler(backend, main):
    class Handler(BaseHTTPRequestHandler):
        server_version = "unirig-server/1.0"

        def _json(self, status, body):
            data = json.dumps(body).encode("utf-8")
            self.send_response(status)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(data)))
            self.end_headers()
            self.wfile.write(data)

        def log_message(self, fmt, *args):
            sys.stderr.write("%s - %s\n" % (self.address_string(), fmt % args))

        def do_GET(self):
            path = self.path.split("?")[0]
            if path == "/v1/health/live":
                return self._json(200, {"status": "live"})
            if path == "/v1/health/ready":
                if backend.ready:
                    return self._json(200, {"status": "ready"})
                return self._json(503, {"status": "failed" if backend.error else "loading",
                                        "detail": backend.error})
            if path == "/v1/metadata":
                return self._json(200, backend.metadata())
            if path == "/openapi.json":
                return self._json(200, OPENAPI)
            return self._json(404, {"detail": f"No route {path}."})

        def do_POST(self):
            if self.path.split("?")[0] != "/v1/infer":
                return self._json(404, {"detail": f"No route {self.path}."})
            if not backend.ready:
                return self._json(503, {"detail": "The models are still loading." if not backend.error
                                        else "The models failed to load: " + backend.error})
            try:
                length = int(self.headers.get("Content-Length", "0"))
                if length <= 0 or length > MAX_BODY:
                    return self._json(413 if length > MAX_BODY else 400,
                                      {"detail": f"Body of {length} bytes; expected 1..{MAX_BODY}."})
                body = json.loads(self.rfile.read(length))
                unknown = set(body) - {"mesh", "format", "seed", "faces_target_count"}
                if unknown:
                    return self._json(422, {"detail": f"Unknown field(s): {', '.join(sorted(unknown))}."})
                if not isinstance(body.get("mesh"), str) or not body["mesh"]:
                    return self._json(422, {"detail": "`mesh` is required: the mesh as base64."})
                fmt = body.get("format", "glb")
                if fmt not in ("glb", "gltf", "obj", "fbx"):
                    return self._json(422, {"detail": f"`format` must be glb, gltf, obj or fbx; got {fmt!r}."})
                mesh = base64.b64decode(body["mesh"], validate=True)
                seed = int(body.get("seed", DEFAULT_SEED))
                faces = int(body.get("faces_target_count", DEFAULT_FACES))
            except (ValueError, TypeError) as e:
                return self._json(422, {"detail": f"Malformed request: {e}"})

            t = time.time()
            # One GPU and one Blender, on the main thread: requests queue there rather than contend.
            try:
                rigged, stats, timing = main.call(lambda: backend.rig(mesh, fmt, seed, faces))
            except RigFailure as e:
                return self._json(422, {"detail": str(e)})
            except Exception as e:
                trace = getattr(e, "trace", None) or traceback.format_exc()
                print(trace, flush=True)
                # The traceback's tail goes back to the caller, because the console it was printed
                # to is on another machine and a bare exception name says nothing about which stage.
                return self._json(500, {"detail": f"{type(e).__name__}: {e}",
                                        "trace": trace.strip().splitlines()[-24:]})
            timing["total"] = int((time.time() - t) * 1000)
            return self._json(200, {
                "artifacts": [{"base64": base64.b64encode(rigged).decode("ascii"), "format": "glb",
                               "finishReason": "SUCCESS", "seed": seed}],
                "rig": stats, "timing_ms": timing})

    return Handler


def main():
    p = argparse.ArgumentParser(description="Resident UniRig server.")
    p.add_argument("--unirig", help="UniRig checkout to load (its run.py, configs and src/).")
    p.add_argument("--host", default="0.0.0.0")
    p.add_argument("--port", type=int, default=8001)
    p.add_argument("--work", default=None, help="Where per-request files go (default: a temp dir).")
    p.add_argument("--keep", action="store_true", help="Keep each request's files, for debugging.")
    p.add_argument("--echo", action="store_true", help="Serve the contract without UniRig.")
    p.add_argument("--echo-file", help="With --echo, answer every request with this rigged file.")
    args = p.parse_args()

    if args.echo:
        backend = Echo(os.path.abspath(args.echo_file) if args.echo_file else None)
    else:
        if not args.unirig or not os.path.isfile(os.path.join(args.unirig, "run.py")):
            p.error("--unirig must name a UniRig checkout (a directory containing run.py).")
        work = args.work or os.path.join(tempfile.gettempdir(), "unirig-server")
        os.makedirs(work, exist_ok=True)
        backend = UniRig(args.unirig, work, args.keep)

    main_thread = MainThread()
    if not args.echo:
        # Loading goes through the same queue, so Blender is imported on the thread that will use it.
        main_thread.jobs.put(backend.load)

    # The HTTP server on a background thread, so /v1/health/ready answers "loading" while the main
    # thread loads, and the main thread is free for the work only it can do.
    server = ThreadingHTTPServer((args.host, args.port), make_handler(backend, main_thread))
    threading.Thread(target=server.serve_forever, daemon=True).start()
    print(f"unirig-server on http://{args.host}:{args.port} ({'echo' if args.echo else 'loading models'})",
          flush=True)
    try:
        main_thread.run_forever()
    except KeyboardInterrupt:
        server.shutdown()


if __name__ == "__main__":
    main()
