---
title: Script - LightSource
tags: [script, unity, light, source]
created: 2026-11-05
---

# ☀️ Script: `LightSource.cs`

- **Arquivo no Projeto**: `Assets/Scripts/LightSource.cs`
- **Linhas**: ~76 linhas
- **Retorno**: [[MOC - Arquitetura e Scripts]]
- **Conceito de Design**: [[Mecânica de Luz e Cores]]

---

## ⚙️ Características
- Define a cor da luz emitida (`lightColor`).
- Suporta sincronização automática com a cor da luz padrão da Unity (`Light`) se presente no GameObject.
- Garante automaticamente que o seu `Collider` seja um Trigger para permitir a entrada física de prismas móveis ([[Script - PrismaController]]).
