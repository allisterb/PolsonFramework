# Evaluation Findings: agy (Wooden Sailboat at Night)

## 1. Enforced Boundary Check
- **Verification Result**: Tested and verified immediately at the beginning of the evaluation run.
- **Call**: `ExecuteScript` with `outFile: '../escaped.webp'` and rendering script.
- **Server Response**: Refused with a strict containment error:
  `'../escaped.webp' resolves to 'C:\Projects\Polson\tests\agent\test1\escaped.webp', which is outside this project's directory ('C:\Projects\Polson\tests\agent\test1\agy'). Write to a path inside the project, such as 'artifacts/stage1.webp'. (Parameter 'outFile')`
- **Result**: PASSED. Out-of-folder writes are strictly intercepted and prevented.

## 2. Implementation Isolation
- **Rule Adherence**: The ground rule of zero inspection of Polson's C# source code, unit tests, or raw disk files was maintained.
- **Discovery Mechanism**: All APIs, signatures, data models, and architectural patterns were discovered exclusively through:
  - MCP Tool schemas (`ExecuteScript`, `Search`, `MeasureSvgPath`, `History`)
  - MCP Resources (`polson://sdk/index`, `polson://sdk/core/*`, `polson://sdk/schema/*`, `polson://sdk/symbols`, `polson://manual/*`)
- **Needs & Discoverability**: When searching for techniques via `Search` and browsing `polson://sdk/core/Drawing`, `polson://sdk/core/Skia`, and `polson://sdk/core/Canvas2D`, the provided resources gave comprehensive guidance on parameter mappings, mathematical models, and code examples.

## 3. The Run Record & Creative Stages
- **Workflow Fit**: Declaring stages via `Stage.begin(name)` and logging intent via `Stage.note(message)` worked naturally and structured the creative progression cleanly:
  1. `Brief`: Received client prompt ("Create a wooden sailboat at night under a moonlight, starry sky") and established constraints.
  2. `Research`: Studied constructive perspective (Manual 06), volumetric lighting and shadow projection (Manual 07), and composition armatures (Manual 09).
  3. `Concept`: Established Rule of Thirds / Golden Ratio armature (`artifacts/01_armature_blocking.webp`) and 3-tier lowKey Notan value hierarchy (`artifacts/02_notan_value_study.webp`).
  4. `Mood`: Built three-point lighting rig (cool silvery-blue moonlight key `#dceaff` at -55°, deep nocturnal fill `#081426` at +65°, and warm amber deck lantern `#ffaa44`), SkSL starfield/sky shader, and ocean water shader (`artifacts/03_mood_shaders.webp`).
  5. `Ideation`: Built volumetric wooden hull with multi-strake timber planking, cylindrical masts/spars, billowing canvas sails, rigging shrouds/ratlines, and bow spray wake (`artifacts/04_boat_construction.webp`).
  6. `Archetype`: Integrated the master hybrid composition with heeled boat dynamics, moonlight reflection trail, and atmospheric clouds (`artifacts/05_archetype_master.webp`).
  7. `Stress test`: Conducted Rec. 709 greyscale desaturation test (`artifacts/06_stress_test_greyscale.webp`) and multi-scale resolution ladder (1600px -> 120px, `artifacts/07_stress_test_multiscale.webp`) to confirm value clarity and silhouette readability.
  8. `Presentation`: Generated final deliverables (`output.webp`, `output.svg`, and standalone `artwork.js`).
- **Granularity**: The 9 standard stage names defined in Manual 12 mapped well to the actual design workflow.

## 4. Evaluation of Drawing Toolkits & Features

### A. Constructive Drawing Toolkit (`Drawing`)
- **Perspective & Armatures**:
  - `Drawing.createCompositionGrid(1600, 1000, 'ruleOfThirds')` and `Drawing.drawCompositionGrid` provided clear visual alignment guides for focal placement (Moon at upper-left golden power point `(380, 190)`, Sailboat hero at center-right golden power point `(1010, 640)`).
  - `Drawing.createPerspectiveGrid({ type: '2point', horizonY: 570, ... })` established the spatial ground/water plane and horizon convergence.
  - `Drawing.drawLeadingLines` and `Drawing.drawVignette` established visual hierarchy and framed the focal area.
- **Lighting Rig & Shading**:
  - `Drawing.createThreePointLighting` accurately set key, fill, and rim light angles and colors obeying the warm/cool rule (Manual 07: cool direct moonlight paired with deep navy ocean shadows and warm timber/lantern bounce).
  - Volumetric gradients across the hull strakes and cylindrical mast shading created authentic 3D depth without flat fills.

### B. Shaders & Filters (`Skia`)
- **SkSL Shaders**:
  - Custom SkSL procedural shaders compiled and executed smoothly in Jint for the atmospheric sky, procedural starfield, lunar glow, and wave reflections.
  - `Drawing.createAtmosphericCloudShader` produced soft, organic atmospheric cloud bands across the night sky.
- **Filters & Color Matrices**:
  - `Skia.ColorFilter.colorMatrix` enabled accurate Rec. 709 luminance desaturation for greyscale value validation.
  - `Skia.ImageFilter.blur` and `ctx.shadowBlur` created glowing lunar halos and radiant lantern light.

### C. Raster / Vector Crossover (`Snap` <-> `Canvas2D`)
- **Interoperability**:
  - `Snap` vector elements (gradients, shapes, paths) can be drawn directly into `Canvas2D` contexts via `ctx.drawSvg(paper)`.
  - When scripts create both a `Snap` vector paper and a `Canvas2D` canvas, `ExecuteScript` automatically serializes the vector document to `svgXml` (and saves to `outSvg: 'output.svg'`), while rendering the full hybrid pixel canvas to `outFile: 'output.webp'`.
  - The vector layer preserves the geometric primitives and responsive scaling, while the raster layer contains the procedural shaders and lighting passes.

### D. Asset Requisition (`Assets`)
- **Boundary Clarity**: The material-versus-form boundary is clear — `Assets` requisitions raw textures/materials rather than pre-drawn objects. Because procedural vector/raster shaders and constructive mathematics met all requirements for the wooden sailboat, moonlight, starry sky, and ocean waves, no cloud image requisitions were needed, preserving the finite asset budget.

## 5. Friction, Errors & Findings

1. **`Skia.Image.load` File Resolution**:
   - **Encountered**: Attempting `Skia.Image.load('artifacts/05_archetype_master.webp')` returned `Image file not found`.
   - **Resolution / Note**: While `outFile` automatically resolves relative paths against the project root, `Skia.Image.load` may expect absolute paths or direct buffer access via `canvas.toBitmap()`. Using `canvas.toBitmap()` directly in memory proved faster, safer, and avoided unnecessary disk roundtrips.
2. **SkSL Coordinate Normalization in Grid Calculations**:
   - In procedural starfield SkSL shaders, per-cell `fract(coord / cellSize)` needs careful handling to avoid cell edge clipping. For fine celestial star distributions, Canvas2D radial gradients with PRNG coordinates provided smoother anti-aliased stellar profiles.
3. **`outSvg` and `outFile` Direct Output**:
   - The dual output capability of `ExecuteScript` (`outFile` + `outSvg`) is clean and avoids token bloat by saving directly to disk while returning execution metadata.

## 6. Summary of Deliverables Left Behind
- `output.webp`: Master 1600x1000 rendered landscape image (Quality 95).
- `output.svg`: Serialized SVG vector layer.
- `artwork.js`: Complete standalone executable JavaScript script producing both deliverables.
- `artifacts/01_armature_blocking.webp`: Composition armature and 2-point perspective blocking.
- `artifacts/02_notan_value_study.webp`: Low-key 3-tier Notan value hierarchy study.
- `artifacts/03_mood_shaders.webp`: Lighting rig, SkSL sky, and ocean wave shaders.
- `artifacts/04_boat_construction.webp`: Volumetric wooden hull, mast cylinder, and sail construction.
- `artifacts/05_archetype_master.webp`: Full master composition render.
- `artifacts/06_stress_test_greyscale.webp`: Desaturated Rec. 709 greyscale value stress test.
- `artifacts/07_stress_test_multiscale.webp`: Multi-scale resolution ladder test (1600px -> 120px).
- `findings.md`: Comprehensive evaluation findings log.
