# Unity CLI Reference for Dev Cycle

Complete reference for the Unity CLI commands used in the `unity-dev-cycle`
workflow. The Unity CLI is the bridge between the Antigravity agent and the
Unity Editor.

## Prerequisites

- Unity 6.0 or later installed
- Unity CLI installed (comes with Unity Hub, or install standalone)
- Unity Pipeline package installed in the project (`unity pipeline install`)
- Unity Editor running (or headless batchmode)

## Core commands

### Project management

```bash
# Open a project (starts Editor with GUI)
unity open <project-path>

# Open headless (no window, no GPU — ideal for CI/agents)
unity open <project-path> --args "-batchmode -nographics"

# Open with custom args
unity open <project-path> --args "-batchmode -nographics -logFile /tmp/unity.log"
```

### Editor commands

```bash
# List all available commands in the connected Editor
unity command

# List commands as JSON (for agents/tooling)
unity command --format json

# Execute a registered command
unity command <command-name> [args]

# Evaluate C# expression in the running Editor (REPL)
unity command eval "return Application.version;"

# Evaluate with complex expressions
unity command eval "return GameObject.Find(\"Player\").transform.position;"

# Run tests
unity command run_tests
```

### Pipeline package

```bash
# Install Pipeline package (one-time setup per project)
unity pipeline install

# Check Pipeline package status
unity pipeline list

# Upgrade Pipeline package
unity pipeline upgrade
```

### Editor management

```bash
# List installed editors
unity editors --format json

# Install a specific Unity version
unity install 6.0.0f1

# Install with modules (e.g., Android, iOS)
unity install 6.0.0m1 --module android --module ios

# List running editor instances
unity editors --running
```

### Authentication

```bash
# Sign in (required for some operations)
unity auth login

# Check auth status
unity auth status

# Sign out
unity auth logout
```

### MCP server

```bash
# Start MCP server for AI agents
unity mcp

# Configure agent clients to use the MCP server
unity mcp --install
```

## Common patterns for Antigravity

### Compile check

After writing C# code, the Antigravity agent compiles by evaluating a trivial
expression:

```bash
unity command eval "return true;"
```

If there are compile errors, the output will contain the error messages. The
agent reads these and fixes the code.

### Run a specific test

```bash
unity command run_tests --filter "MyTestClass"
```

### Get project info

```bash
unity command eval "return Application.unityVersion;"
unity command eval "return Application.dataPath;"
unity command eval "return Application.platform;"
```

### Create a GameObject

```bash
unity command create_gameobject --name "MyObject"
```

### Find GameObjects

```bash
unity command find_gameobjects --name "Player"
```

### Set transform

```bash
unity command set_transform --name "Player" --position "0,0,0" --rotation "0,0,0"
```

## Output formats

Most commands support structured output:

```bash
# JSON output
unity command --format json

# TSV output
unity command --format tsv
```

## Exit codes

- `0` — success
- Non-zero — failure (check output for error details)

## Headless batchmode

For CI and agent workflows, always use headless mode:

```bash
unity open <project> --args "-batchmode -nographics"
```

- `-batchmode` — no GUI, no input
- `-nographics` — no GPU, no rendering (faster, but no Game view)

If you need rendering (e.g., for visual tests), omit `-nographics`:

```bash
unity open <project> --args "-batchmode"
```

## Troubleshooting

### "No connected Editor"

The Unity CLI requires a running Editor. Start one first:

```bash
unity open <project> --args "-batchmode -nographics"
```

### "Pipeline package not installed"

Install it first:

```bash
unity pipeline install
```

### "Unity version mismatch"

The project's Unity version must match an installed Editor. Check:

```bash
cat ProjectSettings/ProjectVersion.txt
unity editors --format json
```

If missing, install the correct version:

```bash
unity install <version>
```
