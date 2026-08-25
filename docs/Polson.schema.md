# Polson JavaScript SDK — Schema Reference

This is the **schema** reference for the Polson JavaScript SDK. It defines the JSON schemas and model structures returned by drawing operations, element measurements, text metrics, execution results, and graphics primitives.

Served sliced by subject area: read `polson://sdk/schema/{Area}` for an area's schemas, or `polson://sdk/schema/all` for this complete document.

---

# Execution Envelopes

## `DrawingExecutionResult`

The result envelope produced by executing a JavaScript drawing script:

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "DrawingExecutionResult",
  "type": "object",
  "properties": {
    "success": {
      "type": "boolean",
      "description": "True if the script executed to completion without unhandled exceptions or syntax errors."
    },
    "error": {
      "type": ["string", "null"],
      "description": "Error diagnostic message if execution failed."
    },
    "svgXml": {
      "type": ["string", "null"],
      "description": "Rendered SVG XML markup when a vector SVG document is produced."
    },
    "imageBytes": {
      "type": ["array", "null"],
      "items": { "type": "integer", "minimum": 0, "maximum": 255 },
      "description": "Rendered image byte stream (encoded as WebP, PNG, or JPEG; default: WebP Q=85)."
    },
    "imageFormat": {
      "type": "string",
      "description": "The format of the encoded image ('webp', 'png', 'jpeg')."
    },
    "imageDataUri": {
      "type": ["string", "null"],
      "description": "The rendered image formatted as a base64 Data URI ('data:image/...;base64,...')."
    },
    "logs": {
      "type": "array",
      "items": { "type": "string" },
      "description": "Accumulated log messages from console.log, log, error, table, and exit calls."
    },
    "executionTimeMs": {
      "type": "integer",
      "description": "Script execution duration in milliseconds."
    },
    "returnValue": {
      "description": "The raw evaluation return value from the script."
    }
  },
  "required": ["success", "logs", "executionTimeMs", "imageFormat"]
}
```

---

# Snap

## `SnapBBox`

Bounding box returned by `Snap.path.getBBox(d)` and `element.getBBox()`:

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "SnapBBox",
  "type": "object",
  "properties": {
    "x": { "type": "number", "description": "Left X coordinate of the bounding box." },
    "y": { "type": "number", "description": "Top Y coordinate of the bounding box." },
    "x2": { "type": "number", "description": "Right X coordinate (x + width)." },
    "y2": { "type": "number", "description": "Bottom Y coordinate (y + height)." },
    "width": { "type": "number", "description": "Width of the bounding box." },
    "height": { "type": "number", "description": "Height of the bounding box." },
    "cx": { "type": "number", "description": "Center X coordinate." },
    "cy": { "type": "number", "description": "Center Y coordinate." }
  },
  "required": ["x", "y", "width", "height", "cx", "cy", "x2", "y2"]
}
```

## `SnapPoint`

Point along a path returned by `Snap.path.getPointAtLength(d, len)` and `element.getPointAtLength(len)`:

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "SnapPoint",
  "type": "object",
  "properties": {
    "x": { "type": "number", "description": "X coordinate at length." },
    "y": { "type": "number", "description": "Y coordinate at length." },
    "alpha": { "type": "number", "description": "Tangent angle in degrees at length." }
  },
  "required": ["x", "y", "alpha"]
}
```

## `SnapMatrix`

Affine transformation matrix components `[ a c e ; b d f ; 0 0 1 ]`:

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "SnapMatrix",
  "type": "object",
  "properties": {
    "a": { "type": "number", "description": "Horizontal scaling / cosine." },
    "b": { "type": "number", "description": "Horizontal shearing / sine." },
    "c": { "type": "number", "description": "Vertical shearing / -sine." },
    "d": { "type": "number", "description": "Vertical scaling / cosine." },
    "e": { "type": "number", "description": "Horizontal translation (dx)." },
    "f": { "type": "number", "description": "Vertical translation (dy)." }
  },
  "required": ["a", "b", "c", "d", "e", "f"]
}
```

---

# Canvas2D

## `TextMetrics`

Object returned by `ctx.measureText(text)`:

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "TextMetrics",
  "type": "object",
  "properties": {
    "width": { "type": "number", "description": "Advance width of the measured text in CSS pixels." },
    "actualBoundingBoxAscent": { "type": "number", "description": "Distance from alphabetic baseline to top of glyph bounding box." },
    "actualBoundingBoxDescent": { "type": "number", "description": "Distance from alphabetic baseline to bottom of glyph bounding box." },
    "fontBoundingBoxAscent": { "type": "number", "description": "Distance from baseline to highest ascent of font." },
    "fontBoundingBoxDescent": { "type": "number", "description": "Distance from baseline to lowest descent of font." }
  },
  "required": ["width", "actualBoundingBoxAscent", "actualBoundingBoxDescent", "fontBoundingBoxAscent", "fontBoundingBoxDescent"]
}
```

---

# Skia

## `ImageData`

Raw RGBA pixel buffer returned by `ctx.getImageData(...)` and `canvas.toImageData()`:

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "ImageData",
  "type": "object",
  "properties": {
    "width": { "type": "integer", "description": "Width of the image buffer in pixels." },
    "height": { "type": "integer", "description": "Height of the image buffer in pixels." },
    "data": {
      "type": "array",
      "items": { "type": "integer", "minimum": 0, "maximum": 255 },
      "description": "One-dimensional array containing RGBA pixel byte values in row-major order."
    }
  },
  "required": ["width", "height", "data"]
}
```

## `SkiaBitmapWrapper`

Bitmap container returned by `Skia.Bitmap.create(...)`, `Skia.Image.load(...)`, `canvas.toBitmap()`, and bitmap transformation methods:

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "SkiaBitmapWrapper",
  "type": "object",
  "properties": {
    "width": { "type": "integer", "description": "Bitmap width in pixels." },
    "height": { "type": "integer", "description": "Bitmap height in pixels." }
  },
  "required": ["width", "height"]
}
```

---

# Drawing (Constructive Toolkit Schemas)

## `LoomisHead`

Parametric 3D cranial structure model returned by `Drawing.createLoomisHead(...)`:

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "LoomisHead",
  "type": "object",
  "properties": {
    "origin": { "type": "object", "properties": { "x": { "type": "number" }, "y": { "type": "number" } }, "required": ["x", "y"] },
    "cranialCenter": { "type": "object", "properties": { "x": { "type": "number" }, "y": { "type": "number" } }, "required": ["x", "y"] },
    "cranialRadius": { "type": "number" },
    "brow": { "type": "object", "properties": { "x": { "type": "number" }, "y": { "type": "number" } }, "required": ["x", "y"] },
    "noseBase": { "type": "object", "properties": { "x": { "type": "number" }, "y": { "type": "number" } }, "required": ["x", "y"] },
    "chin": { "type": "object", "properties": { "x": { "type": "number" }, "y": { "type": "number" } }, "required": ["x", "y"] },
    "nearEye": { "type": "object", "properties": { "center": { "type": "object" }, "rx": { "type": "number" }, "ry": { "type": "number" } } },
    "farEye": { "type": "object", "properties": { "center": { "type": "object" }, "rx": { "type": "number" }, "ry": { "type": "number" } } },
    "noseWedge": { "type": "object", "properties": { "bridge": { "type": "object" }, "apex": { "type": "object" }, "base": { "type": "object" } } },
    "mouthGuides": { "type": "object", "properties": { "center": { "type": "object" }, "leftCorner": { "type": "object" }, "rightCorner": { "type": "object" } } }
  },
  "required": ["origin", "cranialCenter", "cranialRadius", "brow", "noseBase", "chin", "nearEye", "farEye", "noseWedge", "mouthGuides"]
}
```

## `PerspectiveGrid`

Camera projection vanishing point and horizon model returned by `Drawing.createPerspectiveGrid(...)`:

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "PerspectiveGrid",
  "type": "object",
  "properties": {
    "type": { "type": "string", "enum": ["1point", "2point", "3point"] },
    "horizonY": { "type": "number", "description": "Y coordinate of the horizon line." },
    "centerOfVisionX": { "type": "number", "description": "X coordinate of the principal point of projection." },
    "focalLength": { "type": "number", "description": "Camera focal distance d in pixels." },
    "cameraAngleDeg": { "type": "number", "description": "Yaw angle in degrees." },
    "vpL": { "type": "object", "properties": { "x": { "type": "number" }, "y": { "type": "number" } }, "required": ["x", "y"] },
    "vpR": { "type": "object", "properties": { "x": { "type": "number" }, "y": { "type": "number" } }, "required": ["x", "y"] }
  },
  "required": ["type", "horizonY", "centerOfVisionX", "focalLength", "vpL", "vpR"]
}
```

## `PerspectiveBox`

3D projected geometric bounding box model returned by `Drawing.createPerspectiveBox(...)`:

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "PerspectiveBox",
  "type": "object",
  "properties": {
    "vertices": {
      "type": "array",
      "items": { "type": "object", "properties": { "x": { "type": "number" }, "y": { "type": "number" } }, "required": ["x", "y"] },
      "minItems": 8,
      "maxItems": 8
    },
    "faces": {
      "type": "object",
      "properties": {
        "top": { "type": "array", "items": { "type": "object" } },
        "bottom": { "type": "array", "items": { "type": "object" } },
        "left": { "type": "array", "items": { "type": "object" } },
        "right": { "type": "array", "items": { "type": "object" } },
        "backLeft": { "type": "array", "items": { "type": "object" } },
        "backRight": { "type": "array", "items": { "type": "object" } }
      },
      "required": ["top", "bottom", "left", "right", "backLeft", "backRight"]
    }
  },
  "required": ["vertices", "faces"]
}
```

## `MannequinFigure`

Parametric 8-head proportional full-body anatomical joint model returned by `Drawing.createMannequinFigure(...)`:

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "MannequinFigure",
  "type": "object",
  "properties": {
    "headUnit": { "type": "number", "description": "Height of one head unit (totalHeight / 8)." },
    "totalHeight": { "type": "number", "description": "Total standing figure height in pixels." },
    "head": { "type": "object", "properties": { "center": { "type": "object" }, "rx": { "type": "number" }, "ry": { "type": "number" } } },
    "neck": { "type": "object", "properties": { "x": { "type": "number" }, "y": { "type": "number" } } },
    "sternum": { "type": "object", "properties": { "x": { "type": "number" }, "y": { "type": "number" } } },
    "clavicles": { "type": "object", "properties": { "left": { "type": "object" }, "right": { "type": "object" }, "center": { "type": "object" } } },
    "ribcage": { "type": "object", "properties": { "center": { "type": "object" }, "rx": { "type": "number" }, "ry": { "type": "number" }, "tiltDeg": { "type": "number" } } },
    "navel": { "type": "object", "properties": { "x": { "type": "number" }, "y": { "type": "number" } } },
    "pelvis": { "type": "object", "properties": { "center": { "type": "object" }, "leftHip": { "type": "object" }, "rightHip": { "type": "object" }, "rx": { "type": "number" }, "ry": { "type": "number" }, "tiltDeg": { "type": "number" } } },
    "crotch": { "type": "object", "properties": { "x": { "type": "number" }, "y": { "type": "number" } } },
    "leftArm": { "type": "object", "properties": { "shoulder": { "type": "object" }, "elbow": { "type": "object" }, "wrist": { "type": "object" }, "hand": { "type": "object" } } },
    "rightArm": { "type": "object", "properties": { "shoulder": { "type": "object" }, "elbow": { "type": "object" }, "wrist": { "type": "object" }, "hand": { "type": "object" } } },
    "leftLeg": { "type": "object", "properties": { "hip": { "type": "object" }, "knee": { "type": "object" }, "ankle": { "type": "object" }, "foot": { "type": "object" } } },
    "rightLeg": { "type": "object", "properties": { "hip": { "type": "object" }, "knee": { "type": "object" }, "ankle": { "type": "object" }, "foot": { "type": "object" } } }
  },
  "required": ["headUnit", "totalHeight", "head", "neck", "sternum", "clavicles", "ribcage", "navel", "pelvis", "crotch", "leftArm", "rightArm", "leftLeg", "rightLeg"]
}
```


