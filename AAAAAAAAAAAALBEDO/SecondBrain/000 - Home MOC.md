---
title: Home MOC - ALBEDO
tags: [moc, second-brain, index, hub]
created: 2026-11-05
---

# 🌐 ALBEDO — Second Brain (Master MOC)

Bem-vindo ao **Second Brain** do projeto **ALBEDO**, estruturado com arquitetura **MOC (Map of Content)** para visualização perfeita no grafo do **Obsidian** e consulta ultra-eficiente de informações.

```mermaid
graph TD
    HOME["000 - Home MOC"] --> MOC_GD["[[MOC - Game Design]]"]
    HOME --> MOC_ARCH["[[MOC - Arquitetura e Scripts]]"]
    HOME --> MOC_ASSETS["[[MOC - Assets e Cenário]]"]
    HOME --> MOC_OPT["[[MOC - Otimização e Tokens]]"]

    MOC_GD --> GD_CORE["[[Game Overview e Conceito]]"]
    MOC_GD --> GD_LIGHT["[[Mecânica de Luz e Cores]]"]
    MOC_GD --> GD_SHADOW["[[Mecânica de Sombras e Puzzles]]"]

    MOC_ARCH --> SC_PLAYER["[[Script - PlayerController]]"]
    MOC_ARCH --> SC_PRISMA["[[Script - PrismaController]]"]
    MOC_ARCH --> SC_SOMBRA["[[Script - SombraController]]"]
    MOC_ARCH --> SC_LIGHT["[[Script - LightSource]]"]
    MOC_ARCH --> SC_PICK["[[Script - PickableObject e Prompt]]"]
    MOC_ARCH --> SC_CAM["[[Script - IsometricCamera]]"]

    MOC_OPT --> RULE_TOKENS["[[Diretriz de Economia de Tokens]]"]
    MOC_OPT --> PLAN_REFAC["[[Plano de Refatoração de Scripts Longos]]"]
```

---

## 🗺️ Mapas de Conteúdo (MOCs Centrais)

Navegue rapidamente pelos tópicos do projeto:

1. **Game Design & Conceito**: [[MOC - Game Design]]
   - Ideia central, atmosfera, mecânicas de refração e luz, regras das sombras.
2. **Arquitetura de Código & Scripts**: [[MOC - Arquitetura e Scripts]]
   - Componentes C#, interações, ciclo de gameplay e controladores.
3. **Cenário, Modelos & Arte**: [[MOC - Assets e Cenário]]
   - Modelos FBX (`CenaAjeitada`, `prismateste`), materiais e iluminação isométrica.
4. **Governança, Performance & Tokens**: [[MOC - Otimização e Tokens]]
   - Aplicação da skill de economia de tokens, limite de 300 linhas e plano de modularização.

---

## ⚡ Atalhos Rápidos
- Regra de Otimização Ativa: [[Diretriz de Economia de Tokens]]
- Script mais complexo: [[Script - PrismaController]]
- Setup de Câmera: [[Script - IsometricCamera]]
