# 0003. Stripe Checkout hospedado na v1

- **Status:** Aceito
- **Data:** 2026-09-29
- **Relacionado:** `docs/DEFINICOES.md` D07; `.claude/rules/pagamentos.md` §6

## Contexto

A Stripe oferece duas formas principais de montar a tela de pagamento: o **Checkout hospedado** (o cliente é redirecionado para uma página da Stripe) e o **Payment Element** (formulário embutido no site, com PaymentIntents gerenciados pela aplicação). O diferencial do portfólio é o backend, e não o formulário de cartão.

## Decisão

Usar o **Stripe Checkout hospedado** (`ui_mode = hosted_page`) na v1. O Payment Element fica planejado para a v3.

## Alternativas consideradas

- **Payment Element na v1:** traria controle total da experiência, mas exigiria gerenciar PaymentIntents, 3DS no frontend e **expiração implementada à mão**, o que reintroduziria o problema do timer próprio (ver ADR 0006).

## Consequências

- **Positivas:** PCI (o cartão nunca passa pelo sistema), 3DS, localização pt-BR, layout mobile e **expiração de sessão** já prontos. A expiração nativa é a base do design de estoque.
- **Negativas:** o cliente sai do site para pagar, e a personalização visual é limitada.
- **Como saber se deu errado:** se for preciso contornar limitações do Checkout com gambiarras no fluxo.
