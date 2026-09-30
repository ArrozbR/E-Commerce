---
description: Retoma o trabalho no projeto, lendo o estado atual e propondo o próximo passo
allowed-tools: Read, Glob, Grep, Bash(git status:*), Bash(git log:*), Bash(git diff:*), Bash(git branch:*)
---

Você está iniciando uma sessão de trabalho no KeycapStore. O autor trabalha de 2 a 3 horas por dia útil, então o objetivo é recuperar o contexto em poucos minutos e começar a trabalhar.

Faça, nesta ordem:

1. Leia `docs/STATUS.md` inteiro: marco atual, última sessão, próximos passos, bloqueios.
2. Rode `git status`, `git branch --show-current` e `git log --oneline -10`. Se houver mudanças não commitadas, avise, porque o autor pode ter esquecido de commitar o trabalho da sessão anterior.
3. Liste `docs/decisoes/` e leia só os ADRs relevantes para o próximo passo.
4. Se o próximo passo tocar em pagamento, pedido ou estoque, releia `.claude/rules/pagamentos.md`.

Depois, responda de forma **curta**, com:

- **Onde paramos:** marco atual e o que foi feito na última sessão (2 ou 3 linhas).
- **Pendências:** mudanças não commitadas, bloqueios, questões em aberto que afetam o próximo passo.
- **Proposta para hoje:** 1 ou 2 tarefas que caibam em cerca de 2 horas, cada uma com um critério de pronto verificável. Se o marco atual tiver passado da estimativa em `docs/STATUS.md`, aponte isso.
- **Pergunta de aquecimento** (opcional): uma pergunta curta sobre um conceito que será usado hoje, para o autor explicar com as próprias palavras antes de começar.

Termine perguntando o que o autor quer fazer. **Não comece a implementar antes da resposta.**

$ARGUMENTS
