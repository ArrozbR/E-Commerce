# 0042. Pré-venda: vagas no estoque, quantidade mínima, fechamento automático e prazo de 80 dias

- **Status:** Aceito
- **Data:** 2026-11-03
- **Relacionado:** ADR 0025 (roadmap da v2), ADR 0006 (estoque), ADR 0031 (cancelamento), ADR 0032 (arrependimento), ADR 0034 (cadastro de produtos), ADR 0041 (frete); `.claude/rules/pagamentos.md` §1, §4 e §5; `docs/DEFINICOES.md` D03 e §3

## Contexto

Item 8, o último da segunda parte da v2: vender um kit **antes de ele ser fabricado** (*group buy*). O cliente paga hoje e recebe semanas depois; o lote só é fabricado se vender o suficiente.

Conferido na documentação oficial da Stripe (03/11):
- Uma **reserva de cartão** ("capturar depois", `payment_intent_data.capture_method = manual` na sessão do Checkout) vale cerca de **7 dias** em compras online; existe "autorização estendida" só para casos elegíveis ("Fazer uma reserva em uma forma de pagamento").
- O **Pix não aceita reserva** ("Manual capture ✗") e o **reembolso de Pix só pode ser pedido até 90 dias** depois do pagamento ("Submission window: 90 days") (página do Pix).
- Para cartão, a página de reembolsos não fala de prazo máximo. **Não confirmado.**

Logo, uma campanha de semanas não cabe numa reserva: a pré-venda **cobra na hora**, como a compra normal, e o risco passa a ser **conseguir devolver** o dinheiro se o lote não sair ou se o cliente cancelar.

## Decisão

1. **Cobra na hora da compra**, pelo mesmo caminho de hoje (sessão do Checkout, webhook, conciliação). Nada de captura manual.
2. **Vagas = `Stock`** (decisão do autor). O admin define quantas unidades o lote terá; cada compra ocupa uma vaga pela **mesma reserva atômica** (`WHERE stock >= @qty`), com a mesma proteção para a disputa da última vaga. Esgotou = acabaram as vagas.
3. **Dados da pré-venda no produto, só na criação** (decisão do autor): o admin marca "pré-venda" ao criar e informa **fim da campanha**, **envio previsto** e **quantidade mínima**. Depois de criado, esses três não mudam (mudar o mínimo no meio da campanha seria injusto com quem já pagou). Um produto normal não vira pré-venda, nem o contrário. Regras na criação: o fim da campanha é depois de agora; o envio previsto é depois do fim da campanha; o mínimo vai de 1 até o número de vagas.
4. **Prazo máximo de 80 dias** (decisão do autor): o envio previsto fica a no máximo **80 dias da criação** da pré-venda. Assim, todo pedido de pré-venda é pago depois da criação e reembolsado (cancelamento, lote que não saiu) antes do envio, dentro dos **90 dias do Pix**, com 10 dias de folga.
5. **Venda só durante a campanha:** a reserva também exige que a campanha esteja aberta (`AND (pré-venda nula OU fim > agora)`), no "Finalizar" e na nova reserva do pagamento atrasado. Depois do fim, ninguém compra.
6. **Quantidade mínima** (decisão do autor), contada em **unidades de pedidos pagos** (`Paid`) do produto. **Fechamento automático** pelo `OrderMaintenanceJob`, **1 hora depois do fim** (decisão do autor), e só quando não houver pedido desse produto em `AwaitingPayment` ou `AwaitingConfirmation` (se houver, tenta na rodada seguinte):
   - **atingiu o mínimo** → a campanha fica **confirmada**; os pedidos seguem o fluxo normal de envio;
   - **não atingiu** → a campanha fica **cancelada** e cada pedido pago do produto é **cancelado** pelo mesmo `Cancel()` do ADR 0031 (vagas devolvidas na mesma transação) e **reembolsado pelo job** (`RefundProcessor`, chave `refund-{orderId}`), como um cancelamento do cliente.
   Nos dois casos, o produto sai de venda.
7. **Separação só depois de confirmada:** o admin não inicia a separação de um pedido de pré-venda enquanto a campanha não estiver confirmada (recusa com 409).
8. **Pré-venda é comprada sozinha** (decisão do autor): um carrinho com um kit de pré-venda não aceita outros produtos, e vice-versa; o "Finalizar" confere de novo. Um pedido é **ou** pronta entrega **ou** de um lote de pré-venda.
9. **O que o cliente vê** (decisão do autor): no catálogo e no carrinho, "Pré-venda até {fim} · envio previsto em {data} · {vendidas} de {mínimo} para o lote sair"; no pedido, o mesmo aviso e, depois do fechamento, se o lote foi confirmado ou cancelado. O pedido guarda o **envio previsto** como snapshot (não muda se algo mudar no produto). A loja não envia e-mail: o cliente acompanha por "Meus pedidos".
10. **Cancelamento e arrependimento não mudam:** o cliente cancela de `Paid`/`Picking` como hoje (ADR 0031) e desiste até 7 dias depois da entrega (ADR 0032). O frete (ADR 0041) é cotado na compra, como hoje.
11. **Regras alteradas:** `pagamentos.md` §1 (exceção: o fechamento da pré-venda cancela pedidos pagos), §4 (`Cancel()` também pelo job de fechamento) e §5 (a reserva também exige a campanha aberta); `DEFINICOES.md` D03 e a linha da pré-venda em §3.

## Alternativas consideradas

- **Reserva no cartão e captura no fim da campanha:** vale só uns 7 dias e não existe no Pix.
- **Sem quantidade mínima:** muito mais simples (nenhum reembolso em massa), mas o autor quis o modelo dos *group buys* reais, em que o lote só sai se vender o suficiente.
- **Vagas ilimitadas:** exigiria um caminho de reserva sem estoque, mexendo na regra mais testada do projeto.
- **Pré-venda só com cartão:** sem limite de prazo, mas tira o Pix, o meio mais usado no Brasil.
- **Cancelamento só durante a campanha:** prazos livres, mas um reembolso tardio (lote que falhou depois de 90 dias) teria de ser feito à mão, fora da Stripe.
- **Admin fecha a campanha por um botão:** mais controle, mas, se ele esquecer, os clientes ficam sem saber se o lote sai.
- **Carrinho misto:** o kit a pronta entrega ficaria parado meses esperando o lote.

## Consequências

- **Positivas:** reaproveita a reserva atômica, o cancelamento e o reembolso que já estão em produção; o cliente vê o progresso do lote; nenhum reembolso fica fora do prazo do Pix.
- **Negativas / custos:** um job novo (fechamento) que cancela pedidos **pagos** sem o cliente pedir, a primeira vez que a loja faz isso; campos novos no produto e no pedido, com migration; a contagem de vendidas no catálogo; uma regra nova no carrinho; o limite de 80 dias restringe lotes de fabricação longa; um lote cancelado devolve o dinheiro, mas a loja não avisa por e-mail.
- **Como saber se deu errado:** um pedido de pré-venda vendido depois do fim da campanha; um lote cancelado com algum pedido pago sem reembolso; um lote fechado com pedido ainda aguardando pagamento; um carrinho misto que chega a virar pedido; um reembolso de Pix recusado por prazo.
