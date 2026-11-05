# Regras do Projeto ALBEDO

## 1. Diretriz Obrigatória: Economia de Tokens e Eficiência de Contexto
Sempre que interagir, auditar, refatorar ou expandir o código ou documentação deste projeto, execute e respeite estritamente as diretrizes da skill **`optimize-tokens`** (`optimize-for-tokens`) e **`plan-token-optimization`**:

1. **Regra dos 300 Linhas (300-Line Rule)**:
   - Nenhum arquivo de código individual deve exceder 300 linhas de código.
   - Componentes ou classes que crescerem além de 300 linhas devem ser obrigatoriamente planejados e desacoplados em submódulos focados e especializados (ex: separar lógica de renderização, lógica de estado/eventos e tipos/configurações).

2. **Separação de Responsabilidades (Separation of Concerns)**:
   - Manter lógicas de mecânicas de jogo (física, gameplay, iluminação) isoladas de controladores genéricos.
   - Tipos compartilhados, Enums e DTOs devem ser mantidos em arquivos dedicados ou isolados em vez de inflar arquivos de comportamento MonoBehaviour.

3. **Arquitetura de Documentação Não-Monolítica**:
   - Evitar documentações monolíticas gigantescas.
   - Toda informação deve estar particionada e indexada no **Second Brain / Maps of Content (MOC)** utilizando links bidirecionais compatíveis com o Obsidian (`[[NomeDaNota]]`).
   - Antes de ler arquivos inteiros desnecessários, consulte o mapa de tópicos `000 - Home MOC` ou os MOCs de cada domínio para economizar janela de contexto (tokens).

4. **Planejamento Prévio de Refatoração**:
   - Ao identificar arquivos candidatos a split ou refatoração, siga o protocolo de `plan-token-optimization`: elabore um plano detalhado com impacto de contagem de linhas antes de aplicar as mudanças diretamente quando solicitado.
