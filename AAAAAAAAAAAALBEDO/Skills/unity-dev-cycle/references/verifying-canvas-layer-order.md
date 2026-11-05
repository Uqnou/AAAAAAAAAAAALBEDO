# Verifying canvas / SVG layer-order fixes

When the symptom is "A disappears / is covered when B is present" (a neck lost
under long hair, a body part hidden behind a sprite, a badge swallowed by a
banner), the bug is almost always draw order: the occluder is painted **after**
the item it hides. The fix is to move the occluded item after the occluder, plus
a clip that keeps the new overlay inside the region it owns so it does not
cover its other neighbour. Verify that with a call-order recorder, because a
canvas cannot be screenshotted inside a test run.

## Diagnosing (plan phase)

- Read the render entry point and list the actual call order with line numbers
  before writing the plan: what is drawn first, what paints over it.
- Symptom-to-cause map: "X missing with Y present" -> Y is drawn after X.
  "X missing only with variant V" -> V's asset extends lower/higher than the
  others; check the variant table (e.g. per-style clip constants) for the
  styles that already compensate.
- Prefer reordering an existing call plus a clip over touching assets,
  constants, or adding a new layer. State explicitly in the scope boundary
  that assets and constants stay untouched.

## Harness shape (Antigravity writes and runs it)

- **Stub the 2D context**: every method (`save`, `restore`, `translate`,
  `rotate`, `scale`, `clip`, `fill`, `fillRect`, `drawImage`, `beginPath`,
  `rect`, `arc`, `moveTo`, `lineTo`, `bezierCurveTo`, `closePath`, `stroke`)
  appends `{op, ...args}` to a call list and returns undefined; stub `Path2D`
  and `Image` so `img.complete` / `naturalWidth` look loaded.
- **Wrap the renderer's own draw methods** (`drawHeadBase`, `drawBackHair`,
  `drawTorsoAndNeck`, ...) so each appends a named entry plus its `callIndex`.
  Assert on renderer-level order, not on raw canvas ops — raw ops are noisy
  and change when internals refactor.
- Assertions per acceptance criterion:
  - occluded method index > occluder index, **iterated over every variant id**
    in the config enum (every head style, every badge type) — a single-variant
    check passes while one style still fails;
  - the overlay's clip geometry actually constrains it: a `clip` call precedes
    the fill, and the rect's boundary (y / height) matches the seam line
    (chin, waistline) with the coordinates from the code, not guessed;
  - the sibling views that must NOT change are asserted by absence — find the
    new method's event and require it to be undefined there;
  - print the full call order for old views as a regression record.

## Rules that generalize

- RED must have the right shape: the new method shows as missing (`NONE`) in
  the order trace. A run with no assertions is not RED.
- Constrain a newly-reordered overlay with a clip to the region it owns;
  reordering without a clip fixes one overlap and creates another (the overlay
  covers the thing it must stay behind).
- Iterate the full variant enum in the harness — long/oversized variants are
  exactly where the bug lives.
- Assert the negative case explicitly (the view that must not call the new
  helper); it is the cheapest guard against the fix leaking into a sibling
  path.
- Evidence to read back is the harness stdout with per-criterion PASS/FAIL
  plus the printed call order for every view. A prose "tests pass" is not
  evidence — send the task back for the output.

## Delegation prompt shape for this class

Inline the current call order with line numbers, the helper/asset names, the
clip constants and what they mean, and one named negative case per sibling
view. Require the harness in `/tmp` before the code (RED), then the output
pasted back. Keep the scope boundary narrow: one file, no asset or constant
changes.
