# 0006. Reserva de estoque em "Finalizar", liberada só por evento da Stripe

- **Status:** Aceito
- **Data:** 2026-09-29
- **Relacionado:** `docs/DEFINICOES.md` D12, D14, D15, D35; `.claude/rules/pagamentos.md` §5

## Contexto

Com estoque limitado, dois clientes podem disputar a última unidade. Se ninguém reservar, os dois pagam por um item só. Se a reserva for cedo demais, carrinhos abandonados prendem o estoque. Se a reserva for liberada por um timer próprio, o sistema pode liberar o item enquanto o cliente ainda está pagando, e aí alguém paga por um item que já foi vendido.

## Decisão

- **Reservar ao clicar em "Finalizar"**, na mesma transação que cria o pedido, com uma atualização **atômica e condicional** (`UPDATE ... SET stock = stock - @qty WHERE id = @id AND stock >= @qty`). Se afetar 0 linhas, o item está esgotado.
- A sessão do Checkout expira em **30 minutos**; o Pix, em **1800 segundos** depois de escolhido. A reserva máxima é de cerca de 1 hora.
- **A reserva só é liberada pelos eventos `checkout.session.expired` e `async_payment_failed`, nunca por timer próprio.** Uma sessão expirada não aceita pagamento, o que torna impossível pagar um item já liberado no cartão.
- **Única exceção:** um job libera pedidos `AwaitingPayment` **sem `StripeSessionId`** há mais de 10 minutos (a aplicação caiu antes de criar a sessão). É seguro porque, sem sessão, não existe forma de pagar.
- **Defesa:** uma confirmação de pagamento sem reserva tenta reservar de novo. Se não houver estoque → `PaidOutOfStock` → **reembolso automático e idempotente para quem pagou**.

## Alternativas consideradas

- **Reservar ao adicionar ao carrinho:** carrinhos abandonados prendem estoque, e dá para "esgotar" a loja sem pagar.
- **Baixar o estoque só quando o pagamento é confirmado:** os dois clientes pagam, e um deles precisa de reembolso.
- **Liberar por timer próprio:** cria uma janela de corrida com o pagamento.
- **Cancelar o pedido de outro cliente para atender quem pagou atrasado:** pune quem seguiu as regras e cria efeito cascata.
- **Padrões da Stripe (sessão de 24 horas, Pix de 4 horas):** prendem o estoque tempo demais.

## Consequências

- **Positivas:** o estado inválido é impossível no caminho normal; o que escapa é tratado de forma automática e justa.
- **Negativas:** o estoque fica preso por até cerca de 1 hora em checkouts abandonados; existe um job pequeno para os pedidos órfãos.
- **Como saber se deu errado:** estoque negativo; pedidos `AwaitingPayment` antigos com sessão; o teste de "reserva concorrente do último item" falhar.

> Princípio: primeiro tornar o estado inválido impossível, depois tratar o que ainda escapar.
