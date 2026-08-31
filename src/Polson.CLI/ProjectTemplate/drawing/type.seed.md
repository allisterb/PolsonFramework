## The other hand: seed

**The director has drawn first.** Their sketch is in `seed/` — find it, `view_file` it, and load it
with `Skia.Image.load('seed/<name>')`. It is the opening turn of this session and everything you do
is a response to it.

This gets one genuine human mark into the loop, which is what the `review` type cannot do. It is one
mark rather than a continuing hand, so the session becomes: their opening contribution, then your
turns, then their spoken responses. Treat the seed as the strongest offer in the drawing.

### Before your first turn

**Read it as a drawing, not as a specification.** Say what you actually see — the marks, their weight,
where they are confident and where they trail off, what the composition is already committed to.
Write that as a note before you draw anything. A seed misread in turn one is a session spent politely
elaborating a mistake.

```javascript
Stage.begin('Ground');
Stage.note('seed: four strokes. Two heavy verticals left of centre, one long horizontal at ' +
           'roughly two-thirds height, one hooked mark top right that could be a bird or a ' +
           'branch and is drawn faster than the rest. Reading the hook as the subject and the ' +
           'verticals as the setting — asking before I commit to that.');
```

**Match its register.** A seed drawn in four loose strokes is not asking for a rendered illustration
on top of it, and answering a gestural mark with a tight one is the commonest way this type goes
wrong. Look at line weight, speed and finish, and set your pencil to the same. If you deliberately
raise the finish, say so and say why.

**Keep the seed visible.** Composite it into your surface rather than tracing over it and discarding
it — `ctx.drawImage(seed, 0, 0)` on the ground layer. A reader should be able to see, in the final
render, which marks came from the director. If the drawing eventually covers it, keep an artifact
from before that happened.

### Their marks are the material

Your repertoire runs against the seed's lines first, and against your own once there are some:

- **Extend** their strokes rather than replacing them. A line that trails off is an invitation.
- **Mimic** a mark of theirs elsewhere on the surface — it is the fastest way to make the drawing
  feel like one hand rather than two.
- **Complete** what the seed implies without stating it. This is the move that shows you read it.
- **Depart** from it only deliberately, and say so. Overriding the director's own marks is a real
  move and a legitimate one, but doing it silently reads as not having noticed them.

### If there is no seed

Say so and stop. Do not invent one and proceed — a `seed` session with a seed you drew yourself is a
`review` session with the record claiming otherwise. Ask the director for the file, or ask them to
switch the project to `--type review`.
