# 0026. Validade da sessão do Checkout: criação do pedido + 31 minutos

- **Status:** Aceito
- **Data:** 2026-10-12
- **Relacionado:** ADR 0006 (ajusta o "30 minutos"), ADR 0005, `docs/DEFINICOES.md` D15, `.claude/rules/pagamentos.md` §3 e §6

## Contexto

A regra original dizia `expires_at` = **agora + 30 minutos**, o mínimo que a Stripe aceita. Ao implementar a criação da sessão (M3, 09/10/2026), duas regras da documentação oficial da Stripe entraram em conflito com o "agora":

1. **`expires_at` precisa ficar entre 30 minutos e 24 horas depois da criação da sessão.** "Agora + 30 min" fica exatamente no limite: alguns segundos de diferença entre o relógio do servidor e o da Stripe bastam para a sessão ser recusada.
2. **Chave de idempotência (`session-{orderId}`):** se o servidor tentar de novo com a mesma chave, os parâmetros precisam ser **idênticos** aos da primeira tentativa, senão a Stripe responde com erro. Com "agora", o `expires_at` muda a cada tentativa, e a nova tentativa, que deveria devolver a mesma sessão, falha.

## Decisão

1. `expires_at` = **data de criação do pedido (`Order.CreatedAt`) + 31 minutos**, calculado na `StripeCheckoutSessionFactory` (`SessionLifetime`).
2. A data de criação não muda nunca, então **toda tentativa para o mesmo pedido envia os mesmos parâmetros**, e a chave de idempotência funciona como deve.
3. O minuto extra é a **folga** para o limite de 30 minutos: cobre o tempo entre criar o pedido e pedir a sessão, e pequenas diferenças de relógio.
4. O Pix continua com `expires_after_seconds` = **1800**.

## Alternativas consideradas

- **Agora + 30 minutos (regra original):** quebra a idempotência nas novas tentativas e fica no limite exato da Stripe.
- **Agora + 31 minutos:** resolve o limite, mas continua quebrando a idempotência, porque "agora" muda a cada tentativa.
- **Não enviar `expires_at` (padrão da Stripe, 24 horas):** o estoque ficaria reservado por até um dia para um cliente que desistiu. Contraria o ADR 0006.
- **Gravar o `expires_at` no pedido:** também daria parâmetros fixos, mas acrescenta uma coluna e uma migration para algo que já se deduz do `CreatedAt`.

## Consequências

- **Positivas:** novas tentativas de criar a sessão são seguras (a Stripe devolve a mesma sessão); a regra é determinística e testável (o teste fixa a data de criação e confere o `expires_at`).
- **Negativas / custos:** a reserva máxima sobe um minuto (sessão de 31 min + Pix de 30 min). Se a sessão for pedida mais de 1 minuto depois da criação do pedido, ela dura um pouco menos de 30 minutos a partir desse momento; se for pedida mais de 31 minutos depois (o que não acontece no fluxo normal, que pede logo em seguida), o `expires_at` já estará no passado e a Stripe recusa, o que leva à compensação (`Failed` + estoque devolvido).
- **Como saber se deu errado:** erros da Stripe sobre `expires_at` nos logs, ou erros de idempotência ("parâmetros diferentes") ao criar sessões.
