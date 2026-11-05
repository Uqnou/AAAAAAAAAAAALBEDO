---
description: Diretrizes obrigatórias de economia de tokens e limite de tamanho de arquivos
globs: ["**/*"]
alwaysApply: true
---

# Regra: Sempre Utilizar a Skill de Economia de Tokens

Esta regra é de aplicação contínua e prioritária para todo o projeto ALBEDO.

### Diretrizes:
1. **Ativação da Skill de Economia de Tokens**:
   - Utilize a mentalidade e os princípios das skills `optimize-tokens` e `plan-token-optimization`.
   - Limite estrito de 300 linhas por arquivo de código.
   - Qualquer arquivo que atinja ou se aproxime desse limite (como `PrismaController.cs` atualmente com 366 linhas) deve ser planejado para modularização e desacoplamento.

2. **Modularização e Consultas Eficientes**:
   - Para leitura de contexto, prefira buscar notas atômicas no `SecondBrain/` através dos MOCs (`[[000 - Home MOC]]`, `[[MOC - Arquitetura e Scripts]]`, etc.) em vez de inspecionar diretórios ou arquivos inteiros às cegas.
   - Não crie documentações extensas em um único arquivo; prefira notas modulares interconectadas no grafo Obsidian.
