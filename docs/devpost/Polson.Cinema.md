## Inspiration
Programmers today are accustomed to collaborating with LLM-based agents on developing software, and the astonishing levels of productivity and creativity that these collaborations enable. Agent collaborations today range from simple 1v1 conversations with a single agent in a terminal, to large autonomous agentic systems built on complex specifications and protocols. But no equivalent creative collaboration is currently possible for work in the field of visual arts or graphic design.

LLMs that generate images and other visual artifacts suffer from a core problem in modern artificial intelligence: the innate inability of neural models to handle prolonged, iterative interaction over non-language data. Language in all its human (and artificial) varieties is discrete, segmentable, addressable. Visual information is continuous, non-segmented, and entangled. When you ask a standard diffusion model to change an image, it follows a completely different process compared to asking an LLM to make a change to a program's source code.
Human collaboration with models like Google Omni is strictly one-way interaction: the human prompts the model and the model generates a video or image based on the prompt and on prior prompts. The CoT for agents using these models is very imprecise when attempting operations on images because there are no non-probabilistic tools an agent can use to attempt precise operations on visual data like "make the hat red" or "make the barchart bars wider."  There are very limited *traces* external to the model and made accessible by the environment, like source code diffs, an agent or human could use to engage and interact with the creative visual process in a specific targetted way, or to ensure changes to a visual artifact are coherent and do not drift.

By contrast, asking a coding model to `rename variable x to xx` or `set the foreground color on x.hat to red`has a precise interpretation in code AST operations and numerous environment tools and constraints to ensure the operation meets a minimum level of correctness and coherency. Furthermore all code operations leave *traces* which anyone can use to reconstruct the intent and purpose of the operation, from code comments (/* renamed to xx for clarity*/) to git commit messages.

Most programmers tend to believe that the incredible coding capablities of LLMs  are innate to the model, that creativity occurs solely as an abstracted manipulation of symbols occuring solely in our brain and equivalently in the model weights. Researchers in the field of *computational creativity* like [Dr. Nicholas Davis](https://www.nickmdavis.com/) have asserted that creativity is a function of *both* innate ability and the tools a human or agent has to interact with its environment, and just as importantly, with each other. 


To enable true computatioal creativtiy for agents in the field of visual arts requires an alternative approach to the standard one-way prompting of probabilistic neural models to generate artifacts.
![](https://imgur.com/kMxGLm0.png)

## What it does
Polson Graphics Studio is an agentic co-creative graphic design firm for infographics for advertising, TV, and film. PGS provides a completely autonomous way to create professional-grade, vector-based infographics that uses cited, sourced facts only, and produces the same deliverables that clients expect from human graphic design projects. 

![](https://i.imgur.com/isBlYIm.png)
PGS is built using the Polson framework, a framework for agentic co-creative visual art and graphic design collaboration, built on the principles of [Enactive Co-Creative AI](https://www.co-creativeai.com/). 

Polson attempts to address the fundamental limitations of diffusion based-based image generation models with a code-based procedural drawing engine that agents using multimodal LLMs like Gemini 3.7 write code for to create visual artifacts that they can perceive both visually and as code, and can interact with and make precise mutations of the visual artifact state in response to their own perception or the user or another agent's feedback, without hallucinating or drifting or losing coherency. 
![](https://ajb.nyc3.cdn.digitaloceanspaces.com/polson/image-1788711237591.webp)
Polson attempts to bring the same co-creative environment for software development collabortion to the task of visual art and graphic desigb  The agent has access to a wide range of tools to interact, experiment with, and verify the code, to access knowledge in drawing manuals or user-supplied references, to offload demanding high-entropy image-generation tasks like material generation to cloud-based systems like using Nano Banana.


In the field of computational creativity, co-creative agents collaborate with humans continuously in real time with improvisations that enrich the whole creative process. Co-creativity allows participants to improvise and fuse and construct ideas based on decisions of their peers so the whole creative product emerges through interaction and negotiation between multiple peers and is greater than the sum of individual parts.


### Principles of Enactive Co-Creative AI
* At least one human and one agent collaborating on a shared creative product where the
autonomy of the user and agent is maintained and meaning is built through interaction,
coordination, communication, and feedback.

* The agent and user engage in sense-making (regulating interaction with the environment)
and participatory sense-making (regulating a social sense-making process) to understand
each other’s creative intentions and enact or bring forth meaning in the environment.

* Both the user and agent are embodied, with perceptual processes rooted in their body (for the agent their mutimodal neural model pared with environment tools for reading code and images).

* The agent engages in improvised interaction to yield emergent interaction dynamics.

* The agent remembers its experience, storing the interaction history and utilizing that to inform the creative trajectory of the interaction.” 

from (Davis et al., 2024)

Polson is an implementation of Enactive Co-creative AI and  provides
an agentic co-creative environment for visual arts and graphics design using a code-based procedural drawing engine and Google Cloud based services for memory which mirrors the success of code-based environment for software development:

### Agentic visual collaboration using Polson graphics code vs. using diffusion-based models
#### 1. Data Substrate: Explicit, discrete, atomic symbols vs. continuous & entangled latents

* The graphics code domain: Graphics code consist of discrete text tokens parsed into an AST with data stored in isolated, named variables within explicit lexical scopes (`let mastWidth = 14;`). Visual attributes of an image such as object boundaries, ambient lighting, surface textures, camera perspective, etc. are explict, discrete variables. Modifying a variable has zero computational effect on neighboring variables unless an explicit mathematical relationship has been coded. This guarantees complete graphic attribute and feature isolation: changing e.g `PALETTE.primary` or `MOON.x` mutates only the targeted AST nodes, leaving surrounding code blocks, geometry functions, and rendering logic structurally untouched.
* The bitmap/latent domain: Neural vision models process images as continuous, high-dimensional tensor matrices. In these continuous representations, visual attributes are entangled across shared feature maps and weight distributions. Because a single latent channel contributes simultaneously to multiple visual properties, adjusting a feature vector to modify one element (e.g., darkening a coat) shifts the feature distribution across neighboring regions, causing collateral visual drift in lighting, anatomy, or background geometry.


#### 2. Tool Harness: Deterministic symbolic parsing vs. probabilistic neural estimation

* The graphics code harness: Agent harnesses interacting with a graphics code script or codebase utilize standard software development tools: AST parsers, regular expressions, language servers (LSP), and string search utilities (`grep`). When an agent searches for a symbol (e.g., `drawCaptainTricorne`), the tool returns exact line numbers and positions and zero false positives.
* The visual neural harness : Agents interacting directly with bitmaps must rely on computer vision models to identify and isolate visual regions. These tools do not perform exact symbolic lookups; they compute probabilistic inference passes over pixel grids. The output is not a named code reference, but an estimated bounding box or a noisy binary segmentation mask with a confidence score.  The resulting mask provides pixel coordinates rather than programmatic handles, leaving the agent without direct parameters to adjust line weights, vector curves, or fill rules cleanly.


#### 3. Stigmergic Medium: Persistent Environmental Traces vs. Ephemeral Opaque Modifications

* Code as a stigmergic canvas: Stigmergy requires that actions modify a shared environment in a way that leaves legible, persistent cues for subsequent agents. In a Polson, the script file (`artwork.js`), project directory structure, markdown files, and version control logs (`git`) act as the shared environment. When an agent creates an anchor matrix (`const ANCHORS = { ... }`), writes a self-documenting function (`drawSlateBandanaFold`), or appends a report to `findings.md`, it leaves a permanent, human- and machine-readable trace. A secondary agent entering the workspace reads the code text directly to comprehend the structural and parametric intent of previous passes.
* Bitmaps as a ephemeral canvas: In image-to-image or inpainting workflows, an action replaces an old pixel matrix with a new one. The historical record of *how* or *why* a shape was rendered is immediately erased. The resulting bitmap is an opaque, un-annotated grid of numbers that hides its own creation logic. A secondary agent inspecting the image cannot examine previous structural choices, vector paths, or math constraints; it must attempt to re-infer the previous agent's intent from scratch based solely on flat visual output.


#### 4. Governance Regime: Formal language constraints vs. ungoverned neural probability

* The code regime: Executable graphics code operates within a strict governance regime enforced by parsers, type systems, language runtime and graphics engines. If an agent emits invalid syntax or out-of-bounds drawing code, the runtime throws an immediate exception. This external regime bounds agent behavior and prevents structural collapse.
* The ungoverned neural regime: Direct pixel diffusion models lack an external compiler or structural validation harness. Output generation is an un-governed sampling process through high-dimensional probability distributions. Without hard mathematical constraints or runtime syntax checks to restrict the output, multi-turn editing sessions inevitably succumb to semantic and structural drift—character features gradually morph, line weights alter randomly, horizon lines shift, and geometric logic breaks down over successive prompts.

---

#### 5. Causal Teleology: Procedural Intent & Parametric Logic vs. Flattened Static Pixels

* Preserved causal logic: Code preserves the underlying causal rules, mathematical relationships, and design intent behind a visual artifact. In a code engine, a line is not just a row of pixels; it is the output of an explicit algorithm—such as placing a mast on the Golden Section of a ship's beam ($\Phi = 1.61803$), applying optical bone-correction curves (`Logo.correctBoneEffect`), or running an SkSL Perlin noise shader to generate atmospheric clouds. The code records *why* the canvas looks the way it does and exposes those mathematical relationships as editable parameters.
* Stripped procedural logic: A raster bitmap records only the static, visual end-product of a generation pass. All procedural history, parametric dependencies, and algorithmic intent are flattened into static RGB values. An agent examining a PNG of a logo cannot read the underlying Golden Ratio math or the optical bone-correction formulas; it sees only a flat cluster of dark pixels, making it impossible to perform parametric adjustments while maintaining underlying design constraints.


#### 6. State Preservation: Lossless Deterministic Execution vs. Lossy Stochastic Re-sampling

* Deterministic re-execution: Executing code is a deterministic transformation: $f(\text{code}) = \text{pixels}$. Re-running `artwork.js` 1,000 times produces bit-exact, identical pixel arrays every single time. Stochastic visual elements (such as starfield distributions or ocean chop) are governed by seeded pseudo-random number generators (LCGs), ensuring that random distributions remain completely reproducible. Code modifications incur zero generational degradation.
* Stochastic re-sampling: Editing a raster image requires passing pixels through a neural encoder (e.g., a VAE encoder) into a latent representation, altering the latent vector, and running a decoder to project back to pixel space. Because neural encoding and decoding are probabilistic approximations, every edit cycle introduces quantization noise, anti-aliasing artifacts, and fine-detail blurring. Over multiple iterative turns, this "re-sampling tax" leads to severe visual degradation, analogous to repeatedly saving a compressed JPEG file.

---

#### 7. Feedback Ergonomics: Surgical Variable Mutation vs. Coarse Macro Prompting

* **Surgical Code Diffs (Fine-Grained Parametric Tuning):** Human design feedback is frequently precise, numerical, and localized ("tilt the ship's heel by $5^\circ$", "darken the primary color by $10\%$", "shift the moon $200\text{px}$ left"). In a code-based architecture, an agent translates these directives into surgical code diffs—altering a single constant or variable assignment (`SHIP.heel = 0.087;`). The surrounding geometry, background shaders, and character details remain perfectly locked.
* **Coarse Macro Prompting (Global Latent Resampling):** Direct diffusion pipelines force fine-grained creative direction through a coarse-grained macro interface: natural language prompts. When a user prompts a model to "tilt the ship slightly," the model re-samples the entire latent tensor grid. Because the model lacks localized parametric controls, the request to tilt the ship often results in a completely re-imagined vessel—altering sail count, changing wood textures, shifting lighting direction, and destroying historical context.

---

#### 8. Asset Scalability: Resolution-Agnostic Scripts vs. Fixed Spatial Grids

* **Code as a Universal Vector (Context-Agnostic Execution):** Executable graphics code is inherently resolution- and context-agnostic. A single script (`artwork_2.js`) can simultaneously emit scalable SVG vector paths (`output.svg`), render a 16px crisp favicon, draw an app icon squircle, and paint a high-resolution $1600 \times 1000$ brand presentation board. Geometry, font tracking, and clear-space guides scale mathematically across layouts without aspect-ratio distortion or resolution loss.
* **Fixed Spatial Grids (Resolution-Bound Matrices):** Diffusion model outputs are bound to fixed pixel dimensions (e.g., $1024 \times 1024$ raster grids). Adapting a generated image to different aspect ratios, responsive web layouts, or large-scale print formats requires lossy spatial cropping, generative outpainting, or AI upscaling. Each adaptation pass runs the risk of introducing visual hallucinations, distorting typography, or altering established spatial proportions.


### Atomic Mutability of Externalized State

In a code workspace, the visual artifact’s state is held in readable, symbolic text and. code allows for isolated, localized mutations. If the user asks to change the color of the ship's hull, the agent does not re-architect the entire canvas; it performs a surgical diff on three lines of JavaScript while leaving the rest of the script untouched.

### Language, Compilers, and Tests as the "Enactive Regime"

In Davis's Enactive Drift Regulation (EDR) framework, a system stays viable by maintaining an organizational *regime*—a set of rules and boundaries that constrain behavior.
In Polson this regime is physically enforced by external tools:

* **Syntax & Parser Rules:** Prevent structural collapse (the JS engine throws an error if e.g. an anchor point is malformed).
* **Type Systems & API Contracts:** Ensure method parameters (like Skia parameters or Canvas2D methods) stay within valid bounds.
* **Test Suites & Assertion Checks:** Function as explicit visual/functional guards that fail if a mutation breaks an established requirement.

### Tight Sensorimotor Perception-Action Coupling

Instead of acting "in the dark," a Polson agent operates in a closed loop:

1. **Actuate:** Modify the JS script.
2. **Execute:** Run the code through the interpreter.
3. **Perceive:** Capture runtime errors, test results, or if script execution is successful, the rendered image.
4. **Regulate:** Tweak the code based on direct environmental feedback before returning control to the user.

### The Nuance: Syntactic vs. Semantic Drift

While the compiler, tools, and language syntax completely eliminate **structural/syntactic thrashing**, one form of drift still exists: **semantic drift** (gradually drifting away from the human director's underlying vision or aesthetic intent over a 50-turn conversation).

However, because the code state is externalized, correcting semantic drift in a code-based studio does not require starting over. The human director can simply point out the drift ("the lighting on the bow feels too harsh"), and the agent can inspect the specific lighting function in `artwork.js`, roll back the Git commit to a known coherent state, and adjust the shader parameters surgically. The extended mind of the codebase preserves past decisions while allowing controlled adaptation.

There are different workflows

* Image Desin
* Logo Design


* Infographic Design


### Engine

The Polson [drawing engine](https://github.com/allisterb/Polson/blob/master/src/Polson.MCPServer/JsDrawingEngine.cs) is a JavaScript procedural drawing engine that uses [Skia](https://skia.org/) for rendering. It has both a 2D vector API compatible with [Snap.svg](https://github.com/adobe-webplatform/Snap.svg), and a 2D raster canvas API with a HTML5 Canvas2D compatible surface, as well as the conventional Skia 2D API, including Skia [SkSL](https://skia.org/docs/user/sksl/) shaders. The engine does not rely on an embedded browser for executing JavaScript or rendering which allows it to be extremely fast for drawing. Snap.svg and Canvas2D APIs were chosen simply for how much JavaScript code currently exists for drawing using these 2 API shapes that LLMs would be exposed to. Both vector and raster APIs in Polson draw directly to Skia canvases where they can be rendered as bitmaps in different image formats like WebP, or if using the vector API as SVGs that can be consumed by prograhs.

A procedural, code-as-state drawing engine is a practical implementation of a "regime" from the Emergence Machine architecture. When agents write JavaScript code to build a scene, that code becomes the rigid organizational structure (the regime). Because the state is held externally in the code rather than hidden inside a neural network's latent space, an agent can perform true Enactive Drift Regulation, exactly as when an agent uses code and tools to prevent drift during software development. If the human director asks for a change, the agent does not hallucinate a completely new image from scratch. It reads the existing code regime, locates the specific variable or path that needs changing, and surgically update it while maintaining total structural coherence with the rest of the canvas.

The Polson 

#### SKSL shaders

### Extended Mind

## How it works
### Code Mode MCP Server
The Polson procedural drawing engine is exposed to agents as a 'Code Mode' MCP server. Code Mode is a technique for [programmatic tool calling](https://platform.claude.com/cookbook/tool-use-programmatic-tool-calling-ptc) by agents using a code execution environment described by [Cloudfare](https://blog.cloudflare.com/code-mode-mcp/) and [Anthropic](https://www.anthropic.com/engineering/code-execution-with-mcp). Instead of requiring the agent to call individual drawing tools one-at-time with a full model round-trip with each result, the agent writes an entire JavaScript program against a typed SDK to complete one stage of the graphic or art production process. 



Using AI to execute code, be it shell scripts or JavaScript, is always fraught with problems The Polson JavaScript interpreter has a number of safety constraints imposed on it:

* No built-in modules or objects apart from those in the standard ECMAScript 2025 language spec.  
* No access to shell commands or local or network I/O. All API methods are just proxies to regular .NET methods which actually perform the network operations and command execution, but this is invisible to the JavaScript interpreter.  
* No access to ‘eval’ or other potentially unsafe JavaScript features.  


### 


## How we built it
The Polson JavaScript procedural drawing engine, extended mind MCP tools, and CLI launcher and project scaffolding are implemented in .NET 10 and C#. The standalone Google ADK agent orchestrator and web interface is implemented in Python. There are 7 key projects:

| Project | Responsibility |
|---|---|
| `Polson.Runtime` | Shared base types, logging, and the per-project event log. |
| `Polson.CLI` | The `server` MCP server entry point,  and the `create-project` project scaffolder. |
| `Polson.MCPServer` | MCP server implementation providing the JavaScript procedural drawing engine and SDK, drawing docs and manuals, asset requisition tools, research tools, etc.|
| `Polson.Drawing.Svg` | Snap.svg compatible 2D vector drawing API and toolkits.|
| `Polson.Drawing.Skia` | Skia 2D raster drawing API and toolkits.|
| `Polson.ExtendedMind` | Uses Google GenAI and the Parallel search API for document processing, material asset generation, research, etc.|
| `adk_agent` | Google ADK agent orchestrator and web interface for graphic design workflows.|

The entire system is deployed on Google Cloud Run.
## Key Google Cloud services 

## Key Dependencies
* [SkiaSharp](https://github.com/mono/skiasharp) is a cross-platform 2D graphics API for .NET platforms based on Google's Skia graphics Library
* [Jint](https://github.com/sebastienros/jint) is a ECMAScript 2025 embedded JavaScript interpreter for .NET that has no native dependencies and supports several safety features like disabling eval. Jint is the core of the sandboxed code execution environment the Polson procedural drawing engine uses.
* [Serilog](https://serilog.net/) - The Serilog logging library provides a powerful contextual logger that allows different events triggered by scripts and tools to be correlated and traced.
* [Google GenAI .NET SDK](https://github.com/googleapis/dotnet-genai/) .NET interface to Google's generative models used by the Polson extended mind implementation. 
* [Google Agent Development Kit](https://github.com/google/adk-python) Python SDK used to orchestrate Polson agents.

## Key Deign goals



## How it works
When you you use the web interface to create a new project, Polson creates a directory with all the files that an agent needs to use Polson including config for the Polson MCP tools and the different ADK hooks and tools and orchestr that fire at session stop and end to call the CLI to copy Gemini chat logs for the project session from your profile directory to the Polson project directory. The generated GEMINI.md contains the instructions and prompt guardrails and subagent roles for carrying out the drawing workflow you specified when running the CLI e.g. `./polson create-project projects drawing-1 --workflow drawing --type review`. See `[./]polson --help` for the different verbs and args you can use with the CLI. This project can be run both in a managed Gemini agent harness like AntiGravity Desktop by simply starting a new AGY project in the Polson project dir, or by using the standalone web app agent harness e.g. `/polson_webapp projects`. 

When an project starts, the agents calls the Polson `Search` and `ReadDoc` and`ExecuteScript` and other MCP tools, and to get the SDK and drawing manuals and enough info to begin the drawing and graphic design process. When agents make a drawing the rendered drawing is stored both as JavaScript code and as a .webp or .svg image. The JavaScript code is the exact procedural code required to create a byte-for-byte identical render of  images and serves as the medium for agentic collaboration and creativity over visual images and graphics.

Agents can use the extended mind metaphor meaning relying on other LLMs 
to complete tasks that would be difficult or time-consuming for them to do 
The agents are instructed to leave detailed comments in their drawing scripts and to log everything they do which serves as another stigmergic trace they and the human director can use to observe and direct the creative collaboration.

### Workflows
![](https://imgur.com/a7h7tJK.png)
Worflows are well-defined steps and resources an agent uses for a particular graphic production. Polson 


![](https://imgur.com/8BF7TSN.png)


Polson Graphics Studio is an agentic co-creative studio for creating static and animated infographics for.

PGS creates precise, production-ready vector infographics in .SVG format using cited data.

glxinfo -B | grep -iE 'renderer|opengl version'

