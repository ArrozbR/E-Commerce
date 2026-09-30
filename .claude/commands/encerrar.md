---
description: Encerra a sessão, atualiza docs/STATUS.md e lista o que o autor precisa commitar
allowed-tools: Read, Edit, Write, Glob, Grep, Bash(git status:*), Bash(git diff:*), Bash(git log:*)
---

Você está encerrando uma sessão de trabalho no KeycapStore. **Nunca execute `git add`, `git commit` ou `git push`.** Quem commita é o autor.

Faça, nesta ordem:

1. Rode `git status` e `git diff --stat` para ver o que mudou na sessão.
2. Pergunte ao autor, **numa única mensagem**:
   - quantas horas ele trabalhou hoje;
   - se alguma coisa ficou confusa e deve ser revisada na próxima sessão.

   Se o autor já tiver passado essas informações em `$ARGUMENTS`, não pergunte de novo.
3. Atualize `docs/STATUS.md`:
   - **Marco atual** e checklist do marco (marque o que foi concluído, e só o que tem critério de pronto verificado).
   - **Horas acumuladas** no marco e no total (some as de hoje).
   - Adicione uma entrada no topo do **Diário de sessões**: data, horas, o que foi feito, o que ficou confuso.
   - Reescreva **Próximos passos** (no máximo 3, concretos e pequenos).
   - Atualize **Bloqueios** e **Questões em aberto**.
   - Mantenha o arquivo enxuto: o diário guarda só as **10 sessões mais recentes**. As mais antigas são resumidas em uma linha em "Histórico".
4. Se uma **decisão** foi tomada na sessão (escolha entre alternativas, mudança de escopo, nova dependência), proponha um ADR em `docs/decisoes/` usando `docs/decisoes/TEMPLATE.md`, e pergunte se o autor quer escrevê-lo com as próprias palavras ou se prefere que você faça um rascunho para ele revisar.
5. Responda com:
   - resumo da sessão (3 linhas no máximo);
   - **arquivos para commitar**, agrupados por assunto;
   - **sugestão de mensagem de commit** no formato Conventional Commits, em português (ex.: `feat(orders): reserva atômica de estoque no checkout`);
   - lembrete de rodar os testes localmente antes do push, se houver código alterado.

$ARGUMENTS
