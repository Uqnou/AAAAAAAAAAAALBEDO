---
title: Script - IsometricCamera
tags: [script, unity, camera, isometric]
created: 2026-11-05
---

# 🎥 Script: `IsometricCamera.cs`

- **Arquivo no Projeto**: `Assets/Scripts/IsometricCamera.cs`
- **Linhas**: ~60 linhas
- **Retorno**: [[MOC - Arquitetura e Scripts]]

---

## ⚙️ Características Técnicas
- Marcação `[ExecuteAlways]` para atualizar o posicionamento e rotação também no modo de edição da Unity Scene View.
- Configura o modo ortográfico com `orthographicSize = 8f`.
- **Ângulos Fixos**:
  - `pitchAngle = 30°` (inclinação vertical clássica).
  - `yawAngle = 45°` (giro horizontal diagonal).
- Foca em um alvo de visualização fixo (`lookTarget = Vector3.zero`) a uma distância de `cameraDistance = 25f`.
