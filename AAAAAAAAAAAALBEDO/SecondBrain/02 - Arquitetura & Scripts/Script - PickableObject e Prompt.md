---
title: Script - PickableObject e Prompt
tags: [script, unity, interaction, ui]
created: 2026-11-05
---

# 📦 Scripts: `PickableObject.cs` & `InteractionPrompt.cs`

- **Arquivos no Projeto**: 
  - `Assets/Scripts/PickableObject.cs` (~73 linhas)
  - `Assets/Scripts/InteractionPrompt.cs` (~56 linhas)
- **Retorno**: [[MOC - Arquitetura e Scripts]]

---

## ⚙️ `PickableObject.cs`
- Requer `Rigidbody` e `Collider`.
- Gerencia o estado físico quando o objeto é pego pelo jogador (`IsPickedUp`):
  - Ao ser pego: desativa gravidade/física e se conecta ao `HoldPoint` do [[Script - PlayerController]].
  - Ao ser solto: restaura física e aplica um pequeno impulso à frente (`dropForwardForce`).

## 💬 `InteractionPrompt.cs`
- Exibe o indicador ou prompt flutuante de interação para avisar que a tecla `[E]` pode ser pressionada quando o jogador está próximo.
