---
name: unity-dev-cycle
description: "Plan in Hermes; implement/test via Antigravity + Unity CLI."
version: 1.0.0
author: Rennan (rennancr93), Hermes Agent
license: MIT
platforms: [linux, macos, windows]
metadata:
  hermes:
    tags: [workflow, orchestration, antigravity, unity, unity-cli, delegation, planning, testing]
    related_skills: [dev-cycle, test-driven-development, systematic-debugging, requesting-code-review, simplify-code, antigravity-cli]
---

# Unity Dev Cycle — plan in Hermes, execute in Antigravity + Unity CLI

A specialized variant of `dev-cycle` for Unity projects. Same gated loop, same
token economy, but the execution layer is the **Unity CLI** driving a running
Unity Editor (or headless batchmode) through the **Pipeline package**.

- **Hermes (free models) plans and verifies.** Reads the Unity project, writes
  the plan, delegates, checks evidence. Coordination is cheap here.
- **Antigravity (paid models) reviews, implements, and tests.** All execution
  work — writing C#, compiling via Unity CLI, running tests — happens in the
  Antigravity Desktop app, reached through the MCP bridge.
- **Unity CLI** is the bridge between Antigravity and the Unity Editor. It is a
  standalone binary that talks HTTP+JSON to the Pipeline package running inside
  the Editor.

Every phase ends in a **gate** with a checkable exit criterion. Nothing reaches
"done" on the strength of "it looks right" or of Antigravity's own claim that it
worked — Hermes verifies evidence before closing a gate.

## When to Use

**Auto-detection:** This skill activates automatically when the working directory
is a Unity project. Detection is platform-agnostic — it works for mobile, VR,
Mac, Windows, 2D, 3D, any Unity project. See `references/detecting-unity-project.md`
for the detection logic.

Use this skill when:
- The project is a Unity project (has `ProjectSettings/ProjectVersion.txt`)
- The task involves C# scripts, scenes, prefabs, components, shaders, or any
  Unity asset
- You want the Antigravity agent to write and test Unity code via the Unity CLI

Don't use for:
- Non-Unity projects (use `dev-cycle` instead)
- Pure Q&A about Unity concepts
- Tasks that don't involve code changes (e.g., "what does this Unity API do?")

## The Loop

```
  ┌───────────── HERMES (free) ─────────────┐
  │ 1. PLAN                                  │
  │   - Read Unity project structure         │
  │   - Identify target scripts/components   │
  │   - Write plan + acceptance criteria     │
  └──────────────────┬───────────────────────┘
                     │ plan + acceptance criteria + scope boundary
                     ▼
  ┌───────────── ANTIGRAVITY (paid) ────────┐
  │ 2. REVIEW → 3. IMPLEMENT → 4. TEST      │
  │   - Review plan against Unity project    │
  │   - Write C# code                        │
  │   - Compile via `unity command eval`     │
  │   - Run tests via `unity command`         │
  └──────────────────┬───────────────────────┘
                     │ evidence: compile output, test results, trajectory
                     ▼
  ┌───────────── HERMES verifies ───────────┐
  │ gate pass → done · gate fail → re-plan  │
  └──────────────────────────────────────────┘
```

A failed gate sends you back — to PLAN if the design is wrong, to IMPLEMENT if
only the code is wrong.

## Division of labor

| Phase | Who | Model tier | Output |
|---|---|---|---|
| 1. PLAN | Hermes | free (this session) | written plan + acceptance criteria |
| 2. REVIEW | Antigravity | deep (pro/high) | verdict + objections + plan fixes |
| 3. IMPLEMENT | Antigravity | deep or fast by complexity | C# code + compile evidence |
| 4. TEST | Antigravity writes AND runs tests via Unity CLI; Hermes reads output | fast (flash/medium) | real test output, evidence read by Hermes |

Planning is never delegated: it would spend paid tokens re-deriving context the
free session already has, and the plan is the one artifact that must exist
*before* any delegation.

**Diagnosis and debugging are execution work, not planning work.** "Something is
wrong in the Unity project, find it" belongs to Antigravity, not to the Hermes
session: writing a probe script, instrumenting a component, bisecting a
regression and running it are all the same class of paid execution. Hermes reads
the code to *plan*, and that is where reading stops — the moment the answer
requires a script that runs, the script is Antigravity's.

This split is a **standing operator rule, not a suggestion**. A skill alone does
not make it stick — see "Always-on enforcement" below for how it is pinned into
every session.

## Always-on enforcement

Writing the skill is not enough: **a skill that nothing triggers is never
loaded, and an unloaded skill is not followed.** A session that never consults
the skill will happily implement everything in the free orchestrator, which is
exactly the failure this loop exists to prevent.

Pin the rule into the system prompt with three layers, strongest first:

1. **Force the coding posture.** `hermes config set agent.coding_context on`.
   In `auto` mode the posture is *detected* (project marker, or a git repo that
   holds code), so a brand-new project with no code and no git runs in the
   general posture — and the general posture injects none of the rules below.
2. **Set the standing instruction.**
   `hermes config set agent.coding_instructions "..."` with the delegate-don't-
   execute rule spelled out (route before sending, never queue on RUNNING, read
   evidence back, say so when the bridge is down). It rides the coding brief as
   a cache-stable block, so it costs no extra turn.
3. **Mirror it in `$HERMES_HOME/SOUL.md`** — identity, loaded unconditionally
   on every session regardless of posture or cwd. This is the layer that still
   holds when the posture detector misfires.

Verify the injection actually happened rather than trusting the set command:
run the real resolver and read the parts back
(`resolve_runtime_mode(...).system_prompt_parts()` — the operator block is the
third element, and empty when the posture is not coding). Details in
`references/persisting-a-rule-across-sessions.md`.

## Phase 1 — PLAN (Hermes, free)

Read the relevant code first (`search_files`, `read_file`) and pull project
state from the Second Brain (`brain_read`, `brain_search`, `brain_node`) so you
plan against reality, not a guess. For Unity projects specifically:

- Read `ProjectSettings/ProjectVersion.txt` to know the Unity version
- Read `Packages/manifest.json` to know installed packages
- Read `Assets/` structure to understand the project layout
- Identify which scripts, prefabs, or scenes the task touches

A plan is:

- **What** changes, in which files, and why.
- **Acceptance criteria** — observable behaviors that must hold when done.
  For Unity: "script compiles without errors", "test X passes", "component Y
  behaves correctly in Play mode".
- **Risk / unknowns**, each with a resolution path.
- **Scope boundary** — what is deliberately *not* in this change.

Write the plan down (chat or a spec file the project tracks); it travels
verbatim into the delegation prompts.

**Gate 1:** the plan names concrete files and at least one checkable acceptance
criterion. "Improve the player controller" is not a plan; "make
`PlayerController.cs` handle slope movement without sliding, verified by the
existing movement test plus a new slope test" is.

## Phase 2 — REVIEW (Antigravity, deep tier)

Send the plan to Antigravity on a high-reasoning model and ask it to attack:
criteria coverage, scope, existing patterns, what breaks (callers, tests, data
contracts, Unity API compatibility), and a simpler alternative. The prompt must
demand a **verdict + objections + concrete plan changes**, not agreement.

For Unity specifically, the review should also check:
- Unity API compatibility (does the code use APIs available in the project's
  Unity version?)
- Package dependencies (does the plan require packages not in
  `Packages/manifest.json`?)
- Scene/prefab references (does the plan break existing scene references?)

**Gate 2:** you have read Antigravity's review back (via the conversation's
trajectory, not its summary claim) and either adjusted the plan or consciously
rejected each objection with a reason. A review you didn't read is not a review.

## Phase 3 — IMPLEMENT (Antigravity, TDD inside the prompt)

Delegate the approved plan. The prompt carries everything the executing agent
needs, because a resumed conversation must not re-derive context:

- the full plan, acceptance criteria, and scope boundary;
- the project's Unity version (from `ProjectSettings/ProjectVersion.txt`);
- the project's test command (usually `unity command run_tests` or a custom
  test command);
- an explicit TDD instruction: failing test first, watch it fail, minimal code,
  refactor — and **report the exact compile/test output**;
- "stay inside the scope boundary; flag anything that would exceed it instead
  of absorbing it".

Pick the tier by complexity: `deep` (pro/high) for multi-file features and
systems, `fast` (flash/medium) for well-defined single concerns, `light`
only for mechanical edits. Watch progress with `agy_list_conversations` (status
+ step count) and wait for the conversation to return to IDLE (`agy_wait_idle`)
before verifying.

**Unity-specific implementation notes:**
- The Antigravity agent writes C# files using `write_file` or `patch`
- After writing, it compiles via `unity command eval "return true;"` or
  `unity command compile` to check for errors
- If compilation fails, the agent reads the error output and fixes the code
- The agent iterates until compilation succeeds

**Gate 3:** the implementation exists and covers the plan's scope. If
Antigravity's diff grew past the boundary, that is a failed gate — send it back
or re-plan, don't absorb the creep.

## Phase 4 — TEST (Antigravity runs, Hermes verifies)

Antigravity runs the real test suite as part of implementation; this phase is
about **evidence**, not re-running. Read the trajectory (`agy_get_trajectory`) and
confirm:

- the test command actually ran, with its captured output visible;
- the suite is green;
- each acceptance criterion from Phase 1 is demonstrated;
- the conversation is back to IDLE and the step count advanced.

**Unity-specific test notes:**
- Unity tests can be run via `unity command run_tests` (if the project has
  a test setup) or via `unity command eval` with custom test code
- The test output should show pass/fail counts
- If the project has no test framework, the agent can write a simple test
  script that exercises the target behavior via `unity command eval`

**The harness is Antigravity's, end to end.** Writing the probe/stub script and
debugging why it is wrong is execution work; Hermes reads the output back and
never authors its own verification script to "check" the diff. If a criterion is
not covered by the evidence Antigravity produced, ask Antigravity to produce it
— do not fill the gap yourself.

"Tests pass" asserted without output in the trajectory is a failed gate — send
a follow-up demanding the output. After green, a `simplify-code` pass can be a
separate `light`-tier delegation; it must keep the suite green.

**Gate 4:** every acceptance criterion is backed by evidence you actually read,
and the suite is green. This is the only gate that says "done."

## Delegation mechanics (bridge quick reference)

Requires Antigravity Desktop open (the endpoint's port + CSRF token rotate on
every app restart; the bridge tools auto-discover, the `hermes-ag` CLI probes
for the right port):

- Route first: `mcp__antigravity__agy_route` (or `hermes-ag route [hint]`) —
  continue the newest **IDLE** conversation whose workspace matches. A new
  cascade re-reads the whole repo into context; continuing is the token economy.
- Send: `mcp__antigravity__agy_send_message` (or `hermes-ag send <id> "<msg>"`);
  `hermes-ag run "<prompt>" <light|fast|deep> [project]` routes + sends in one
  step.
- Read back: `mcp__antigravity__agy_get_trajectory` (history + tool output),
  `agy_list_conversations` (status/steps), `agy_wait_idle` (block until done).
- Context: `brain_read` / `brain_search` / `brain_node` read the project's
  Second Brain so Hermes knows project state without paying to re-derive it.
- Tier → model: `light` → gemini-3.1-flash-lite (low), `fast` → gemini-3.8-flash
  (medium), `deep` → gemini-3.1-pro (high).

Sending to an IDLE conversation wakes it (status flips to RUNNING) — that is
the point, but never queue work onto a conversation that is already RUNNING.

## Unity CLI quick reference

The Unity CLI is the bridge between Antigravity and the Unity Editor. Key
commands for the dev cycle:

```bash
# Check Unity CLI is installed
unity --version

# List installed editors
unity editors --format json

# Open a project (starts Editor with Pipeline package)
unity open <project-path>

# Open headless (no window, no GPU — ideal for CI/agents)
unity open <project-path> --args "-batchmode -nographics"

# List available commands in the connected Editor
unity command

# Execute a command
unity command <command-name> [args]

# Evaluate C# expression in the running Editor (REPL)
unity command eval "return Application.version;"

# Run tests
unity command run_tests

# Check Pipeline package status
unity pipeline list

# Install Pipeline package (one-time setup)
unity pipeline install
```

Full reference: `references/unity-cli-reference.md`

## Pitfalls

- **Trusting Antigravity's self-report.** "Done, tests pass" without output in
  the trajectory is an unverified claim; Gate 4 is evidence you read yourself.
- **Antigravity not auto-committing.** The skill says "commit automatically" but
  the Antigravity agent consistently stops after the harness passes without
  committing. Always check `git log --oneline -1` and `git status --short`
  after Gate 4 passes. If the commit wasn't made, send a follow-up message
  demanding it — don't assume the auto-commit happened just because the rule
  says so. The follow-up message must: (a) state Gate 4 passed, (b) list exact
  file paths to stage, (c) forbid .DS_Store, (d) request an English conventional
  commits message, (e) ask for the hash and git status after.
- **Delegating the plan.** Planning in Antigravity burns paid tokens re-deriving
  what the free session already knows, and skips Gate 1 entirely.
- **Prompts without full context.** The delegation prompt must inline the plan,
  criteria, scope, and test command — the executing agent works from the prompt,
  not from your head.
- **Sending to a RUNNING conversation.** Queue conflicts and lost work; route
  to an IDLE conversation or wait for the current one to finish.
- **Antigravity Desktop closed / bridge unreachable.** Discovery fails; tell the
  user to open the app instead of pretending the delegation happened.
- **Trusting `agy_wait_idle` to block.** The bridge's `agy_wait_idle` demands
  `conversation_id` and still errors out with it ("conversation_id is required"
  even when passed). Monitor progress with `agy_list_conversations` (status +
  step count) and treat the flip back to IDLE as the completion signal instead.
- **Staging OS cruft with the fix.** `git status` on macOS shows `.DS_Store`
  as modified even though `.gitignore` lists it — `.gitignore` has no effect on
  files already tracked, so the commit silently carries them. Stage the target
  file by path only (`git add <file>`), never `git add .`; untrack cruft with
  `git rm --cached` in a separate housekeeping commit so the fix stays reviewable.
- **Scope creep absorbed silently.** If the diff exceeds the plan, that is a
  failed Gate 3 — loop back, don't rationalize it as "while I was in there".
- **Free session doing execution work.** Every file edit or test run Hermes does
  itself defeats the token split; Hermes coordinates and verifies only. This
  includes **writing its own verification script or debugging why one is wrong**
  — a harness Hermes authored is not evidence of anything (see the
  self-harness pitfall below). When the user asks "shouldn't <the executing
  agent> be doing this?", the answer is yes — hand the diff over and verify its
  evidence rather than redoing the work. Say it plainly and re-delegate instead
  of silently continuing to execute.
- **Assuming a written skill is an active rule.** A skill is loaded on demand;
  if no session ever consults it, the behaviour it describes never happens. Pin
  any standing workflow rule into the system prompt (`coding_instructions` +
  `SOUL.md`) and confirm by reading the resolved prompt back, not by checking
  that the file was written.
- **A harness you wrote yourself is not evidence.** A verification script that
  exercises only the functions you authored passes while a sibling path your
  same diff touched is still broken — you tested what you meant to change, not
  what you actually changed. Verify every code path the diff reaches, and prefer
  the executing agent's or an independent reviewer's run over your own.
- **Deleting "dead" code without grepping the repo's own guards.** A file that
  no app HTML references may still be executed by a pre-commit hook or a CI
  job; removing it breaks the commit rather than cleaning anything. Grep
  `.git/hooks/` and CI configs for the path first, and honour the file-ownership
  map in the repo's AGENTS.md before deleting or editing someone else's file.
- **Trusting a half-applied fix.** Fixing a class of bug at the sites you
  happened to read ("escaped these four interpolations") and reporting it closed
  is a failed gate. Enumerate every site in the class — grep for the pattern —
  and cover the branch you did not look at, including the sibling handler of the
  one you fixed.
- **Diagnosing a "hidden / covered" visual bug as anything but draw order.**
  When the symptom is "A is not visible / disappears when B is present" (a neck
  lost under long hair, a body part hidden behind a sprite), the occluder is
  painted after the occluded item — suspect layer order before anything else.
  The plan lists the current call order with line numbers, and the fix is
  reorder-plus-clip, not a new asset or a retuned constant. For canvas/2D work
  the harness is a call-order recorder, not a screenshot — see
  `references/verifying-canvas-layer-order.md`.
- **Unity Editor not running.** The Unity CLI requires a running Editor (or
  headless batchmode) to execute commands. If `unity command` fails with
  "no connected Editor", the Antigravity agent must first run
  `unity open <project> --args "-batchmode -nographics"` to start the Editor.
- **Pipeline package not installed.** If `unity pipeline list` shows no package,
  the agent must run `unity pipeline install` first. This is a one-time setup
  per project.
- **Unity version mismatch.** The project's Unity version (from
  `ProjectSettings/ProjectVersion.txt`) must match an installed Editor. If not,
  the agent must install the correct version via `unity install <version>`.
- **Compile errors not being read.** The Antigravity agent must read the full
  compile error output from `unity command eval` and fix all errors, not just
  the first one. Partial fixes that leave the project in a non-compiling state
  are a failed Gate 3.

## Verification

The loop is complete only when all five hold:

- [ ] A written plan existed before any delegation, naming files + acceptance
      criteria (produced in the Hermes session, on free models).
- [ ] The plan was reviewed by Antigravity and its review was read back.
- [ ] The implementation delegation carried plan + criteria + scope + test
      command + TDD instruction, and the diff stayed inside scope.
- [ ] Test evidence was read from the trajectory: real command output, suite
      green, conversation back to IDLE.
- [ ] Each acceptance criterion is demonstrated by that evidence.

**Auto-commit on green.** When Gate 4 passes and every acceptance criterion is
backed by evidence you actually read, commit automatically — no need to ask the
user first. Stage only the target files by path (`git add <file>`), never
`git add .`. Untrack OS cruft (`.DS_Store`) in a separate housekeeping commit.
If the user asks to commit manually, honor that instead.

**Follow-up commit is the norm, not the exception.** The Antigravity agent
frequently stops after the harness passes without committing — even when the
skill says "commit automatically." The reliable pattern is:
1. After Gate 4 passes, check `git log --oneline -1` and `git status --short`.
2. If files are still uncommitted, send a follow-up message to Antigravity:
   "Gate 4 passed — all criteria PASS. Auto-commit on green: commit now.
   Stage ONLY <file paths> by path. No .DS_Store. English conventional commits
   message. Report the hash and git status after."
3. Antigravity then commits and reports the hash.
Do not assume the auto-commit happened — always verify git status before
declaring done.

Report the result as: what changed, which files, which Antigravity conversation
did the work (id), the test command and its real captured output, and anything
left undone. Never claim a gate you didn't actually check.

## Commit conventions

Commits are made by Antigravity (paid tier), not by Hermes. Conventions:

- **Language:** English (even though the user speaks pt-BR).
- **Style:** Conventional Commits (`fix(scope):`, `feat(scope):`, `chore:`).
- **Scope:** `fix(player):`, `feat(ui):`, `fix(physics):`, `chore:` — match
  the subsystem that changed.
- **Body:** One line describing the change; no multi-paragraph body.
- **Multi-file commits:** Group related changes (script + component + test) into a
  single commit. Separate housekeeping (`.DS_Store` untracking) into its own
  commit so the fix stays reviewable.

## Support files

- `references/unity-cli-reference.md` — full Unity CLI command reference for the
  dev cycle, including `unity command eval`, `unity command run_tests`, and
  headless batchmode setup.
- `references/detecting-unity-project.md` — how to detect a Unity project
  automatically, regardless of platform (mobile, VR, Mac, Windows, 2D, 3D).
- `references/unity-test-harness.md` — how to write and run tests via Unity CLI,
  including custom test scripts and `unity command eval` patterns.
- `references/persisting-a-rule-across-sessions.md` — how to pin a standing rule
  (like this delegation split) into every session via `coding_context`,
  `coding_instructions` and `SOUL.md`, and how to verify the injection actually
  happened instead of trusting the `config set`.
- `references/adversarial-review-of-own-work.md` — how to hand your own
  uncommitted changes to Antigravity for an independent attack (prompt shape,
  which questions get real findings), and the self-verification traps that make
  an agent's own test run worthless as evidence. Bridge command details live in
  the `antigravity-cli` skill.
- `references/verifying-canvas-layer-order.md` — how a "X is covered when Y is
  present" bug is actually a draw-order bug, and the call-order-recorder harness
  shape that proves a reorder-plus-clip fix: iterate every style variant, assert
  the clip geometry, assert the negative case in sibling views, and make RED
  show the method as missing.
