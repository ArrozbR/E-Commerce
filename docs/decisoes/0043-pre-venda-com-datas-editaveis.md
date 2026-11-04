# 0043. Pré-venda: fim da campanha e envio previsto editáveis até o fechamento

- **Status:** Aceito
- **Data:** 2026-11-04
- **Relacionado:** altera a decisão 3 do ADR 0042 (pré-venda); ADR 0031 (cancelamento)

## Contexto

O ADR 0042 travou o fim da campanha, o envio previsto e o mínimo depois da criação. No primeiro teste em produção (04/11), as campanhas de teste terminaram antes das compras e não havia como adiar o fim: a única saída era criar outras. O autor preferiu poder corrigir as datas. Na vida real acontece o mesmo: o fabricante atrasa, ou a campanha precisa de mais uns dias.

O que precisa continuar valendo: todo reembolso de Pix tem de caber nos **90 dias** depois do pagamento (ADR 0042, decisão 4). O primeiro pagamento possível acontece logo depois da **criação** da campanha, e um reembolso pode acontecer até o envio. Por isso o limite de 80 dias é contado **da criação**, e não da edição.

## Decisão

1. **O admin pode mudar o fim da campanha e o envio previsto** (decisão do autor), por um formulário na lista de produtos, **enquanto o lote não foi fechado** (sem resultado). Depois do fechamento, nada muda.
2. **As mesmas regras da criação:** o novo fim é depois de agora; o envio vem depois do fim; o envio fica a no máximo **80 dias da criação** da campanha. Por isso o produto passa a guardar a **data de criação** da pré-venda (`PreOrderOpenedAt`). As pré-vendas criadas antes desta mudança recebem, na migration, o envio previsto menos 80 dias (o mais cedo que a criação poderia ter sido), o que nunca afrouxa o limite.
3. **Reabrir:** uma campanha que terminou e ainda não foi fechada (dentro da 1 hora de folga) pode receber um fim novo e volta a vender. O fechamento confere de novo o fim dentro da transação, para não fechar uma campanha que acabou de ser adiada.
4. **O mínimo continua travado:** mudá-lo com pedidos pagos mudaria a regra para quem já comprou.
5. **Pedidos já feitos:** o pedido continua guardando a data prometida **na compra** (snapshot); a página do pedido mostra a **previsão atual** e, se mudou, também a da compra ("na compra: …"). Quem não quiser esperar a nova data pode cancelar antes do envio, como hoje (ADR 0031).

## Alternativas consideradas

- **Só o fim da campanha editável:** mais simples (o envio previsto, validado na criação, garantiria sozinho os 80 dias), mas não cobre o atraso do fabricante.
- **Tudo editável, inclusive o mínimo:** injusto com quem já pagou.
- **Manter tudo travado (ADR 0042):** um erro de digitação ou um atraso obrigaria a criar outra campanha, separando os pedidos.

## Consequências

- **Positivas:** corrige datas sem recriar campanhas; o cliente vê a previsão atualizada.
- **Negativas / custos:** uma coluna e uma migration a mais; uma corrida rara entre a edição e o fechamento, tratada pela nova conferência do fim; a data mostrada no pedido pode mudar depois da compra (sempre com a original ao lado).
- **Como saber se deu errado:** um envio previsto a mais de 80 dias da criação; uma campanha fechada logo depois de ser adiada; um pedido sem a data original visível depois de uma mudança.
