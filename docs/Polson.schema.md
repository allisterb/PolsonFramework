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
    "Success": {
      "type": "boolean",
      "description": "True if the script executed to completion without unhandled exceptions or syntax errors."
    },
    "Error": {
      "type": ["string", "null"],
      "description": "Error diagnostic message if execution failed."
    },
    "SvgXml": {
      "type": ["string", "null"],
      "description": "Rendered SVG XML markup when an SVG document is produced."
    },
    "PngBytes": {
      "type": ["array", "null"],
      "items": { "type": "integer", "minimum": 0, "maximum": 255 },
      "description": "Rendered PNG image byte stream."
    },
    "Logs": {
      "type": "array",
      "items": { "type": "string" },
      "description": "Accumulated log messages from console.log, log, error, table, and exit calls."
    },
    "ExecutionTimeMs": {
      "type": "integer",
      "description": "Script execution duration in milliseconds."
    },
    "ReturnValue": {
      "description": "The raw evaluation return value from the script."
    }
  },
  "required": ["Success", "Logs", "ExecutionTimeMs"]
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

