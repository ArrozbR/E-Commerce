# 0032. Direito de arrependimento (CDC art. 49), com reembolso na hora e devolução conferida pelo admin

- **Status:** Aceito
- **Data:** 2026-10-19
- **Relacionado:** ADR 0025 (v2: parte legal primeiro), ADR 0031 (cancelamento), `docs/DEFINICOES.md` D17, §11; `.claude/rules/pagamentos.md` §1, §4 e §5

## Contexto

Segunda peça da parte legal da v2. O cancelamento (ADR 0031) cobre o pedido que ainda não saiu da loja. Depois do envio, vale o direito de arrependimento.

Textos conferidos no site do Planalto em 19/10/2026:
- **CDC (Lei 8.078/1990), art. 49:** o consumidor pode desistir "no prazo de 7 dias a contar de sua assinatura ou do ato de recebimento do produto" quando a compra ocorre fora do estabelecimento comercial. **Parágrafo único:** os valores pagos "a qualquer título" (inclusive o frete) "serão devolvidos, de imediato, monetariamente atualizados".
- **Decreto 7.962/2013, art. 5º:**
  - **caput:** a loja informa de forma clara os meios para desistir;
  - **§ 1º:** o cliente pode desistir "pela mesma ferramenta utilizada para a contratação";
  - **§ 3º:** a loja comunica a desistência "imediatamente" a quem processou o pagamento, para o valor ser estornado;
  - **§ 4º:** a loja envia "confirmação imediata do recebimento da manifestação de arrependimento".

## Decisão

1. **Quem e quando:** só o **dono** do pedido, por um botão "Desistir da compra" na página do pedido (a mesma ferramenta da compra, § 1º):
   - enquanto o pedido estiver **`Shipped`** (a caminho; os 7 dias contam do recebimento, então antes dele também vale);
   - ou **`Delivered` há no máximo 7 dias** (`Order.WithdrawalPeriod`), contados do novo campo **`DeliveredAt`**, gravado pelo `MarkDelivered(deliveredAt)` com a hora do servidor.
2. **Reembolso na hora**, sem esperar o produto voltar ("de imediato", art. 49; "imediatamente", § 3º): `Withdraw()` → **`Withdrawn`**, e o mesmo job de reembolso (`refund-{orderId}`, valor total, frete incluído) reembolsa no minuto seguinte. O risco de o cliente não devolver fica com a loja, como a lei prevê.
3. **Depois do reembolso o pedido não termina:** `MarkRefunded()` leva `Withdrawn` para **`AwaitingReturn`** (e não para `Refunded`, que é um estado final e não espera nada).
4. **Devolução conferida pelo admin:** na página de envio, ao receber o pacote, o admin marca a devolução (`ConfirmReturn()` → **`Returned`**) e **escolhe se o produto volta ao estoque** (pode chegar aberto ou danificado). O estoque só sobe se ele marcar.
5. **Confirmação para o cliente (§ 4º):** a página do pedido passa a mostrar a desistência registrada. A loja não envia e-mail (fora de escopo); ver "Consequências".
6. **Pedidos entregues antes deste ADR** não têm `DeliveredAt` e **não** podem usar o botão (só existe um, de teste, `04bd9d69`).

## Alternativas consideradas

- **Reembolsar só quando o produto voltar:** protege a loja, mas contraria o "de imediato" da lei e deixa o cliente esperando dias pelo correio.
- **Estoque sempre de volta ao receber:** um produto danificado voltaria à venda.
- **Estoque nunca de volta:** obrigaria um ajuste manual, e ainda não existe tela de estoque (vem com o cadastro de produtos da v2).
- **Desistir só depois de `Delivered`:** mais simples, mas a lei conta do recebimento, e quem desiste com o pacote a caminho teria de esperar.
- **Contar os 7 dias por dia do calendário (até 23:59 do 7º dia):** mais generoso, porém exige decidir fuso e dia útil. Ficou em **7 × 24 h exatas** a partir da entrega; se a regra jurídica pedir o calendário, muda só a conta em `Withdraw()`.

## Consequências

- **Positivas:** o cliente exerce o direito sozinho e recebe o dinheiro em minutos; o reembolso reaproveita o job já testado em produção (cartão e Pix).
- **Negativas / custos:**
  - três estados novos (`Withdrawn`, `AwaitingReturn`, `Returned`), o campo `DeliveredAt` (migration) e uma ação nova na página do admin;
  - o `MarkRefunded()` passa a ter dois destinos, conforme a origem.
- **Pendente para o go-live (não para o modo teste):**
  - **e-mail de confirmação** (§ 4º pede que a loja "envie" a confirmação; hoje ela só aparece na página);
  - **quem paga o frete de volta** e como o cliente envia o pacote: a lei não diz no texto conferido. Definir com orientação jurídica e informar nos termos de uso.
- **Como saber se deu errado:**
  - um pedido `Withdrawn` parado sem virar `AwaitingReturn` (o `LogError` do job mostra o motivo);
  - um pedido `Delivered` há mais de 7 dias aceitando a desistência.
