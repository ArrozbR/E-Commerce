# 0007. Cartão e Pix na v1, com decisão pelo `payment_status`

- **Status:** Aceito
- **Data:** 2026-09-29
- **Relacionado:** `docs/DEFINICOES.md` D08, D15; `.claude/rules/pagamentos.md` §2 e §6

## Contexto

O público brasileiro usa muito Pix. Diferente do cartão, o Pix é **assíncrono**: o cliente conclui o Checkout, recebe um QR code e paga depois, dentro de um prazo. Segundo a documentação da Stripe, contas do Brasil aceitam Pix para pagamentos únicos no Checkout, com limite de R$ 0,50 a R$ 3.000 por transação e reembolso em até 90 dias.

## Decisão

- **Cartão e Pix** na v1. **Boleto fica de fora.**
- O handler decide pelo **`payment_status`**, nunca pelo meio de pagamento: `paid` → `Paid`; `unpaid` → `AwaitingConfirmation`; os eventos `async_payment_succeeded` e `async_payment_failed` fecham o ciclo.
- `payment_method_options.pix.expires_after_seconds = 1800` (o padrão da Stripe seria 4 horas).
- Os meios de pagamento são habilitados no Dashboard da Stripe.

## Alternativas consideradas

- **Só cartão:** mais simples, mas ignora o meio mais usado no Brasil e não exercita pagamento assíncrono.
- **Cartão, Pix e boleto:** o boleto é lento (até 3 dias úteis), raramente é pago e prenderia o estoque por dias.

## Consequências

- **Positivas:** acrescentar outro meio assíncrono no futuro não muda o código do handler.
- **Negativas:** um novo estado (`AwaitingConfirmation`) e mais casos de borda; carrinhos acima de R$ 3.000 não oferecem Pix.
- **Pendente (Q1):** confirmar no spike do M0 quais eventos o Checkout emite com Pix na prática.
- **Como saber se deu errado:** um `if (pix)` aparecendo no fluxo; pedidos presos em `AwaitingConfirmation`.
