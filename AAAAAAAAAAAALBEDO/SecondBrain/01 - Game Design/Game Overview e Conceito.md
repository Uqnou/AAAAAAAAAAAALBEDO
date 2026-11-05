---
title: Game Overview e Conceito
tags: [concept, gamedesign, narrative]
created: 2026-11-05
---

# 📖 Game Overview e Conceito

O projeto **ALBEDO** é um jogo de puzzle e exploração em perspectiva isométrica construído na Unity (URP).

- Retorno: [[MOC - Game Design]]

---

## 🎯 Conceito Central
O termo *Albedo* refere-se à medida de refletividade de uma superfície. No jogo, a luz e a reflexão são os elementos-chave para avançar no ambiente. O jogador explora salas e cenários manipulando a direção e a cor dos feixes de luz por meio de prismas móveis.

## 🕹️ Loop Principal de Gameplay
1. O jogador navega pelo cenário com movimentação isométrica WASD: [[Script - PlayerController]].
2. Identifica obstáculos sólidos feitos de sombras densas: [[Mecânica de Sombras e Puzzles]].
3. Localiza prismas e fontes luminosas: [[Mecânica de Luz e Cores]].
4. Carrega os prismas até a luz usando a tecla de interação [E]: [[Script - PickableObject e Prompt]].
5. Alinha o feixe de luz gerado pelo prisma contra a sombra para torná-la transponível e abrir o caminho: [[Script - SombraController]].
