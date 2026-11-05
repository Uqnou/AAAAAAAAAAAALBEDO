# Skills de Economia de Tokens para Antigravity

Este repositório contém duas "Skills" personalizadas para o Antigravity, projetadas para ajudar a manter o seu projeto eficiente em termos de uso de tokens, auditando e refatorando arquivos grandes ou documentações monolíticas.

## Skills Incluídas

### 1. `optimize-for-tokens`
- **O que faz**: Realiza uma auditoria completa na estrutura de arquivos e documentação do projeto.
- **Ação**: Identifica e **aplica refatorações diretas** (ex: divide arquivos com mais de 300 linhas, separa lógica de UI em hooks, etc.).
- **Resultado**: Gera um arquivo `token_savings_analysis.md` com a economia estimada.

### 2. `plan-token-optimization`
- **O que faz**: Analisa o projeto em busca de gargalos de tokens, mas **não aplica alterações**.
- **Ação**: Gera um relatório detalhado e um `implementation_plan.md` com as etapas recomendadas.
- **Extra**: Cria uma tela visual premium em HTML (`optimization_summary.html`) que abre automaticamente no navegador para visualização de resultados esperados.

---

## Como Instalar no Antigravity

Para que o Antigravity (seu assistente de IA) possa utilizar estas skills, você deve disponibilizá-las no diretório de skills do seu projeto:

1. No seu projeto principal, crie (se não existir) a pasta `.agent/skills/`.
2. Copie as pastas `optimize-tokens` e `plan-token-optimization` deste repositório para dentro de `.agent/skills/`.
   - Certifique-se de que o arquivo `SKILL.md` esteja dentro de cada pasta respectiva.
3. Certifique-se de que o Antigravity tenha acesso de leitura ao seu projeto.

O diretório final deve se parecer com isto:
```
seu-projeto/
├── .agent/
│   └── skills/
│       ├── optimize-tokens/
│       │   └── SKILL.md
│       └── plan-token-optimization/
│           └── SKILL.md
├── src/
└── ...
```

---

## Como Usar (Prompts)

Uma vez instaladas, você pode acionar as skills usando prompts naturais no Antigravity:

- **Para Otimizar Diretamente**:
  - *"Otimizar tokens neste projeto"*
  - *"Reduzir context window"*
  - *"Analise o tamanho dos arquivos e otimize"*

- **Para Planejar a Otimização (Sem aplicar mudanças)**:
  - *"Planeje a otimização de tokens"*
  - *"Analise o uso de tokens e gere um relatório"*
  - *"Gerar relatório de economia de tokens"*

---

## Notas para o Agente
Cada skill segue um protocolo rigoroso baseado na **Regra das 300 Linhas** e na **Separação de Preocupações**, garantindo que o seu projeto permaneça focado e eficiente para sessões de codificação com IA.
