# Script - PrismaController

MOC: [[MOC - Arquitetura e Scripts]] | [[MOC - Game Design]]
Tópicos Relacionados: [[Script - GodRayBeam]], [[Script - LightSource]], [[Script - SombraController]], [[Mecânica de Luz e Cores]]

---

## 📌 Visão Geral
O `PrismaController.cs` gerencia o redirecionamento, difração, tingimento e reflexão de luz no jogo ALBEDO. Instancia o prefab oficial **`GodRay_Mystic.prefab`** e permite controle em tempo real sobre o **tamanho e alcance do God Ray** (`godRayTopRadius`, `godRayBottomRadius`, `godRayLength`), projetando o anel de luz (`GroundRing`) com precisão sobre qualquer objeto ou parede sólida impactada.

---

## ⚙️ Principais Funcionalidades

1. **Customização de Dimensões (`customizeGodRaySize`)**:
   - `godRayTopRadius`: Raio de abertura no início do cone no prisma.
   - `godRayBottomRadius`: Raio da base cônica e anel de impacto.
   - `godRayLength`: Alcance / comprimento máximo do feixe volumétrico.
   - Atualiza a geometria e o colisor em tempo real no Editor e durante o gameplay.

2. **Projeção de Anel em Objetos e Paredes**:
   - Ao colidir com qualquer geometria sólida (chão, parede, caixa, etc.), o anel de impacto (`GroundRing`) se apoia na superfície usando a normal de impacto (`hit.point + hit.normal * 0.02f` e `Quaternion.LookRotation(hit.normal, up)`).
   - O raycast filtra e ignora os colisores do próprio prisma e do próprio feixe, evitando bloqueios prematuros.

3. **Instanciação do Prefab Oficial (`godRayPrefab`)**:
   - Referencia `Assets/GodRay_Mystic.prefab` com material URP dedicado em `Assets/Materials/GodRay_GroundRing.mat`.
   - Ajusta a cor através de `prismGodRay.SetColor(activeEmittedColor)`.

4. **Suporte Nativo à Layer `Light` (`lightLayerMask`)**:
   - Ao colidir com qualquer objeto pertencente à layer `Light`, extrai a cor da fonte (`GodRayBeam`, `LightSource`, `Light` ou material do `Renderer`) e iguala a cor do feixe emitido diretamente à cor do objeto.

---

## 📊 Regra dos 300 Linhas
- **Linhas Atuais**: < 300 linhas (294 linhas).
- Em conformidade com a diretriz [[Diretriz de Economia de Tokens]].
