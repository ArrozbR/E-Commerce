# 0051. "Avise-me quando voltar": só com conta, um aviso por pedido, conferido pelo job

- **Status:** Aceito
- **Data:** 2026-11-09
- **Relacionado:** ADR 0049 (e-mails da loja), ADR 0034 (cadastro de produtos e estoque por ajuste), ADR 0042 (pré-venda), ADR 0033 (exclusão de conta), ADR 0015 (dados pessoais)

## Contexto

Terceiro item da V4. Hoje um kit esgotado só mostra "Esgotado", e quem queria comprar não fica sabendo quando ele volta. Numa loja de lotes limitados, o estoque volta por vários caminhos:
- o admin repõe (ajuste por diferença, ADR 0034);
- um pedido não pago expira;
- um pagamento falha;
- um cliente cancela (ADR 0031);
- uma devolução volta ao estoque (ADR 0032).

## Decisão

1. **Só quem tem conta** pede o aviso (decisão do autor): o botão **"Avise-me quando voltar"** aparece no lugar do "Adicionar ao carrinho" de um kit esgotado e pede login. O e-mail usado é o da conta; nenhum formulário recebe e-mail solto, e ninguém consegue inscrever o e-mail de outra pessoa.
2. **Só kits normais** (decisão do autor): o botão não aparece em pré-venda, porque nela "esgotado" quer dizer que as vagas acabaram ou o lote fechou. Nem em kit inativo.
3. **O pedido de aviso fica numa tabela** (`stock_alerts`):
   - guarda o cliente, o kit, a data e o idioma da página no momento do pedido;
   - a chave é **(cliente, kit)**, então pedir duas vezes não duplica (o banco garante);
   - o cliente pode cancelar o aviso na própria página do kit.
4. **Quem avisa é o job**, a cada minuto (decisão do autor: "sempre que voltar a ter estoque"). O `StockAlertNotifier`, no `OrderMaintenanceJob`, procura avisos de kits **ativos, fora de pré-venda e com estoque > 0**. Para cada um, numa transação:
   - anota o e-mail **"Voltou ao estoque"** na caixa de saída (ADR 0049; a tabela ganha o kit, `ProductId`);
   - **apaga o pedido de aviso** (decisão do autor: avisa uma vez e acaba).

   Conferir o estoque num lugar só cobre todos os caminhos de volta, sem mexer em nenhum deles (reserva, webhook, cancelamento, devolução, admin).
5. **Todos os inscritos recebem ao mesmo tempo.** O e-mail diz que o kit voltou **enquanto durar o estoque**. Não há reserva para quem foi avisado: a reserva continua só no "Finalizar" (`pagamentos.md` §5).
6. **Dados pessoais:**
   - os avisos entram no **"baixar meus dados"**;
   - são **apagados na mesma transação** da exclusão de conta (ADR 0033);
   - os logs só levam ids.

## Alternativas consideradas

- **Qualquer e-mail, sem conta:** alcança mais gente, mas a loja passaria a guardar e-mails de quem não tem conta, e qualquer um poderia inscrever o e-mail de outra pessoa.
- **Avisar só na reposição do admin:** mais previsível, mas uma unidade que volta de um pedido expirado não avisaria ninguém.
- **Avisar dentro de cada caminho que devolve estoque:** sabe a hora exata, mas mexe em seis lugares do fluxo de pagamento e estoque, alguns deles na transação do webhook.
- **Manter o aviso depois de mandar:** quem não comprou receberia um e-mail a cada vez que o kit voltasse.
- **Pré-venda também:** uma vaga que volta durante a campanha é rara, e um lote fechado nunca volta.

## Consequências

- **Positivas:**
  - quem queria o kit fica sabendo quando ele volta, sem a loja guardar e-mails de fora;
  - nenhum caminho de estoque mudou;
  - o e-mail usa a mesma base de envio e novas tentativas.
- **Negativas / custos:**
  - até 1 minuto de atraso (o job) mais o envio;
  - uma unidade que volta e esgota em segundos pode avisar todo mundo de um kit que já acabou de novo (o e-mail diz "enquanto durar");
  - uma tabela nova, uma coluna nova na caixa de saída e textos novos nos dois idiomas.
- **Como saber se deu errado:**
  - um aviso que não sai com o kit em estoque;
  - um aviso repetido para a mesma pessoa e o mesmo kit sem um pedido novo;
  - um aviso de pré-venda ou de kit inativo;
  - um aviso que sobra depois de a conta ser excluída.
