# 0030. Backups na Cloudflare R2, com bucket lock no lugar da credencial só de escrita

- **Status:** Aceito
- **Data:** 2026-10-15
- **Relacionado:** ADR 0018 (completa a parte de backups e muda a "credencial só de escrita"), `docs/DEFINICOES.md` §10.6, D31, Q4; `docs/runbooks/backup.md`

## Contexto

O ADR 0018 decidiu guardar os backups em outro provedor (Cloudflare R2 ou Backblaze B2), com uma **credencial só de escrita**. Um invasor da VM não poderia ler nem apagar os backups. A escolha entre os dois ficou para a Q4.

Na documentação oficial, em 15/10/2026:
- **B2:** 10 GB grátis, e o cadastro diz "No credit card required".
- **R2:** 10 GB-mês, 1 milhão de operações classe A e 10 milhões classe B grátis por mês, e saída de dados grátis. Mas:
  - ativar a R2 **pede um cartão**, mesmo no plano grátis (relatos do fórum da Cloudflare, sem confirmação na documentação);
  - a Cloudflare **não tem teto de gasto**, só **alertas de orçamento**, que avisam e não pausam nada;
  - os tokens da R2 **não têm permissão "só escrita"**: as opções são leitura e escrita, ou só leitura, com escopo por bucket.

O autor preferiu a R2, com a condição de que **nunca** haja cobrança.

## Decisão

1. **Destino: Cloudflare R2**, bucket `keycapstore-backups`, classe **Standard**. A "Infrequent Access" não está no plano grátis e não é usada.
2. **Sem credencial só de escrita.** O token é de **leitura e escrita de objetos**, só para esse bucket. A proteção que ela daria vem de duas outras camadas:
   - **ler não adianta:** todo arquivo sai criptografado com `age`, e a chave privada fica só com o autor;
   - **apagar não funciona:** um **bucket lock** impede apagar ou sobrescrever `daily/` por 7 dias e `weekly/` por 28 dias.
3. **Retenção pelo ciclo de vida do bucket:** apagar `daily/` depois de 8 dias e `weekly/` depois de 29 dias. Na prática, isso guarda 7 diários e 4 semanais, como no ADR 0018. O ciclo de vida só apaga depois que o lock expira.
4. **Proteção contra cobrança:**
   - o uso previsto fica milhares de vezes abaixo do gratuito: menos de 50 MB, contra 10 GB; cerca de 60 gravações por mês, contra 1 milhão;
   - o ciclo de vida impede o acúmulo;
   - um **alerta de orçamento** fica no menor valor que o painel permitir;
   - nenhum outro produto pago da Cloudflare é ativado na conta.
5. **Envio pelo `rclone`** (pacote do Ubuntu, versão ≥ 1.59, como pede a documentação da R2), configurado por variáveis de ambiente lidas do `/opt/keycapstore/backup.env` (root, 600). Com um token de objeto, a documentação pede `no_check_bucket = true`. Também `no_head = true`: no teste de 15/10, o `rclone` 1.60 (do Ubuntu) conferia o arquivo recém-enviado com `HEAD ...?versionId=...`, e a R2 respondia **501 Not Implemented**. O envio dava certo, mas cada execução gastava uma nova tentativa. Sem esse `HEAD`, o envio continua conferido pelo hash devolvido na resposta do `PUT`.

## Alternativas consideradas

- **Backblaze B2:** não exige cartão e tem chave só de escrita (o ADR 0018 original). Foi recomendada pelo Claude, mas o autor preferiu a Cloudflare.
- **R2 sem bucket lock:** quem invadisse a VM poderia apagar todos os backups com o próprio token.
- **Object Storage da Oracle:** descartado no ADR 0018, porque não sobrevive a um problema na conta da Oracle.

## Consequências

- **Positivas:** saída de dados grátis (restaurar não custa nada); o bucket lock protege contra apagamento até de quem tiver o token; tudo documentado no runbook.
- **Negativas / custos:**
  - há um cartão cadastrado, e não existe um teto que bloqueie o uso. O risco de cobrança depende dos limites acima e do alerta de orçamento;
  - o token da VM consegue **ler** os backups, mas eles estão criptografados.
- **Como saber se deu errado:**
  - qualquer valor diferente de zero na fatura da Cloudflare, ou o alerta de orçamento disparando;
  - o healthchecks.io avisando que o backup não rodou;
  - um `rclone` recusado com 401 (versão antiga ou token errado).
