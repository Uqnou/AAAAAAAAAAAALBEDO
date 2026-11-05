---
title: Mecânica de Luz e Cores
tags: [mechanics, light, colors, puzzles]
created: 2026-11-05
---

# 💡 Mecânica de Luz e Cores

A mecânica de luz do ALBEDO define como feixes luminosos interagem com os objetos do cenário e prismas.

- Retorno: [[MOC - Game Design]]
- Implementação: [[Script - LightSource]] e [[Script - PrismaController]]

---

## 🌈 Modos de Combinação de Cores (`PrismColorMode`)
Quando um prisma entra em contato com um volume de luz, ele calcula a cor resultante do feixe projetado de acordo com seu modo configurado:

- **`PrismTint`**: Se o prisma tiver uma cor diferente de branco, ela tinge a luz. Se for branco, reflete a cor original da fonte.
- **`Multiply`**: Multiplica matematicamente a cor física da luz pela cor do prisma.
- **`PrismOverrides`**: Prioriza sempre a cor do prisma caso não seja branco.
- **`LightColorDirect`**: Emite diretamente a cor original da fonte luminosa incidente.

## 🔦 Projeção do Feixe
- O feixe só é emitido enquanto o prisma permanecer em contato com o colisor trigger da fonte ([[Script - LightSource]]).
- Ao ser desconectado da luz, o feixe é instantaneamente extinto.
