# Regras de pagamento (Stripe)

Estas regras são inegociáveis. Qualquer código que toque em pedido, estoque, sessão da Stripe ou webhook deve segui-las. Justificativas completas em `docs/DEFINICOES.md` §7–§8 e nos ADRs 0003–0007.

## 1. Fonte da verdade

- **O webhook assinado da Stripe é a ÚNICA fonte da verdade sobre pagamento.** Só o handler do webhook muda um pedido para `Paid`, `AwaitingConfirmation`, `Expired`, `Failed`, `PaidOutOfStock` ou `Refunded`.
- **O redirecionamento (`success_url`) não prova nada.** A página de retorno apenas consulta o próprio backend (`GET /orders/{id}/status`, restrito ao dono do pedido) e **nunca** altera estado.
- Nenhum endpoint de admin, nenhuma tela e nenhum comando altera estado de pagamento.
- Consultar a API da Stripe (polling) é só rede de segurança (conciliação, fora da v1), nunca o caminho principal.

## 2. Webhook: `POST /webhooks/stripe`

Ordem obrigatória:

1. **Ler o corpo bruto** da requisição (sem model binding) e verificar a assinatura com o header `Stripe-Signature` e o segredo `whsec_...`, usando o SDK oficial (`EventUtility.ConstructEvent`). A verificação vive na `Infrastructure`.
2. Se a assinatura for inválida ou estiver ausente, responder **`400`**, sem tocar no banco.
3. Tipos tratados:
   - `checkout.session.completed`
   - `checkout.session.async_payment_succeeded`
   - `checkout.session.async_payment_failed`
   - `checkout.session.expired`

   Qualquer outro tipo recebe **`200`** e é ignorado, com um log de nível Debug. (Q1 em aberto: validar no spike quais eventos o Pix emite de fato. Se `payment_intent.*` precisar ser tratado, o tratamento segue as mesmas regras.)
4. **Tudo numa única transação no banco:**
   1. `INSERT` do `event.id` em `ProcessedStripeEvents` (restrição **UNIQUE**). Uma violação de unicidade significa duplicata: responder **`200`** sem nenhum efeito.
   2. Carregar o pedido por `metadata.order_id`. Se o pedido não existir, registrar Warning e responder **`200`**.
   3. **Conferir `amount_total` e `currency` contra o pedido.** Se divergirem, **não** marcar como pago, registrar um alerta (Error) e responder `200`.
   4. Aplicar a transição **pelo método do domínio** (seção 4).
   5. Commit e resposta **`200`**.
5. Qualquer exceção **desfaz a transação inteira** e o endpoint responde **`500`**, para a Stripe tentar de novo. Nunca responder `200` quando o processamento falhou.
6. O handler precisa ser rápido. Nada de chamadas lentas ou de e-mail dentro da requisição do webhook.

### Mapeamento evento → transição

| Evento | Condição | Transição |
|---|---|---|
| `checkout.session.completed` | `payment_status == "paid"` | `ConfirmPayment()` → `Paid` |
| `checkout.session.completed` | `payment_status == "unpaid"` | `MarkAwaitingConfirmation()` → `AwaitingConfirmation` |
| `checkout.session.async_payment_succeeded` | — | `ConfirmPayment()` → `Paid` (aceito também a partir de `AwaitingPayment`, para eventos fora de ordem) |
| `checkout.session.async_payment_failed` | — | `MarkFailed()` → `Failed` + **liberar estoque** |
| `checkout.session.expired` | — | `Expire()` → `Expired` + **liberar estoque** |

**A decisão é pelo `payment_status`, nunca pelo meio de pagamento.** Não existe `if (method == "pix")` no fluxo.

## 3. Idempotência

- **Webhook:** o `event.id` é gravado em `ProcessedStripeEvents` com UNIQUE, **na mesma transação** da mudança de estado. Nunca "consultar e depois inserir" (condição de corrida). Inserir primeiro e tratar a violação.
- **Estado:** as transições são condicionais (concorrência otimista via token de concorrência do EF Core, ou `UPDATE ... WHERE Status = @esperado`). Uma segunda execução encontra o estado já alterado e não faz nada.
- **Chamadas à API da Stripe** sempre com uma chave de idempotência determinística:
  - criar sessão: `session-{orderId}`
  - reembolso: `refund-{orderId}`
- **Nunca** usar `lock`, `SemaphoreSlim`, `Mutex` ou Redis para garantir unicidade. **A garantia mora no banco.** Um lock em memória não funciona com mais de um processo.

## 4. Estados do pedido

- `Order.Status` tem **`private set`**. Nenhum código fora da entidade atribui status.
- Cada transição é um método da entidade `Order`, que valida o estado de origem e lança `InvalidOrderTransitionException` se a transição não for permitida. A camada Web traduz essa exceção para **`409 Conflict`**.

| Método | De | Para | Quem chama |
|---|---|---|---|
| (criação) | — | `AwaitingPayment` | caso de uso de checkout |
| `MarkAwaitingConfirmation()` | `AwaitingPayment` | `AwaitingConfirmation` | webhook |
| `ConfirmPayment()` | `AwaitingPayment`, `AwaitingConfirmation` | `Paid` | webhook |
| `Expire()` | `AwaitingPayment` | `Expired` | webhook |
| `MarkFailed()` | `AwaitingPayment`, `AwaitingConfirmation` | `Failed` | webhook, falha ao criar sessão, job de órfãos |
| `MarkOutOfStock()` | (confirmação sem reserva) | `PaidOutOfStock` | webhook |
| `MarkRefunded()` | `PaidOutOfStock` | `Refunded` | caso de uso de reembolso |
| `StartPicking()` | `Paid` | `Picking` | admin |
| `MarkShipped(trackingCode)` | `Picking` | `Shipped` | admin |
| `MarkDelivered()` | `Shipped` | `Delivered` | admin |

- **Admin:** um endpoint por ação (`POST /admin/orders/{id}/start-picking`, `/ship`, `/deliver`). **Nunca um PATCH genérico de status.**
- Cancelamento **não existe na v1** (entra na v2).

## 5. Estoque

- `Product.Stock` = quantidade **disponível para venda**. Reservar = decrementar. Liberar = incrementar.
- **Reserva** em "Finalizar", na mesma transação que cria o pedido, **atômica e condicional**:
  ```sql
  UPDATE products SET stock = stock - @qty WHERE id = @id AND stock >= @qty
  ```
  (ou `ExecuteUpdateAsync` equivalente). Se afetar 0 linhas, a transação inteira é desfeita, e o cliente vê "esgotado". **Nunca ler o estoque, checar em C# e depois gravar.**
- **Liberação SÓ por evento da Stripe** (`expired`, `async_payment_failed`). **Nunca por timer próprio.**
- **Única exceção (D35):** um job libera pedidos `AwaitingPayment` **sem `StripeSessionId`** há mais de 10 minutos. Sem sessão, não existe forma de pagar.
- Se a criação da sessão na Stripe falhar: `MarkFailed()` + liberar estoque (compensação).
- A confirmação de pagamento **não** mexe no estoque (ele já foi reservado). Se a reserva tiver sido liberada: tentar reservar de novo de forma atômica. Se conseguir → `Paid`. Se não → `PaidOutOfStock` → reembolso automático (`refund-{orderId}`) → `Refunded`. Se o reembolso falhar, o pedido continua em `PaidOutOfStock` e gera um alerta.

## 6. Criação da sessão do Checkout

- `mode = payment`, `ui_mode = hosted_page`.
- `line_items` em **`brl`**, montados **a partir do snapshot do pedido no servidor**. Preço vindo do cliente é ignorado.
- `client_reference_id` = `orderId` e `metadata["order_id"]` = `orderId`.
- `expires_at` = agora + **30 minutos**.
- `payment_method_options.pix.expires_after_seconds` = **1800**.
- `success_url` → `/orders/{id}/return` e `cancel_url` → carrinho.
- Chave de idempotência `session-{orderId}`.
- Os meios de pagamento (cartão e Pix) são habilitados no Dashboard. Nada de lista fixa no código, exceto se o spike provar que é necessário.
- Gravar o `StripeSessionId` no pedido logo após a criação.
- Limites do Pix: de R$ 0,50 a R$ 3.000. Acima disso, o Checkout não oferece Pix, e isso é aceito.

## 7. Segredos e dados

- `sk_...` (chave secreta) e `whsec_...` (segredo do webhook) **nunca** aparecem em código, `appsettings*.json`, imagem Docker, log ou mensagem de erro.
  - Em desenvolvimento: `dotnet user-secrets`.
  - Em produção: `.env` fora do repositório.
- Configuração via Options (`StripeOptions`) com **`ValidateOnStart`**: a aplicação não sobe sem as chaves.
- **Chaves de teste e de produção nunca no mesmo ambiente.** Nenhum `if (isTest)` no fluxo de pagamento.
- **Dados de cartão nunca passam pelo sistema.** Só guardamos os IDs da Stripe (`cs_...`, `pi_...`, `re_...`, `evt_...`).
- Logs do fluxo de pagamento incluem **`event.id` e `orderId`**, e **nunca** e-mail, nome, endereço ou payload completo do evento.

## 8. Testes obrigatórios

Toda mudança no fluxo de pagamento vem acompanhada de testes que cubram, no mínimo, o cenário alterado entre os listados em `docs/DEFINICOES.md` §10.1. Os de **integração** (PostgreSQL real, eventos assinados localmente) são obrigatórios para:
- assinatura inválida
- duplicata em sequência
- duplicata em paralelo
- reserva concorrente do último item
- expiração duplicada
- evento fora de ordem
- rollback em falha
