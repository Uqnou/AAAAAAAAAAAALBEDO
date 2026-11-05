---
title: Script - PrismaController
tags: [script, unity, prism, light, raycast, bottleneck]
created: 2026-11-05
---

# 💎 Script: `PrismaController.cs`

- **Arquivo no Projeto**: `Assets/Scripts/PrismaController.cs`
- **Linhas**: ~366 linhas ⚠️ *(Ultrapassa a regra das 300 linhas)*
- **Plano de Refatoração**: Ver [[Plano de Refatoração de Scripts Longos]]
- **Retorno**: [[MOC - Arquitetura e Scripts]]

---

## ⚙️ Responsabilidades
1. **Detecção de Luz**:
   - Detecta sobreposição com colisores Trigger de fontes luminosas ([[Script - LightSource]]).
2. **Cálculo Cromático**:
   - Processa a cor do feixe emitido conforme a enumeração `PrismColorMode` ([[Mecânica de Luz e Cores]]).
3. **Raycast e Projeção do Feixe**:
   - Dispara raycast contínuo à frente do prisma para renderizar o feixe visual (LineRenderer / Efeitos).
   - Detecta superfícies e componentes de sombra ([[Script - SombraController]]) para alterar seus estados.

> [!WARNING] Alerta de Token Efficiency
> Este é o maior arquivo de código do projeto atual (366 linhas). Seguindo a [[Diretriz de Economia de Tokens]], ele deve ser particionado em submódulos: emissão de feixe (Raycast/Rendering), cálculo de cores e detecção de gatilhos.
