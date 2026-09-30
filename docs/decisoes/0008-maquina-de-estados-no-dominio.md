# 0008. Máquina de estados na entidade `Order`; o admin só executa as transições de envio

- **Status:** Aceito
- **Data:** 2026-09-29
- **Relacionado:** `docs/DEFINICOES.md` D16, §8; `.claude/rules/pagamentos.md` §4

## Contexto

A v1 tem um endpoint de admin para avançar os pedidos no envio. A primeira proposta era um endpoint que aceitasse **qualquer status**. Isso permitiria marcar um pedido como `Paid` sem dinheiro (fraude, ou senha de admin vazada), corromperia o estoque (`Paid` → `Expired` devolveria um item já pago) e transformaria a máquina de estados em decoração.

## Decisão

- **Um estado não se define, uma transição se executa.** `Order.Status` tem `private set`, e cada transição é um método com intenção, que valida o estado de origem e executa os efeitos.
- Uma transição inválida lança `InvalidOrderTransitionException` → **`409 Conflict`**.
- **As transições de pagamento são exclusivas do webhook.** O admin só tem acesso a `StartPicking`, `MarkShipped(trackingCode)` e `MarkDelivered`.
- **Um endpoint por ação** (`POST /admin/orders/{id}/ship`), sem PATCH genérico de status.

## Alternativas consideradas

- **Regra no controller:** o webhook e o admin têm controllers diferentes, então a regra ficaria duplicada e cedo ou tarde divergiria.
- **Regra num serviço, com `Status { get; set; }` público:** qualquer código novo passaria por cima do serviço (modelo anêmico).

## Consequências

- **Positivas:** é impossível burlar a regra (o compilador impede); as transições são testáveis com testes unitários puros; é o modelo de domínio rico que as empresas .NET querem ver.
- **Negativas:** um pouco mais de modelagem no início.
- **Como saber se deu errado:** qualquer atribuição de `Status` fora da entidade; um endpoint genérico de status.
