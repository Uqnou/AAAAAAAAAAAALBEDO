---
title: Script - CameraTriggerZone
tags: [script, unity, camera, trigger, zones]
created: 2026-11-05
---

# 📹 Script: `CameraTriggerZone.cs`

- **Arquivo no Projeto**: `Assets/Scripts/CameraTriggerZone.cs`
- **Linhas**: ~125 linhas (Conforme à [[Diretriz de Economia de Tokens]])
- **Retorno**: [[MOC - Arquitetura e Scripts]]
- **Relacionado com**: [[Script - IsometricCamera]]

---

## ⚙️ Visão Geral
Componente acoplado a qualquer GameObject com `Collider` (configurado como Trigger) que detecta a passagem do jogador (`PlayerController` ou tag `Player`) e atualiza suavemente as propriedades da câmera isométrica.

### Parâmetros Configuráveis no Inspector:
- **`Target Pitch Angle`**: Inclinação vertical (ex: 30° ou 45°).
- **`Target Yaw Angle`**: Ângulo horizontal (ex: 45°, 135°, -45°).
- **`Target Orthographic Size`**: Zoom/enquadramento (ex: 5 para salas fechadas, 12 para áreas abertas).
- **`Target Focus Transform` / `Target Look Position`**: Ponto para onde a câmera deve apontar.
- **`Transition Duration`**: Tempo de transição suave em segundos.
- **`Revert On Exit`**: Se marcado, ao sair do volume da zona a câmera retorna aos valores anteriores.
- **`Gizmos Visuais`**: Desenha a área do volume na Scene View em ciano translúcido.
