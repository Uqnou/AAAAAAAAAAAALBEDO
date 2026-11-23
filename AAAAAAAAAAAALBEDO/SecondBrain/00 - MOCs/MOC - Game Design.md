---
title: MOC - Game Design
tags: [moc, gamedesign, mechanics]
created: 2026-11-05
---

# 🎮 MOC — Game Design

Mapa de conteúdo focado na concepção do jogo, pilares estéticos, regras de interação e dinâmicas de quebra-cabeça.

- Retornar ao Início: [[000 - Home MOC]]

---

## 📌 Documentos de Design
- [[Game Overview e Conceito]]: Sinopse, visão estética isométrica e objetivo do jogador.
- [[Mecânica de Luz e Cores]]: Como funcionam as fontes de luz, filtros de cores e o espectro cromático.
- [[Mecânica de Sombras e Puzzles]]: O comportamento das sombras que bloqueiam caminhos e se dissipam com a luz direcionada.
- [[Mecânica de Vitrais e Divisores Cromáticos]]: Filtragem subtrativa RGB e divisão de feixes em vitrais e cristais fixos.
- [[Level Design - Fase 2 O Claustro dos Vitrais]]: Blueprint completo da Fase 2 integrando vitrais, divisores, Fogo Negro e slots de mesa.

---

## 🔗 Relação com Sistemas e Código
- Mecânica de luz implementada por: [[Script - LightSource]] e [[Script - PrismaController]]
- Mecânica de vitrais e slots implementada por: [[Script - ChromaticFilterGate]]
- Mecânica de sombras implementada por: [[Script - SombraController]]
- Movimentação e interação por: [[Script - PlayerController]] e [[Script - PickableObject e Prompt]]
