# 0040. Payment Element com a sessão do Checkout, na mesma página do endereço

- **Status:** Aceito
- **Data:** 2026-10-29
- **Relacionado:** substitui o ADR 0003 (Checkout hospedado); ADR 0004 (webhook como fonte da verdade), ADR 0006 (estoque), ADR 0025 (roadmap da v2), ADR 0026 (validade da sessão), ADR 0037 (conciliação), ADR 0038 (idioma da Stripe); `.claude/rules/pagamentos.md` §6; `docs/DEFINICOES.md` D07

## Contexto

Item 6 da segunda parte da v2: trocar a página hospedada da Stripe por um formulário de pagamento embutido na loja. O ADR 0003 tinha adiado o Payment Element porque, com a Payment Intents API, a loja teria de gerenciar o pagamento e **implementar a expiração à mão**, o que traria de volta o "timer próprio" proibido pelas regras de estoque.

Conferido na documentação oficial da Stripe (29/10):
- "A Stripe recomenda usar a **Checkout Sessions API com o Payment Element**, em vez da Payment Intents API, para a maioria das integrações."
- A sessão é criada com **`ui_mode: "elements"`** e devolve um `client_secret`; na página, o Stripe.js monta o formulário com ele (`stripe.initCheckoutElementsSdk`) e o pagamento é confirmado com `checkout.confirm()`, que também cuida de autenticação (3DS) e de redirecionamentos de bancos.
- Depois do pagamento, a Stripe leva a pessoa para o **`return_url`** (com `{CHECKOUT_SESSION_ID}`), no lugar de `success_url`/`cancel_url`.
- A confirmação do pedido continua pelos webhooks **`checkout.session.completed`** e **`checkout.session.async_payment_*`**; a página de retorno sozinha "não é confiável" (a pessoa pode perder a conexão antes de ela carregar).
- O Stripe.js precisa ser carregado **direto de `js.stripe.com`** (PCI) e usa a **chave publicável**.
- Também existem `ui_mode` `embedded_page` (a página inteira da Stripe num quadro) e `form`.

**Não confirmado por escrito** (fica para o teste no sandbox): como o **Pix** aparece no formulário com Checkout Sessions (a página do Pix lida descreve a variante com PaymentIntents); se a **expiração da sessão** (`expires_at`, ADR 0026) se comporta igual com `ui_mode: elements` (é um parâmetro da sessão; deve se comportar); se o `locale` da sessão (ADR 0038) vale para o formulário embutido; o **custo** (nenhuma página lida cobra pela forma de exibir; a cobrança é por transação).

## Decisão

1. **Payment Element com a sessão do Checkout** (`ui_mode: "elements"`) (decisão do autor). A sessão continua sendo a mesma de hoje: montada do snapshot do pedido em `brl`, `metadata.order_id`, `client_reference_id`, `expires_at` = criação do pedido + 31 min, Pix 1800 s, `locale` do pedido e chave de idempotência `session-{orderId}`. Muda só: `ui_mode`, `return_url` (página de retorno do pedido, que já existe) no lugar de `success_url`/`cancel_url`, e a loja passa a usar o `client_secret` da sessão.
2. **Na mesma página do endereço, em duas etapas** (decisão do autor; a forma é proposta do Claude):
   - a pessoa preenche o endereço e clica em **"Continuar para o pagamento"**: a loja faz o que o "Finalizar pedido" faz hoje (valida, cria o pedido, **reserva o estoque**, cria a sessão), sem sair da página;
   - o endereço fica só para leitura e o **formulário da Stripe aparece embaixo**, com o total já com o frete; o botão **"Pagar"** chama `checkout.confirm()`.
   A reserva continua acontecendo no mesmo momento (ao confirmar o endereço); nenhuma regra de estoque muda.
3. **Retomar o pagamento:** um pedido `AwaitingPayment` com sessão ainda aberta mostra, na própria página do pedido, o mesmo formulário (para quem fechou a aba e voltou dentro dos 31 min). Depois disso, a sessão expira pela Stripe, como hoje.
4. **Só o formulário embutido** (decisão do autor): o Checkout hospedado deixa de ser usado. Se o Stripe.js não carregar, a página avisa para tentar de novo. Um caminho só: uma sessão por pedido, a idempotência `session-{orderId}` intacta.
5. **Nada muda no backend do pagamento:** webhook, idempotência, transições, estoque, expiração, conciliação (ADR 0037) e reembolsos continuam iguais. A página de retorno continua só consultando o backend.
6. **Chave publicável** (`pk_test_`/`pk_live_`) como configuração obrigatória (`Stripe:PublishableKey`, `ValidateOnStart`, formato conferido como a chave secreta). Não é segredo, mas fica no `.env` e no `user-secrets` para não misturar teste e produção. O `client_secret` da sessão só vai para o **dono** do pedido e nunca aparece em log.
7. **Testes:** os automáticos cobrem o servidor (sessão criada com `ui_mode: elements` e `return_url`; a página entrega a chave publicável e o `client_secret` só ao dono; o "Continuar" com endereço inválido, carrinho vazio e produto sem estoque; retomar só com sessão aberta). O JavaScript da Stripe é conferido **no sandbox**, à mão: cartão `4242`, 3DS (`4000002500003155`), Pix pago, Pix nunca pago (expiração em 31 min, como a Q1b) e o idioma do formulário.

## Alternativas consideradas

- **Payment Intents API** (o Payment Element "puro"): expiração e liberação de estoque escritas à mão; o motivo do ADR 0003 continua valendo.
- **Página embutida (`embedded_page`):** quase nenhum JavaScript nosso, mas o visual continua o da Stripe; pouco ganho em relação ao hospedado.
- **Página própria de pagamento** (`/orders/{id}/pay`): mais simples (o "Finalizar" continua um formulário comum), mas o autor preferiu tudo na mesma página.
- **Manter o hospedado como reserva:** dois caminhos para manter e testar e duas sessões possíveis por pedido.

## Consequências

- **Positivas:** o cliente paga sem sair da loja, com o visual da loja; o backend de pagamento, já testado em produção, não muda.
- **Negativas / custos:** JavaScript na página de checkout (a primeira lógica de pagamento no navegador do projeto, e não coberta pelos testes automáticos); mais uma configuração (`pk_`); a página do checkout fica mais complexa (duas etapas); o comportamento do Pix e da expiração com `ui_mode: elements` precisa ser confirmado no sandbox antes da produção.
- **Como saber se deu errado:** um pedido pago que não vira Pago (o webhook não chegou e nem a conciliação resolveu); o formulário aparecendo para quem não é o dono do pedido; um pedido com duas sessões; um Pix que não expira em 31 minutos.
