## Inspiration
Programmers today are accustomed to collaborating with LLM-based agents on developing software, and the astonishing levels of productivity and creativity that these collaborations enable. Agent collaborations today range from simple 1v1 conversations with a single agent in a terminal, to large autonomous agentic systems built on complex specifications and protocols. But no equivalent creative collaboration is currently possible for work in the field of visual arts or graphic design.

LLMs that generate images and other visual artifacts suffer from a core problem in modern artificial intelligence: the innate inability of neural models to handle prolonged, iterative interaction over non-language data. Language in all its human (and artificial) varieties is discrete, segmentable, addressable. Visual information is continuous, non-segmented, and entangled. When you ask a standard diffusion model to change an image, it follows a completely different process compared to asking an LLM to make a change to a program's source code.
Human collaboration with models like Google Omni is strictly one-way interaction: the human prompts the model and the model generates a video or image based on the prompt and on prior prompts. The CoT for agents using these models is very imprecise when attempting operations on images because there are no non-probabilistic tools an agent can use to attempt precise operations on visual data like "make the hat red" or "make the barchart bars wider."  There are very limited *traces* external to the model and made accessible by the environment, like source code diffs, an agent or human could use to engage and interact with the creative visual process in a specific targetted way, or to ensure changes to a visual artifact are coherent and do not drift.

By contrast, asking a coding model to `rename variable x to xx` or `set the foreground color on x.hat to red`has a precise interpretation in code AST operations and numerous environment tools and constraints to ensure the operation meets a minimum level of correctness and coherency. Furthermore all code operations leave *traces* which anyone can use to reconstruct the intent and purpose of the operation, from code comments (/* renamed to xx for clarity*/) to git commit messages.

Most programmers tend to believe that the incredible coding capablities of LLMs  are innate to the model, that creativity occurs solely as an abstracted manipulation of symbols occuring solely in our brain and equivalently in the model weights. Researchers in the field of *computational creativity* like [Dr. Nicholas Davis](https://www.nickmdavis.com/) have asserted that creativity is a function of *both* innate ability and the tools a human or agent has to interact with its environment, and just as importantly, with each other. 


To enable true computational creativtiy for agents in the visual arts fields requires an alternative approach to the standard one-way prompting of probabilistic neural models to generate visual artifacts.
![](https://ajb.nyc3.cdn.digitaloceanspaces.com/polson/storyboad-annot.png)

## What it does
Polson Graphics Studio is an agentic co-creative graphic design firm for drawing and infographics for advertising, TV, and film. PGS provides a completely autonomous way to create raster drawings like storyboards, and professional-grade, vector-based infographics that uses cited, sourced facts only, and produces the same deliverables that clients expect from human art and graphic design projects. 

![](https://i.imgur.com/isBlYIm.png)
![](https://ajb.nyc3.cdn.digitaloceanspaces.com/polson/blocking2.webp)
![](https://ajb.nyc3.cdn.digitaloceanspaces.com/polson/004_horror_shadows.webp)
Polson Graphics Studio is built using the Polson framework, a framework for agentic co-creative visual art and graphic design collaboration, built on the principles of [Enactive Co-Creative AI](https://www.co-creativeai.com/).  The agentic orchestration and collaboration for the Polson Graphics Studio web application is built using Google ADK:
![](https://ajb.nyc3.cdn.digitaloceanspaces.com/polson/shootaday1.png)
Polson attempts to address the fundamental limitations of diffusion based-based image generation models with a code-based procedural drawing engine that agents using multimodal LLMs like Gemini 3.7 write code for to create visual artifacts that they can perceive both visually and as code, and can interact with and make precise mutations of the visual artifact state in response to their own perception or the user or another agent's feedback, without hallucinating or drifting or losing coherency. 
![](https://ajb.nyc3.cdn.digitaloceanspaces.com/polson/genres.svg)
![](https://ajb.nyc3.cdn.digitaloceanspaces.com/polson/final.svg)


Polson attempts to bring the same co-creative enactive environment for agentic software development collaboration we have today to the task of visual art and graphic design. By creating a dual representation of visual media as both code and image, an agent using a multimodal model like Gemini 3.7 Flash has access to a wide range of tools to verify, interact, and experiment with the code and immediately observe the results of changes, e.g. this is part of the JS code for the above Project Apollo infographic using the Polson drawing engine: 
```js
// Historical Telemetry Notes Box
const noteBoxY = phaseY + 72;
trajG.rect(tlX, noteBoxY, tlW, 76).attr({
  fill: '#051833',
  stroke: C_GRID_MAJOR,
  'stroke-width': 1
});

trajG.text(tlX + 10, noteBoxY + 16, 'HISTORICAL TELEMETRY NOTES:').attr({
  'font-family': 'monospace',
  'font-size': 8.5,
  'font-weight': 'bold',
  fill: C_AMBER
});
trajG.text(tlX + 10, noteBoxY + 30, '• 1202/1201 RADAR OVERFLOWS: Rendezvous radar duty cycle flooded AGC CPU at T+300s & T+540s; core guidance held.').attr({
  'font-family': 'monospace',
  'font-size': 7.5,
  fill: C_WHITE
});
trajG.text(tlX + 10, noteBoxY + 44, '• BOULDER FIELD REDESIGNATION: Armstrong took manual P66 control @ 500 ft to clear West Crater boulder field.').attr({
  'font-family': 'monospace',
  'font-size': 7.5,
  fill: C_WHITE
});
trajG.text(tlX + 10, noteBoxY + 58, '• RESIDUAL FUEL MARGIN: Low-level sensor (5.6%) tripped @ T+682s. Touchdown achieved with ~45s (304 kg) margin.').attr({
  'font-family': 'monospace',
  'font-size': 7.5,
  fill: C_CYAN
});


// ----------------------------------------------------
// SECTION 3: MASS FRACTION & FUEL BUDGET (RIGHT)
// ----------------------------------------------------
drawSectionHeader(1145, 96, 410, 'SEC 03 // MASS FRACTION & FUEL BUDGET', 'METRIC & US TON');

const massG = paper.g().attr({ id: 'mass-group' });
```

In contrast to using static markup languages for graphics like SVG, the agent can also do things like audit the infographic layout using code:

```js
// ----------------------------------------------------
// SECTION 5: AUDIT CHECKS
// ----------------------------------------------------
Stage.begin('Audit');

// Check SUCCESS
const successGoalMet = (massChart.isZeroBased && Math.abs(massChart.lieFactor - 1.0) < 0.05);
Stage.check(
  'A reader can identify the 80.15% descent fuel fraction, read the 762s descent trajectory altitude profiles, and find the ~45s margin without confusion.',
  successGoalMet,
  `Zero-based: ${massChart.isZeroBased}, Lie factor: ${massChart.lieFactor.toFixed(3)}, Prop fraction: 80.15%`
);

// Check label collisions
const labels = [];
const texts = paper.selectAll('text');
for (let i = 0; i < texts.length; i++) {
  const kids = texts[i].children ? texts[i].children.filter(c => c.type === 'tspan' || c.type === 'textPath') : [];
  const parts = kids.length > 0 ? kids : [texts[i]];
  for (let j = 0; j < parts.length; j++) {
    const b = parts[j].getBBox();
    if (b && b.width > 0 && b.height > 0) {
      labels.push({ b: b, s: String(parts[j].attr('text') || '') });
    }
  }
}
```
Polson leverages for the visual arts the massive amounts of program generation and instruction data LLMs like Gemini 3.7 Flash are trained on by providing a typed SDK and constrained code execution environment for vector- and canvas-based procedural drawing and experimenting and analysis  .

In the field of computational creativity, co-creative agents collaborate with humans continuously in real time with improvisations that enrich the whole creative process. Co-creativity allows participants to improvise and fuse and construct ideas based on decisions of their peers so the whole creative product emerges through interaction and negotiation between 
multiple peers and is greater than the sum of individual parts.
![](https://ajb.nyc3.cdn.digitaloceanspaces.com/polson/share_draft.png)
![](https://ajb.nyc3.cdn.digitaloceanspaces.com/polson/interject1.png)

In the screen shots above the human director asked the agent to change the background color of the infographic and the agent responded by making a precise change to the script while also changing the infographic palette to make sure it was compatible.
![](https://ajb.nyc3.cdn.digitaloceanspaces.com/polson/03_perfected.webp)

Interaction is 2-way: Polson agents can also ask humans for direction e.g in the screenshot below:
![](https://ajb.nyc3.cdn.digitaloceanspaces.com/polson/ask5.png)

When the human responds the agent accepts direction and completes the task:
![](https://ajb.nyc3.cdn.digitaloceanspaces.com/polson/ask6.png)

Polson enables agent-human enactive co-creative collaborations across different project types like infographics and storyboarding, each using different sets of rules and deliverables.

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

(from [Davis et al., 2024](https://computationalcreativity.net/iccc24/papers/ICCC24_paper_58.pdf))

![](https://ajb.nyc3.cdn.digitaloceanspaces.com/polson/drawing_partner.png)

![](https://ajb.nyc3.cdn.digitaloceanspaces.com/polson/genres2_csm.png)


![](https://ajb.nyc3.cdn.digitaloceanspaces.com/polson/chatlog3.png)


### How Polson implements Enactive Co-Creative AI

#### Atomic Mutability of Externalized State

In a code workspace, the visual artifact’s state is held in readable, symbolic text and. code allows for isolated, localized mutations. If the user asks to change the color of a buildings wall or a graph, the agent does not re-architect the entire canvas; it performs a surgical operation on JavaScript while leaving the rest of the script untouched.

#### Language, Compilers, and Tests as the "Enactive Regime"

In Davis's Enactive Drift Regulation (EDR) framework, a system stays viable by maintaining an organizational *regime*—a set of rules and boundaries that constrain behavior.
In Polson this regime is physically enforced by external tools:

* **Syntax & Parser Rules:** Prevent structural collapse (the JS engine throws an error if e.g. an anchor point is malformed).
* **Type Systems & API Contracts:** Ensure method parameters (like Skia parameters or Canvas2D methods) stay within valid bounds.
* **Assertion Checks:** Function as explicit visual/functional guards that fail if a mutation breaks an established requirement.

#### Tight Sensorimotor Perception-Action Coupling

Instead of acting "in the dark," a Polson agent operates in a closed loop:

1. **Actuate:** Modify the JS script.
2. **Execute:** Run the code through the interpreter.
3. **Perceive:** Capture runtime errors, test results, or if script execution is successful, the rendered image.
4. **Regulate:** Tweak the code based on direct environmental and human feedback before returning control to the user.

#### Semantic Drift Regulation

While the compiler, tools, and language syntax completely eliminate **structural/syntactic thrashing**, one form of drift still exists: **semantic drift** (gradually drifting away from the human director's underlying vision or aesthetic intent over a 50-turn conversation).

However, because the code state is externalized, correcting semantic drift in a code-based studio does not require starting over. The human director can simply point out the drift, and the agent can inspect the specific lighting function in `artwork.js`, and change it
![](https://ajb.nyc3.cdn.digitaloceanspaces.com/polson/ask9.png)




## How it works
Polson organizes it work into *projects*. When you you use the web interface or CLI to create a new project, Polson creates a directory with all the files that an agent needs to use Polson including config for the Polson MCP tools and the different ADK hooks and tools and orchestr that fire at session stop and end to call the CLI to copy Gemini chat logs for the project session from your profile directory to the Polson project directory. The generated GEMINI.md contains the instructions and prompt guardrails and subagent roles for carrying out the drawing workflow you specified.  

When an project starts, the agents calls the Polson `Search` and `ReadDoc` and`ExecuteScript` and other MCP tools, and to get the SDK and drawing manuals and enough info to begin the drawing and graphic design process. When agents make a drawing the rendered drawing is stored both as JavaScript code and as a .webp or .svg image. The JavaScript code is the exact procedural code required to create a byte-for-byte identical render of  images and serves as the medium for agentic collaboration and creativity over visual images and graphics.

Agents can use the extended mind metaphor meaning relying on other LLMs 
to complete tasks that would be difficult or time-consuming for them to do 
The agents are instructed to leave detailed comments in their drawing scripts and to log everything they do which serves as another stigmergic trace they and the human director can use to observe and direct the creative collaboration.

### Engine

The Polson [drawing engine](https://github.com/allisterb/Polson/blob/master/src/Polson.MCPServer/JsDrawingEngine.cs) is a JavaScript procedural code-as-state drawing engine that uses [Skia](https://skia.org/) for rendering. It has both a 2D vector API compatible with [Snap.svg](https://github.com/adobe-webplatform/Snap.svg), and a 2D raster API with a Canvas2D compatible surface, as well as the conventional Skia 2D API, including Skia [SkSL](https://skia.org/docs/user/sksl/) shaders. The engine does not rely on an embedded browser for executing JavaScript or rendering which allows it to be extremely fast for drawing. Snap.svg and Canvas2D APIs were chosen simply for how much JavaScript code currently exists for drawing using these 2 API shapes that LLMs would be exposed to. Both vector and raster APIs in Polson draw directly to Skia canvases where they can be rendered as bitmaps in different image formats like WebP, or if using the vector API as SVGs that can be consumed by programs like Illustrator or Inkscape.

### Code Mode MCP Server
The Polson drawing engine is exposed to agents as a 'Code Mode' MCP server. Code Mode is a technique for [programmatic tool calling](https://platform.claude.com/cookbook/tool-use-programmatic-tool-calling-ptc) by agents using a code execution environment described by [Cloudfare](https://blog.cloudflare.com/code-mode-mcp/) and [Anthropic](https://www.anthropic.com/engineering/code-execution-with-mcp). Instead of requiring the agent to call individual drawing tools one-at-time with a full model round-trip with each result, the agent writes an entire JavaScript program against a typed SDK to complete one stage of the graphic or art production process. 

Using AI to execute code is always fraught with problems. The Polson JavaScript interpreter has a number of safety constraints imposed on it:

* No built-in modules or objects apart from those in the standard ECMAScript 2025 language spec.  
* No access to shell commands or local or network I/O. All API methods are just proxies to regular .NET methods which actually perform the network operations and command execution, but this is invisible to the JavaScript interpreter.  
* No access to ‘eval’ or other potentially unsafe JavaScript features.  



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

## Key Library Dependencies
* [SkiaSharp](https://github.com/mono/skiasharp) is a cross-platform 2D graphics API for .NET platforms based on Google's Skia graphics Library
* [Jint](https://github.com/sebastienros/jint) is a ECMAScript 2025 embedded JavaScript interpreter for .NET that has no native dependencies and supports several safety features like disabling eval. Jint is the core of the sandboxed code execution environment the Polson procedural drawing engine uses.
* [Serilog](https://serilog.net/) - The Serilog logging library provides a powerful contextual logger that allows different events triggered by scripts and tools to be correlated and traced.
* [Google GenAI .NET SDK](https://github.com/googleapis/dotnet-genai/) .NET interface to Google's generative models used by the Polson extended mind implementation. 
* [Google Agent Development Kit](https://github.com/google/adk-python) Python SDK used to orchestrate Polson agents.

### Google ADK Agent Orchestration

Polson Graphics Studio runs on Google ADK 2.8.0. ADK owns each project's generation, artifacts, and agent loop, and gives every run a set of lifecycle callbacks that fire outside any single agent. The Polson agent orchestration is [implemented](https://github.com/allisterb/Polson/tree/master/src/adk_agent) as follows:

#### One ADK app per project

ADK scopes sessions, artifacts and toolsets by **app name**, and one app means one `McpToolset`, built once and cached, so one drawing engine process and one project directory for every session it
serves. 

#### The generated project is the agent definition
`agent.py` reads the generated project: its `GEMINI.md` becomes the agent's `instruction`, and the stdio launch command mirrors `ProjectGenerator.McpConfig` exactly. A workflow with a `roles/` directory becomes a root agent plus one ADK sub-agent per role file, parsed by the same code that generates them, so the multi-agent and single-agent cases cannot disagree about what a role is. All of the prompt work in
`ProjectTemplate/*/instructions.md`,  including its untrusted-brief boundary, is inherited rather than reimplemented.

#### Plugins

`BasePlugin` callbacks fire for every agent in an app, outside any of them, so one instance sees a whole run without each agent opting in. That is the seam Polson builds the studio on.

| Plugin | Callbacks | What it does |
|---|---|---|
| `transcript` | `before_run`, `after_run`, `on_run_error`, `after_model` | Writes the run record — `thinking`, `text`, `tool.call`, `usage`, `budget` — into `events/agent.jsonl`, the same vocabulary the web UI, the replay and the sense-making curve already read. |
| `mirror` | `before_run`, `after_run`, `on_run_error` | Sweeps the project directory to Cloud Storage every 20s **while the run is happening**, because a Cloud Run instance can be replaced mid-run and its filesystem does not outlive it. |
| `interject` | `after_tool_callback` | The director's words, mid-run. ADK has no way to push a message into a running invocation, but `after_tool_callback` may return a **replacement tool result** — so the interjection is appended to the next tool result the agent receives, never substituted for it. |
| `StudioWatchdog` | `after_tool_callback` | Puts a directive in front of a role that has stopped making progress. |
| budget / breaker | `before_model_callback` | Below. |


#### Tools

ADK artifacts are not file paths but versioned entities.  `save_artifact` returns an integer, and successive saves under one name accumulate. `peek` is an ADK `FunctionTool` that reads a render from the project, saves it as an ADK artifact, and lets `LoadArtifactsTool` inject it as a real `inline_data` part. `write_script` and `edit_script` exist for the same reason: `ExecuteScript` accepts and runs a `scriptFile`.

#### Budget

All human creators work under time and budget constraints. Every Polson project carries a wall-clock deadline, per-role time allowances, and a token cap measured in billable tokens with cached input tokens charged at a fraction of rgular input

The circuit breaker is a module-level set of tripped `invocation_id`s rather than ADK's `end_invocation`


#### Web Interface

`get_fast_api_app(...)` returns an ordinary `FastAPI`, so the Polson studio interface is mounted onto ADK's own app.

### Research using Gemini Document Processing and the Parallel Task API

The extended mind thesis says cognitive load can be offloaded to the environment — so an agent should query the world rather than recall it. Two surfaces in `Polson.ExtendedMind` do that, and they answer different questions. Gemini Document Processing reads what the director actually supplied; the [Parallel Task API](https://parallel.ai) commissions what nobody supplied, from the open web, with a citation attached. Both are exposed to the agent through the same Code Mode MCP server as the drawing engine, so a figure and the shape that carries it are produced by one program.

The rule both are built around is the one this studio is least willing to break:

> **Never invent a figure, and never draw a placeholder number.** A plausible-looking invented value is the worst thing this system can produce, because the layout puts a source line under it and the graphic then asserts something nobody checked. A chart that admits a missing figure is worth more than one that fabricates it.


A commission may arrive with material attached — a box-office table, a treatment, a shot list, a scanned report. The intake form takes that upload and writes it into the project's `documents/` folder before the agent research, with a basis for every field

An infographic that states a number needs a source for it. The `Research` MCP tool commissions one from the Parallel Task API: an `objective` in prose — read by a model, not a keyword lookup — and a JSON Schema whose field descriptions are the instructions. What comes back is the data plus a `basis` per field: the reasoning, the citations, and a confidence.

```javascript
const data = Research.latest;
if (!data || !data.isComplete) exit('figures not available — do not draw invented ones');

for (const m of data.result.missions) ctx.fillText(`${m.mission}: ${m.duration_hours} h`, x, y);
ctx.fillText(data.citeField('missions.0'), x, y + 20);   // "Apollo 11 - NASA — nasa.gov"
```

Three important architectural guardrails for research:
* A script can read research but cannot start it, and that one-way door is the point. An agent that could author its own `basis` could produce a cited number it made up. Commissioning happens in the MCP tool, outside the JavaScript sandbox, where a blocking call is safe — which it has to be anyway, since a run takes minutes and the script timeout is 30 seconds.

* The allowance is two runs, and they are not equal. The first must carry the entire data requirement in one schema; the second exists only to *correct* it; a field that came back empty, wrong, or at a confidence too low to draw. That is not parsimony for its own sake: one long objective with a rich schema is both faster and more accurate than several small ones, because it pays the latency once and the model reconciles every field against the others in a single pass. 

* A document is the sharpest injection surface in the studio: a PDF can carry a paragraph addressed to whoever is processing it, and a model will faithfully relay it into the agent's context. Research prose has the same shape — a page that talks to whoever is processing it is not behaving like a source. Every string either surface brings back is run through `Polson.TextScan`, a C# port of a codepoint scanner, also exposed as the `ScanText` MCP tool. The concealment classes — bidirectional overrides, zero-width characters, the Unicode Tag block — are **stripped on the way in**, and what was found travels with the answer as `answer.warnings` / `task.warnings` rather than being quietly removed. A finding is not proof the answer is wrong; it is a reason to open the source before citing it, and to report what was found rather than following it.


A run using the Parallel Task API takes two to five minutes measured at 143s for one field and 292s for sixteen, and nothing is written to the record while it waits. The `research.started` event now carries `expectSeconds` and a note saying as much, the tool repeats both on every poll, and the guidance to the agent is to commission first and do the grid, the type scale and the palette while it waits. If the *transport* times out the research is still running and has still cost a run, so the tool hands back a `runId` to resume with rather than letting a retry spend the whole allowance on one question.

A finished run is filed to `.polson/research/<runId>.json`, and that file is what makes a drawn figure checkable after the session. The registry holds tasks in memory and the run record carries only the run id, the processor and the elapsed seconds so before this, the moment a session ended the only surviving account of where a number came from was whatever the agent had transcribed into `brief.md`, and a reader was left holding the *name* of a citation with no way to read it. The file carries the objective and the processor as well as the result, because *what was it asked* is where a wrong figure usually starts — a right answer to a question about the wrong period reads perfectly — and it carries the `basis`, which is the half that matters: the result alone proves a number was transcribed faithfully and says nothing about whether it was ever true.

That archive is also machine-readable, which closes the loop. An agent tags each number as it draws it —

```javascript
paper.text(x, y, '1,636').attr({ 'data-basis': 'trun_abc:totalRuntimeMinutes' });
```

— and the `VerifyFigures` tool reads the saved SVG and reconciles every tagged figure against the run it claims, returning only a verdict so nothing large reaches the agent's context. 


## Key Cloud Service Dependencies 
| Google Cloud Service | Responsibility |
|---|---|
| Parallel | Polson Graphics Studio using  |


### Deployment
PGS and its MCP server and SDK docs and web interface et.al is built as a custom ADK container using Google Cloud Build and deployed to Google Cloud Run. The path `/` brings up the ADK dev-ui console while `/studio` brings up the Polson custom web interface.


## What we learned
Working on this project make made a committed believer in cognitive science principles like enactive cognition and stigmergic collaboration. As I worked I could read the traces agents This is the classic example of vertical stigmergic collaboration: workers leaving traces that builders then use to improve the tools and foundations the builders rely on.
