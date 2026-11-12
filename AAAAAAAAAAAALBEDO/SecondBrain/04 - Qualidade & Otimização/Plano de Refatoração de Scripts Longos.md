---
title: Plano de Refatoração de Scripts Longos
tags: [optimization, refactoring, plan, tokens]
created: 2026-11-05
updated: 2026-11-12
---

# 🛠️ Plano de Refatoração e Auditoria de Scripts

Elaborado conforme o protocolo da skill `plan-token-optimization`.

- Retorno: [[MOC - Otimização e Tokens]]
- Referência: [[Diretriz de Economia de Tokens]]

---

## 📊 Auditoria Atual da Codebase

| Arquivo | Linhas | Classificação | Status | Situação |
| :--- | :--- | :--- | :--- | :--- |
| `GodRayBeam.cs` | 286 | Enxuto / Modular | ✅ Conforme | Sob a regra dos 300 |
| `PrismaController.cs` | 280 | Refatorado / Otimizado | ✅ Conforme | Reduzido de 366 para 280 linhas |
| `PlayerController.cs` | 174 | Aceitável | ✅ Conforme | Enxuto |
| `SombraController.cs` | 135 | Aceitável | ✅ Conforme | Enxuto |
| `CameraTriggerZone.cs` | 125 | Enxuto | ✅ Conforme | Enxuto |
| `LightSource.cs` | 76 | Enxuto | ✅ Conforme | Enxuto |
| `PickableObject.cs` | 73 | Enxuto | ✅ Conforme | Enxuto |
| `IsometricCamera.cs` | 60 | Enxuto | ✅ Conforme | Enxuto |
| `InteractionPrompt.cs`| 56 | Enxuto | ✅ Conforme | Enxuto |

---

## 🎯 Histórico de Refatoração Concluída
O `PrismaController.cs` foi refatorado e reduzido de **366 linhas** para **280 linhas**, integrando emissão de God Ray volumétrico, simplificação dos manipuladores de colisão/trigger e auto-vinculação modular com a classe `GodRayBeam`.
