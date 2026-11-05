# Adversarial review of your own uncommitted work

Use when the free session already made changes and the user wants an independent
check ("have <the executing agent> review the corrections"). This is the honest
way out of having violated the loop: you cannot un-make the edits cheaply, but you
can stop vouching for them yourself.

## When it applies

- The orchestrator implemented or fixed something itself and the result is still
  in the working tree, uncommitted.
- The user questions whether the work belonged to the executing agent.
- A self-authored verification run is the only evidence backing a claim.

## Prompt shape that produces real findings

State the setup explicitly, or the reviewer defaults to agreement:

1. **Own the position.** "Another agent wrote this diff; you have no context on
   how." Name that the reviewer's job is to find what is WRONG.
2. **List the claims under review** as numbered allegations, one line each, so
   each can get its own verdict.
3. **Point at the live tree, not your paste.** Give the repo path and tell it to
   run `git diff -- <files>` — it reads the real file, catches drift, and greps
   for consequences your excerpt hid. Inline the diff too when it is small.
4. **Ask per-section verdicts**: CORRECT / INCORRECT / PARTIALLY CORRECT, each
   with `file:line` evidence. Demand that a bare "looks fine" is worthless.
5. **Include the refutation angle**: "if any correction INTRODUCED a regression,
   that is the most important finding — say so explicitly."
6. **Name your own weakest spots** as questions. Reviewers dig hardest where the
   prompt concedes uncertainty.
7. **Close with a prioritised list**: what must be fixed before commit, ordered.

## Reading the verdict back

`agy_get_trajectory` on a long conversation returns the whole history and can
exceed the tool's inline cap, spilling to a file. Work from the spillover file,
not by re-calling the API:

- The file is JSON; a fresh `json.loads` fails when it was truncated mid-string.
  Search the raw text for a unique marker from your prompt to locate the reply
  region, then read forward with offsets.
- The agent's report lives in `"prompt"` fields alongside tool output; section
  headers (`#### A.`) mark where the report starts.
- Step count plus status IDLE means it finished; steps advancing while RUNNING
  means it is reading code, not just composing.

## Traps that make self-verification worthless

These are the failure modes to guard the reviewer against — and to assume you
already committed:

- **Verify the code path your diff actually touched.** Test the sibling handler
  you did not fix; that is where the introduced bug hides. A green run over only
  the functions you authored proves nothing about the ones you edited in passing.
- **A stubbed harness can silently skip the bug.** If `getElementById` returns
  null, a modal function returns early and never reaches the broken lines —
  "NO CRASH" is a false negative, not a pass. Make the stub satisfy the
  function's own guards so the suspect lines actually execute.
- **Enumerate the class, don't sample it.** Grep for the pattern (every raw
  interpolation of a user-supplied field) instead of fixing the sites you
  happened to read, and check the branch you did not inspect.
- **Check what depends on what you delete.** Grep `.git/hooks/` and CI configs
  for the path before removing a file, and read the repo's AGENTS.md
  file-ownership map — a file no app references can still be a test gate or
  someone else's file.
- **Re-derive the consequence, don't trust your intent.** A hardcoded constant
  replaced in the calculation but left in the label is still a mismatch; a modal
  that always assumes one branch (early vs late) is wrong on the other.
