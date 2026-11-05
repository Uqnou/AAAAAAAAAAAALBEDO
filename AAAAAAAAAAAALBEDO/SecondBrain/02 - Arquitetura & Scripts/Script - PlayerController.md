---
title: Script - PlayerController
tags: [script, unity, player, input, csharp]
created: 2026-11-05
---

# 🕹️ Script: `PlayerController.cs`

- **Arquivo no Projeto**: `Assets/Scripts/PlayerController.cs`
- **Linhas**: ~174 linhas (Compatível com a [[Diretriz de Economia de Tokens]])
- **Retorno**: [[MOC - Arquitetura e Scripts]]

---

## ⚙️ Responsabilidades
1. **Movimentação Isométrica**:
   - Utiliza as teclas WASD via New Input System (`Keyboard.current`).
   - Converte os eixos do teclado para o ângulo da câmera ortográfica isométrica (rotacionada a 45 graus).
2. **Física & Gravidade**:
   - Usa `CharacterController` para deslocamento suave e aplicação de gravidade vertical.
3. **Interação de Pegar / Soltar**:
   - Raio de detecção de objetos próximos (`interactionRadius = 2.5f`).
   - Carrega objetos na frente do jogador utilizando um `HoldPoint`.
   - Comunica com [[Script - PickableObject e Prompt]] ao pressionar `[E]`.
