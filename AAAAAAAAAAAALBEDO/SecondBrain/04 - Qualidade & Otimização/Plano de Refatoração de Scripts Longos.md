---
title: Plano de Refatoração de Scripts Longos
tags: [optimization, refactoring, plan, tokens]
created: 2026-11-05
---

# 🛠️ Plano de Refatoração de Scripts Longos

Elaborado conforme o protocolo da skill `plan-token-optimization`.

- Retorno: [[MOC - Otimização e Tokens]]
- Referência: [[Diretriz de Economia de Tokens]]

---

## 📊 Auditoria Atual da Codebase

| Arquivo | Linhas | Classificação | Status | Ação Recomendada |
| :--- | :--- | :--- | :--- | :--- |
| `PrismaController.cs` | **366** | Monólito de Gameplay/Render | ⚠️ Excede 300 | Aplicar particionamento em 3 módulos menores |
| `PlayerController.cs` | 174 | Aceitável | ✅ OK | Manter monitorado |
| `SombraController.cs` | 135 | Aceitável | ✅ OK | Conforme |
| `LightSource.cs` | 76 | Enxuto | ✅ OK | Conforme |
| `PickableObject.cs` | 73 | Enxuto | ✅ OK | Conforme |
| `IsometricCamera.cs` | 60 | Enxuto | ✅ OK | Conforme |
| `InteractionPrompt.cs`| 56 | Enxuto | ✅ OK | Conforme |

---

## 🎯 Proposta de Modularização para `PrismaController.cs` (366 Linhas)

O `PrismaController` atualmente acumula 3 funções distintas:
1. Detecção física de contato com `LightSource`.
2. Processamento do cálculo de mistura de cores (`PrismColorMode`).
3. Raycast e renderização visual do feixe de luz atingindo `SombraController`.

### Particionamento Proposto:
1. `PrismColorMode.cs` (~30 linhas): Isolamento do enum e métodos utilitários de cor.
2. `PrismBeamEmitter.cs` (~160 linhas): Responsável pelo Raycast e pelo LineRenderer do feixe.
3. `PrismaController.cs` (~150 linhas): Controlador principal reduzido, gerenciando apenas os gatilhos e orquestrando o feixe.

*Impacto estimado: Redução do arquivo principal de 366 linhas para ~150 linhas, zerando qualquer gargalo de contexto.*
