---
title: Diretriz de Economia de Tokens
tags: [tokens, rules, prompt-engineering, context]
created: 2026-11-05
---

# 🛡️ Diretriz de Economia de Tokens

Documento normativo para manter as sessões de desenvolvimento com IA ágeis, precisas e econômicas no consumo de tokens de contexto.

- Retorno: [[MOC - Otimização e Tokens]]
- Baseado nas Skills: `optimize-tokens` e `plan-token-optimization`

---

## 📏 1. A Regra dos 300 Linhas (300-Line Rule)
Nenhum arquivo de código C# ou módulo deve ultrapassar **300 linhas**.
- Arquivos extensos obrigam a IA a carregar milhares de tokens repetidamente.
- Ao identificar arquivos acima desse limite, execute um plano de particionamento (veja [[Plano de Refatoração de Scripts Longos]]).

## 🧩 2. Separação Estrita de Responsabilidades
- **UI / Feedback**: Lógica visual (ex: [[Script - PickableObject e Prompt]]) não deve poluir a lógica de colisão ou física.
- **Modelos / Tipos**: Enums como `PrismColorMode` devem idealmente residir em arquivos de tipos independentes.
- **Raycast & Efeitos**: Lógica de cálculo matemático de raios deve ser separada da renderização de linhas/partículas.

## 🧠 3. Navegação Via MOC e Links do Obsidian
- Em vez de pedir leitura ampla de diretórios inteiros, a IA e o desenvolvedor devem navegar usando o grafo de MOCs (`[[000 - Home MOC]]`).
- Cada nota é atômica, contendo apenas as informações essenciais para a tarefa em andamento.
