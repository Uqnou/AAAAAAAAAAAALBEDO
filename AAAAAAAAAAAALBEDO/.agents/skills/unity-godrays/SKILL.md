---
name: unity-godrays
description: Cria e configura God Rays volumétricos verticais (do teto ao chão) ou angulados na Unity (URP e HDRP), com partículas de poeira suspensa (dust motes), demarcação de área no solo, física de raycast e cores dinâmicas customizáveis para mecânicas de gameplay. Use sempre que o usuário solicitar feixes de luz, volumetric light shafts, god rays ou áreas de luz interativas.
allowed-tools: WebFetch, WebSearch
metadata:
  version: 1.0.0
  author: "Antigravity"
---

# Unity God Rays & Volumetric Light Shafts

Este guia ensina a implementar, configurar e automatizar feixes de luz volumétricos (*God Rays*) verticais ou angulados na Unity, cobrindo tanto **Universal Render Pipeline (URP)** quanto **High Definition Render Pipeline (HDRP)**, com foco em estética, física de detecção e controle dinâmico de cores para mecânicas de jogo.

---

## 1. Escolha da Abordagem por Render Pipeline

| Pipeline | Abordagem Recomendada | Vantagens | Quando Usar |
| :--- | :--- | :--- | :--- |
| **URP / Mobile / Web** | **Malha Cônica + Shader HLSL Aditivo / Soft Depth Fade** + Partículas | Altíssima performance, estável em câmeras isométricas/ortográficas, sem dependência de pós-processamento, controle 100% artístico de cor. | Câmera isométrica, jogos estilizados, plataformas móveis e puzzles de gameplay. |
| **HDRP** | **Global Volume (Fog Override) + Spotlight com Volumetric Multiplier + Shadow Map** | Fisicamente correto, recorte automático por aberturas e esquadrias de janelas (Shadow Near Plane). | Jogos fotorrealistas de alta fidelidade para PC/Console com iluminação dinâmica pesada. |

---

## 2. Abordagem URP (Universal & Gameplay-Ready)

### 2.1 Componentes Essenciais da Entidade
Um God Ray completo para demarcação de área consiste em:
1. **Malha do Feixe (Cone / Cilindro Truncado)**:
   - Origem no teto (vértice superior estreito) alargando em direção ao piso.
   - Material com shader transparente aditivo que não projeta sombras nem escreve no Z-Buffer (`ZWrite Off`, `Cull Off`).
2. **Shader Volumétrico (HLSL / Shader Graph)**:
   - **Gradiente Vertical**: Interpolação de $\alpha$ entre $Y_{topo}$ e $Y_{base}$.
   - **Atenuação Fresnel / Rim**: `pow(1.0 - saturate(dot(normalWS, viewDirWS)), _RimPower)` para manter bordas macias sob qualquer ângulo de visão.
   - **Soft Depth Fade**: Amostragem de `_CameraDepthTexture` via `SampleSceneDepth()` para desvanecer o feixe ao tocar pisos ou obstáculos sólidos sem cortes duros.
3. **Partículas de Poeira (*Dust Motes*)**:
   - `ParticleSystem` embutido no cone com emissão por volume cônico/caixa, baixa velocidade com turbulência sutil e fade de opacidade ao longo da vida útil.
   - Cor inicial vinculada à cor do feixe.
4. **Anel de Demarcação no Solo**:
   - Mesh plano ou decalque circular no chão no ponto de impacto da luz, indicando o raio exato de interação.
5. **Colisor Físico de Trigger**:
   - `CapsuleCollider` ou `MeshCollider` com `isTrigger = true` cobrindo o volume do feixe para detectar entrada de jogadores ou objetos de puzzle.

### 2.2 Física de Raycast (Ancoragem Dinâmica Teto-ao-Chão)
Para que o feixe se adapte automaticamente a tetos de diferentes alturas e pisos desnivelados:
```csharp
RaycastHit hit;
if (Physics.Raycast(transform.position, Vector3.down, out hit, maxDistance, groundLayerMask))
{
    float currentHeight = hit.distance;
    // Ajusta a escala Y da malha cônica para encostar no chão
    beamTransform.localScale = new Vector3(groundRadius, currentHeight, groundRadius);
    // Posiciona o anel demarcador exatamente no ponto de impacto
    groundRing.position = hit.point + Vector3.up * 0.01f;
    // Atualiza o centro e altura do colisor físico de trigger
    triggerCollider.height = currentHeight;
    triggerCollider.center = new Vector3(0, -currentHeight * 0.5f, 0);
}
```

### 2.3 Controle Dinâmico de Cores sem Alocação de GC
Evite criar instâncias de material repetidas no `Update`. Utilize `MaterialPropertyBlock`:
```csharp
MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
propBlock.SetColor("_TintColor", rayColor * intensity);
meshRenderer.SetPropertyBlock(propBlock);

// Atualiza também a cor das partículas
var main = dustParticles.main;
main.startColor = new ParticleSystem.MinMaxGradient(rayColor);
```

---

## 3. Abordagem HDRP (Técnica do Cam Ayres)

Se o projeto estiver rodando em HDRP:
1. **Criar Global Volume**:
   - Adicionar override de **Fog**.
   - Habilitar `Enable Fog`, `Volumetric Fog` e aumentar o `Volumetric Fog Distance`.
2. **Configurar Spotlight no Teto/Janela**:
   - Apontar a luz através da abertura.
   - No Inspector do Light: expandir **Volumetrics** e aumentar o `Multiplier` (ex: 2 a 5).
   - **Fundamental**: Habilitar **Shadows** (Shadow Map).
   - Ajustar o **Near Plane** da sombra para garantir que o formato do buraco/janela recorte e divida os feixes de luz (*carving*).
3. **Adicionar Partículas de Poeira**:
   - Criar `VFX Graph` ou `ParticleSystem` com blend aditivo e turbulência lenta dentro da área iluminada.
4. **Animação de Intensidade**:
   - Utilizar o Unity `Timeline` ou script C# para oscilar suavemente a intensidade da luz.

---

## 4. Checklist de Validação
- [ ] O feixe não corta bruscamente a geometria ao tocar o chão (verificar se `Depth Texture` está habilitada no URP Asset).
- [ ] A cor do feixe, das partículas e do anel de demarcação mudam de forma sincronizada ao alterar a cor.
- [ ] O trigger físico responde com `OnTriggerEnter` e `OnTriggerExit` ao cruzar a coluna de luz.
- [ ] A altura da coluna se recalcula se o emissor for movido de altitude ou sobre um obstáculo.
