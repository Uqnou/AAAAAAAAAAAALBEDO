# Detecting a Unity Project

The `unity-dev-cycle` skill must auto-detect whether the working directory is a
Unity project, regardless of platform (mobile, VR, Mac, Windows, 2D, 3D).

## Detection logic

A Unity project is identified by the presence of a `ProjectSettings/` directory
containing a `ProjectVersion.txt` file. This is the canonical marker — every
Unity project has it, regardless of target platform or Unity version.

### Check order

1. **Look for `ProjectSettings/ProjectVersion.txt`** in the current working
   directory. If found, this is a Unity project.
2. If not found, walk up the directory tree (parent directories) looking for
   the same marker. Unity projects are sometimes opened from a subdirectory
   (e.g., `Assets/Scripts/`).
3. If not found anywhere, this is not a Unity project — use `dev-cycle` instead.

### What to read once detected

- `ProjectSettings/ProjectVersion.txt` — contains the Unity version (e.g.,
  `m_EditorVersion: 6.0.0f1`). This tells you which Editor version the project
  requires.
- `Packages/manifest.json` — lists all installed packages (including
  `com.unity.pipeline` if installed). This tells you what dependencies are
  available.
- `Assets/` directory structure — understand the project layout before
  planning.

### Platform-agnostic

The detection does NOT check:
- Target platform (iOS, Android, Windows, Mac, Linux, WebGL, VR, etc.)
- Project type (2D, 3D, URP, HDRP, Built-in)
- Unity version (6.0, 6.1, 6.6, etc.)

All of these are valid Unity projects and should all trigger this skill.

### Example detection (Python)

```python
import os
from pathlib import Path

def is_unity_project(start_dir="."):
    """Walk up from start_dir looking for ProjectSettings/ProjectVersion.txt."""
    current = Path(start_dir).resolve()
    for directory in [current] + list(current.parents):
        marker = directory / "ProjectSettings" / "ProjectVersion.txt"
        if marker.exists():
            return True, directory
    return False, None
```

### Example detection (shell)

```bash
# Check if current directory is a Unity project
if [ -f "ProjectSettings/ProjectVersion.txt" ]; then
    echo "Unity project detected"
    cat ProjectSettings/ProjectVersion.txt
fi
```
