---
title: Script - SombraController
tags: [script, unity, shadow, trigger, events]
created: 2026-11-05
---

# 👤 Script: `SombraController.cs`

- **Arquivo no Projeto**: `Assets/Scripts/SombraController.cs`
- **Linhas**: ~135 linhas (Conforme a [[Diretriz de Economia de Tokens]])
- **Retorno**: [[MOC - Arquitetura e Scripts]]
- **Conceito de Design**: [[Mecânica de Sombras e Puzzles]]

---

## ⚙️ Funcionamento Técnico
- Faz cache de todos os `Renderer` e `Collider` associados ao objeto ou seus filhos.
- Mantém um `HashSet<PrismaController>` para registrar múltiplos prismas que possam estar iluminando a mesma sombra simultaneamente.
- Quando a contagem de prismas ativos for maior que zero:
  - Alterna `Collider.isTrigger = true`.
  - Desativa `Renderer.enabled = false`.
  - Invoca `onShadowHidden`.
- Quando todos os prismas deixam de iluminar a sombra:
  - Restaura `isTrigger` original.
  - Reativa os renderers e invoca `onShadowRevealed`.
