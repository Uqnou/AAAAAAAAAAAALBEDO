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

---

## 🗺️ Campanha (Tutorial + 3 Fases)
1. **Índice 0 — Novo Tutorial**: [[Level Design - Tutorial (Novo)]] *(Linear em 2 salas: luz branca e cor exata)*
2. **Índice 1 — Fase 1 (Antigo Tutorial)**: Fogo Negro, Pontes e Prismas Coloridos *(correção de exploit da Sala 3 planejada)*
3. **Índice 2 — Fase 2 (Claustro dos Vitrais)**: [[Level Design - Fase 2 O Claustro dos Vitrais]] *(auditoria em [[Auditoria - Fase 1 Claustro dos Vitrais (Lente Portal)]])*
4. **Índice 3 — Fase 3 (Grand Finale)**: [[Level Design - Fase 3 (Grand Finale)]] *(Hub Central em Loop Duplo / Backtracking)*

---

## 📐 Estudos, Frameworks e Templates de Level Design
- [[Estudo - Level Design de Portal 2 (Abram e Valve)]]: Princípios estruturais de *Portal 2* e do estudo de caso *Portal2Chambers* (topologia, ritmo, *sightlines*, fiação visual e moderação de ruído).
- [[Framework - Level Design de Puzzles no ALBEDO]]: Matriz de interações do ALBEDO, curva de aprendizado e diretrizes de leitura espacial isométrica.
- [[Auditoria - Fase 1 Claustro dos Vitrais (Lente Portal)]]: Diagnóstico estrutural da Fase 1 atual com backlog priorizado de melhorias.
- [[Template - Blueprint de Câmara de Puzzle]]: Ficha reutilizável com *beats*, design reverso, grafo de dependências e checklist anti-softlock.

---

## 🔗 Relação com Sistemas e Código
- Mecânica de luz implementada por: [[Script - LightSource]] e [[Script - PrismaController]]
- Mecânica de vitrais e slots implementada por: [[Script - ChromaticFilterGate]]
- Mecânica de sombras implementada por: [[Script - SombraController]]
- Movimentação e interação por: [[Script - PlayerController]] e [[Script - PickableObject e Prompt]]
