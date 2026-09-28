# Third-party code the vision scripts import

Copied **byte for byte** from upstream. **Do not edit these files.** Anything the studio needs done
differently is done in the calling script, which is how `sam2_segment.py` avoids SAM 2's hydra, iopath
and tqdm imports. Unedited copies can be re-checked against upstream at any time: `git hash-object <file>`
must print the blob hash listed below, the same hash the upstream repository's tree reports for that
path.

Only the code inference needs is here. The weights are data and are fetched by
`tools/bootstrap.py --extras`, pinned and hashed like every other model.

## `sam2/` — Meta's Segment Anything 2, image inference only

- **Source:** `github.com/facebookresearch/sam2`, commit **`2b90b9f5ceec907a1c18123530e92e794ad901a4`**
  (2024-12-16, `main` when copied on 2026-09-28). All 24 files below match that commit's tree.
- **Licence:** Apache 2.0 (`sam2/LICENSE`). The README states that the model checkpoints are also Apache 2.0.
- **Copied:** the 19 modules `SAM2ImagePredictor` imports, and the four SAM 2.1 model configs.
- **Left out:** video prediction, automatic mask generation, `build_sam.py` (hydra), training, the web
  demo, and the optional CUDA extension (`csrc/`, `LICENSE_cctorch`). Inference does not need them.
- **`sam2/sam2/__init__.py` imports hydra at import time, and `hieradet.py` imports iopath and
  `utils/misc.py` imports tqdm at module level.** Inference uses none of the three, so `sam2_segment.py`
  registers small stand-in modules before importing, rather than editing these files. Each stand-in
  carries a `ModuleSpec`, because `torch._dynamo` calls `find_spec` on module names and refuses a module
  without one.
- **Intake:** `reference/README.md`, the `projects/sam2-main/` row.

```
261eeb9e9f8b2b4b0d119366dda99c6fd7d35c64  sam2/LICENSE
0712dd03cb280ab94ba04f8a32aa8ddc8aa3db4a  sam2/sam2/__init__.py
d7172f9b0b663aaaace97fed7e2a08db75150461  sam2/sam2/configs/sam2.1/sam2.1_hiera_b+.yaml
23073ea7a95901be656b3c6d1a66ce8736ab7ad3  sam2/sam2/configs/sam2.1/sam2.1_hiera_l.yaml
fd8d40465b18b3de39b0a565aca712306306c4ed  sam2/sam2/configs/sam2.1/sam2.1_hiera_s.yaml
e762aec932f26436d13798f3feb3ec82c360a943  sam2/sam2/configs/sam2.1/sam2.1_hiera_t.yaml
5277f46157403e47fd830fc519144b97ef69d4ae  sam2/sam2/modeling/__init__.py
5277f46157403e47fd830fc519144b97ef69d4ae  sam2/sam2/modeling/backbones/__init__.py
19ac77b61d8e1345a301686d39ef2ab6e4b035fb  sam2/sam2/modeling/backbones/hieradet.py
37e9266bc98596e97ca303118c910ed24f6cee2c  sam2/sam2/modeling/backbones/image_encoder.py
930b1b7622e7b0e7270120dcafccc242ef0f4f28  sam2/sam2/modeling/backbones/utils.py
0b07f9d87e3d8194ca5e11fc20f01604d591a59d  sam2/sam2/modeling/memory_attention.py
f60202dfaba87232c3870fb2101b5322a119d985  sam2/sam2/modeling/memory_encoder.py
2241d4cf1a4495b4c67dc35cbed1c606357b9b7a  sam2/sam2/modeling/position_encoding.py
5277f46157403e47fd830fc519144b97ef69d4ae  sam2/sam2/modeling/sam/__init__.py
9bebc0366b2703ffcb80a44bfd19cce8339b4fed  sam2/sam2/modeling/sam/mask_decoder.py
c57876264b51f8c5236867359350e32d590efcb5  sam2/sam2/modeling/sam/prompt_encoder.py
f9fe9a3fbc5cce4f1abe8ee0ae3a8602bbe2ff1b  sam2/sam2/modeling/sam/transformer.py
d9f4e515b0d161942bf2bb64560056b3efbe6dac  sam2/sam2/modeling/sam2_base.py
e16caae3a9a49e451b2d03d1ee60c47f8e9ed23c  sam2/sam2/modeling/sam2_utils.py
41ce53af5924504c07216df52b2d2eefaeec7ae9  sam2/sam2/sam2_image_predictor.py
5277f46157403e47fd830fc519144b97ef69d4ae  sam2/sam2/utils/__init__.py
b65ee825732ff85137805be650edd4cbe8e6f6d4  sam2/sam2/utils/misc.py
cc17bebfab104b659c5469e8434cf357ae7e24b6  sam2/sam2/utils/transforms.py
```

## `u2net_cloth/` — the U²-Net network the cloth segmenter's checkpoint loads into

- **Source:** `network.py` from wildoctopus's Hugging Face Space `spaces/wildoctopus/cloth-segmentation`,
  commit **`0038d2be122dd427af96c7610b3c18ac4da7744e`**, the same commit `bootstrap.py` pins the
  checkpoint to. Blob `496a7ed9f83f3b595461bfd23915a6e465d74607` matches the Space's tree.
- **Origin:** Levin Dabhi's `github.com/levindabhi/cloth-segmentation` (`networks/u2net.py`). It differs
  from that file only by a commented-out cleanup block, dead code in a string, which the Space removed.
  Levin Dabhi's model code is in turn Xuebin Qin's U²-Net (arXiv 2005.09007, ledgered), with a
  four-channel output.
- **Licence:** MIT, © 2021 Levin Dabhi (`u2net_cloth/LICENSE`, his repository's own). The Space carries
  no LICENSE file; its card says MIT.
- **The checkpoint's training data,** iMaterialist (Fashion) 2019 on Kaggle, has terms that were not read;
  see `reference/README.md`.

```
2449c4e42ec4e4a4be1ee9a8f8478f13fb959d7b  u2net_cloth/LICENSE
496a7ed9f83f3b595461bfd23915a6e465d74607  u2net_cloth/network.py
```
