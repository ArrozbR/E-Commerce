# 0027. Eventos do webhook na versão da API do SDK (snapshot, `endive`)

- **Status:** Aceito
- **Data:** 2026-10-13
- **Relacionado:** ADR 0004 (webhook como fonte da verdade), `.claude/rules/pagamentos.md` §2

## Contexto

O handler do webhook verifica e lê os eventos com `EventUtility.ConstructEvent` do Stripe.net 53, que pertence à família de versão da API **`2026-09-30.endive`**. Por padrão, esse método **recusa** eventos de outra família (`throwOnApiVersionMismatch = true`), porque os campos podem ter mudado de nome ou de formato entre famílias.

Em 12/10/2026, os eventos do Stripe CLI chegaram na família **`2026-08-26.dahlia`**, a versão padrão da conta, e todos foram recusados com 400. Segundo a documentação da Stripe, um destino de eventos "snapshot" tem a versão da API **fixada na criação**, e quem usa .NET precisa que essa versão seja a mesma do SDK.

## Decisão

1. O destino de produção (`https://keycapstore.duckdns.org/webhooks/stripe`) recebe eventos **snapshot** na versão **`2026-09-30.endive`**, a mesma do Stripe.net em uso, e só os 4 tipos `checkout.session.*` tratados.
2. A versão padrão da conta continua `dahlia`. Ela não afeta as chamadas da nossa API (o SDK sempre envia a própria versão) e não afeta o destino, que tem a versão fixada.
3. No PC, o `stripe listen` roda com **`--latest`**, para receber eventos na versão mais recente (`endive`).
4. **Não** desligamos a checagem de versão (`throwOnApiVersionMismatch: false`): ler um evento no formato errado é pior do que recusá-lo.
5. **Ao atualizar o Stripe.net para outra família**, o destino precisa ser atualizado **junto**, na mesma janela de deploy. Caso contrário, todos os eventos voltam a ser recusados.

## Alternativas consideradas

- **Desligar a checagem de versão no `ConstructEvent`:** os eventos `dahlia` seriam aceitos, mas lidos com as classes da `endive`. Um campo renomeado viraria `null` sem nenhum erro, e o risco seria confirmar ou não confirmar um pedido com dados errados.
- **Subir a versão padrão da conta para `endive`:** resolveria o CLI sem o `--latest`, mas é uma mudança da conta inteira que pode ser difícil de desfazer, para ganhar pouco. Pode ser reavaliada no M6.
- **Eventos "thin"** (a recomendação da Stripe para integrações novas): não têm versão, mas trazem só o id do objeto. Exigiriam uma chamada à API para buscar a sessão a cada evento, uma dependência a mais dentro do webhook, que precisa ser rápido. Fica para reavaliar se a amarração de versões virar um problema.

## Consequências

- **Positivas:** os eventos chegam no formato exato que o SDK entende, e a checagem de versão continua protegendo contra leituras erradas.
- **Negativas / custos:** existe uma amarração manual entre a versão do Stripe.net e a configuração do destino no Dashboard, e o CLI precisa do `--latest`.
- **Como saber se deu errado:** muitos **400** em "Entregas de eventos" no Dashboard, e o log `Webhook da Stripe recusado: ...` mencionando a versão da API.
