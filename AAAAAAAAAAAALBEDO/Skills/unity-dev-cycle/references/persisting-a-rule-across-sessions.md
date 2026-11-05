# Pinning a standing rule into every session

How to make a workflow rule survive a fresh session instead of living only in a
skill that nothing triggers. Use this whenever the user says a behaviour must be
*persistent* — "always do X", "never do X again".

## The three injection layers

| Layer | Where | When it applies | Cache-safe |
|---|---|---|---|
| Coding posture | `agent.coding_context` | only when the posture resolves to coding | yes |
| Operator instructions | `agent.coding_instructions` | only when the posture resolves to coding | yes |
| Identity | `$HERMES_HOME/SOUL.md` | **unconditionally**, every session, any cwd | yes |

`SOUL.md` is the only unconditional layer. It is loaded from the profile home
and lands in the stable prefix of the system prompt, independent of workspace
detection. Any rule that must never be skipped belongs here; workspace-scoped
rules belong in `.hermes.md` / `AGENTS.md` (see the `hermes-agent` skill →
`references/project-context-files.md`).

## The trap: a detected posture is not a guaranteed posture

`coding_context: auto` decides by *detecting* a code workspace — a project
marker, or a git repo that actually contains code. A brand-new project with no
code and no git resolves to the **general** posture, and the general posture
injects neither the operator instructions nor the coding brief. Everything the
user pinned is silently absent.

Force it instead of hoping for detection:

```
hermes config set agent.coding_context on
```

## Verify by reading the resolved prompt, not the written file

A `config set` succeeding proves the YAML parsed. It does not prove the rule
reaches the model. Resolve it the way the runtime does and inspect the parts:

```python
import sys; sys.path.insert(0, ".")
from agent.coding_context import resolve_runtime_mode
rm = resolve_runtime_mode(platform="desktop", cwd="/path/to/new/project")
prefix, workspace, trailing = rm.system_prompt_parts()
# trailing == [] means the posture is NOT coding and nothing was injected
print("is_coding:", rm.is_coding, "| operator blocks:", len(trailing))
```

Also confirm the identity layer picked the file up:

```python
from agent import prompt_builder as _pb
content = _pb.load_soul_md(200000)
print("SOUL.md loaded:", bool(content), len(content or ""))
```

Pass no `config=` override when verifying — a hand-built dict omits the keys you
just set and reproduces the bug you are trying to rule out. Let it read the
real config from disk.

## Rules for writing the instruction

- **Imperative and specific.** "For any non-trivial code task, load skill X and
go to Antigravity" beats "prefer delegating". A model follows a named skill and a
named tool; it rationalises past a preference.
- **Name the tools and the order.** `agy_route` before `agy_send_message`; never
queue onto a RUNNING conversation; read `agy_get_trajectory` before declaring
done.
- **State the fallback explicitly.** "If the bridge is unreachable, tell the user
instead of doing it yourself" — otherwise the free session silently absorbs the
work the moment the bridge is down, which is the exact failure mode.
- **Give the exemption.** Trivial one-liners and Q&A are not delegated; without
that line the rule reads as absolute and gets ignored as impractical.
- **Keep `SOUL.md` identity intact** when editing — append the rule, don't
replace the existing identity paragraph.

## Session-scope caveat

The system prompt is assembled once per session to protect prompt caching. A rule
pinned now applies to **new sessions**; the session already running will not pick
it up mid-conversation. Tell the user to start a new conversation when the fix
is configuration, and say so plainly rather than implying it takes effect
immediately.

## Editing `~/.hermes/config.yaml`
The agent is blocked from writing that file directly (security-sensitive). Use
`hermes config set <key> <value> [--force]` for nested/unknown keys. `SOUL.md`
and project context files are ordinary files — write them normally.

After a config change, confirm the YAML still parses before trusting the
session that reads it.
