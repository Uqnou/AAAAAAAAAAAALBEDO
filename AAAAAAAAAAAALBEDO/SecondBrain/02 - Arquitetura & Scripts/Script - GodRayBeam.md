# Script - GodRayBeam

MOC: [[MOC - Arquitetura e Scripts]] | [[MOC - Game Design]]
Tópicos Relacionados: [[Script - PrismaController]], [[Script - LightSource]], [[Mecânica de Luz e Cores]]

---

## 📌 Visão Geral
O `GodRayBeam.cs` é o componente central para geração e controle procedural de feixes de luz volumétrica (God Rays) no ALBEDO, suportando orientação vertical, angular e horizontal contra paredes e obstáculos.

---

## ⚙️ Principais Funcionalidades

1. **Geração Procedural e Redimensionamento Dinâmico (`SetDimensions`)**:
   - Reconstrução dinâmica da malha cônica via `RebuildConeMesh()` e `SetDimensions(topR, botR, maxH)`.
   - Permite que controladores externos (como [[Script - PrismaController]]) ajustem o tamanho e comprimento do feixe em tempo de execução.

2. **Detecção e Ancoragem em Qualquer Superfície (Chão, Paredes e Obstáculos)**:
   - Raycast executado ao longo do vetor `-transform.up` ignorando triggers e colisores do próprio emissor / prisma pai.
   - O anel de impacto (`GroundRing`) alinha sua rotação pela normal da superfície impactada (`Quaternion.LookRotation(hit.normal, up)`), permitindo que se projete fielmente tanto em pisos horizontais quanto em paredes verticais ou rampas.
   - Fallback de material: Se o `GroundRing` não tiver material atribuído, inicializa dinamicamente o shader `Custom/GodRayGroundRing`.

3. **Sistema de Partículas de Poeira Volumétrica (Dust Particles)**:
   - Suporte a preenchimento volumétrico completo (`ConeVolume`), controle de raio (`dustRadiusFill`), altura (`dustHeightFill`) e offset vertical (`dustHeightOffset`).

---

## 📊 Regra dos 300 Linhas
- **Linhas Atuais**: < 300 linhas (295 linhas).
- Em conformidade com a diretriz [[Diretriz de Economia de Tokens]].
