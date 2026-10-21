# 0033. Exclusão de conta (LGPD): a conta some, os pedidos ficam sem dono e o endereço espera o prazo legal

- **Status:** Aceito
- **Data:** 2026-10-21
- **Relacionado:** ADR 0025 (v2: parte legal primeiro), ADR 0032 (arrependimento), `docs/DEFINICOES.md` D17, D26, §6 (dados pessoais), Q5

## Contexto

Última peça da parte legal da v2. O `DEFINICOES.md` §6 já tinha o rascunho: apagar a conta, manter os pedidos sem dono, anonimizar nome e endereço depois do prazo legal e bloquear com pedido em andamento. Faltavam os detalhes.

Texto da LGPD (Lei 13.709/2018) conferido no Planalto em 21/10/2026:
- **art. 15, III:** o tratamento termina com a "comunicação do titular";
- **art. 16:** depois do término os dados "serão eliminados", **autorizada a conservação** para "cumprimento de obrigação legal ou regulatória pelo controlador" (I) ou para "uso exclusivo do controlador [...] desde que anonimizados" (IV);
- **art. 18, VI:** direito à eliminação, "exceto nas hipóteses previstas no art. 16";
- **art. 18, § 6º:** quem compartilhou os dados deve avisar os outros agentes (no nosso caso, a Stripe) da eliminação;
- **art. 5º, XI, e art. 12:** dado anonimizado é o que "perde a possibilidade de associação" a uma pessoa, e só deixa de ser dado pessoal se não puder ser revertido com esforço razoável.

Também conferido: o Marco Civil da Internet, art. 7º, X (incluído pela LGPD), garante a "exclusão definitiva" dos dados ao fim da relação, "ressalvadas as hipóteses de guarda obrigatória".

## Decisão

1. **Quem:** o próprio cliente, numa página "Excluir minha conta", **digitando a senha atual**. Senha errada conta para o bloqueio de 3 tentativas, como no login. Depois de excluir, a sessão é encerrada.
2. **Bloqueio:** a exclusão é recusada enquanto houver pedido **não finalizado**. Finalizado é:
   - `Expired`, `Failed`, `Refunded` ou `Returned`;
   - `Delivered` **fora** do prazo de arrependimento (`!CanWithdraw(now)`). Sem conta, o cliente perderia o botão de desistir.

   Todo o resto bloqueia: `AwaitingPayment`, `AwaitingConfirmation`, `Paid`, `Picking`, `Shipped`, `PaidOutOfStock`, `Cancelled`, `Withdrawn`, `AwaitingReturn` e `Delivered` dentro dos 7 dias.
3. **Admin não se exclui pela página.** Uma conta com o papel `Admin` recebe a recusa, para a loja não ficar sem admin por um clique.
4. **O que acontece, numa transação:**
   - os pedidos do cliente ficam **sem dono** (`Order.DetachCustomer()` → `CustomerId = null`; a coluna passa a aceitar vazio);
   - o carrinho é apagado;
   - a conta do Identity é apagada (e-mail, hash da senha, papéis).
5. **Nome e endereço nos pedidos ficam** até o prazo legal de guarda (art. 16, I) e depois são anonimizados por um job. **O job fica para quando o prazo (Q5) for definido.** O prazo conta da **venda** (`CreatedAt`), e não da exclusão da conta, então não é preciso guardar a data da exclusão. Sem dono, o pedido não aparece para ninguém além do admin.

## Alternativas consideradas

- **Apagar os pedidos junto com a conta:** a loja perderia o registro da venda, que a lei obriga a guardar (art. 16, I) e que serve para defesa numa contestação.
- **Anonimizar nome e endereço já na exclusão:** mais protetor, mas a loja perderia um dado que pode precisar dentro do prazo legal (nota fiscal, contestação no cartão).
- **Bloquear só até a entrega:** quem exclui a conta no 2º dia depois de receber perderia o arrependimento pelo site.
- **Confirmar só com um botão:** qualquer pessoa com uma sessão aberta apagaria a conta, sem volta.
- **Admin se excluindo como qualquer conta:** a página de envio ficaria inacessível até alguém rodar `create-admin` na VM.

## Consequências

- **Positivas:** o cliente exerce o direito sozinho; a loja guarda o que a lei manda e nada além; nenhum pedido fica sem histórico.
- **Negativas / custos:**
  - `CustomerId` opcional no pedido (migration);
  - nome e endereço continuam no banco até existir o job de anonimização.
- **Pendente para o go-live (não para o modo teste):**
  - **prazo de guarda (Q5)** e o **job de anonimização**;
  - **avisar a Stripe** (art. 18, § 6º): o Checkout guarda e-mail e nome do pagador. Verificar na documentação da Stripe o que é possível apagar e o que ela é obrigada a manter;
  - **backups:** os dados da conta continuam nos backups criptografados até eles expirarem (até 29 dias, pelo ciclo de vida da R2). Informar isso na política de privacidade.
- **Corrida conhecida:** um pedido criado em outra aba exatamente durante a exclusão ficaria com o id de uma conta que não existe mais. O risco é baixo (exige o cliente comprando e se excluindo no mesmo instante); se aparecer, o job de anonimização trata do mesmo jeito.
- **Como saber se deu errado:** uma conta excluída com pedido em andamento; um pedido sem dono que ainda aparece para algum cliente; um admin que conseguiu se excluir.
