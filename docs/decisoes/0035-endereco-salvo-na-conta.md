# 0035. Endereço salvo na conta: um por cliente, só para preencher o checkout

- **Status:** Aceito
- **Data:** 2026-10-26
- **Relacionado:** ADR 0025 (roadmap da v2), ADR 0033 (exclusão de conta), ADR 0024 (frete por estado); `docs/DEFINICOES.md` §3, §6, D06

## Contexto

Item 2 da segunda parte da v2. Hoje o cliente digita o endereço inteiro em toda compra. O pedido guarda uma **cópia** do endereço (snapshot, desde o M2), e isso precisa continuar:
- o pacote foi enviado para o endereço daquela hora, e o admin e uma eventual reclamação precisam ver esse endereço, não o atual;
- o pedido é registro da venda e fica guardado pelo prazo legal (Q5), mesmo depois de a conta ser excluída.

## Decisão

1. **Um endereço salvo por cliente** (decisão do autor). Ele é o endereço **padrão**: quando existe, o formulário do checkout já vem preenchido com ele.
2. **Só preenche o formulário.** O pedido continua guardando a própria cópia (`ShippingAddress`). Mudar ou apagar o endereço salvo **nunca** mexe em pedidos.
3. **Como entra:** no checkout, uma caixa **"Salvar este endereço"**; marcada, o endereço do pedido **substitui** o salvo, e só depois de o pedido ser criado (o endereço já passou pelas validações do `ShippingAddress`). Em "Minha conta", o cliente vê e **remove** o endereço salvo.
4. **Banco:** tabela própria com o `CustomerId` como chave (um por cliente garantido pelo banco, como o carrinho). Reaproveita o `ShippingAddress` (as mesmas regras de UF, CEP e tamanhos). A entidade (`SavedAddress`) fica na pasta `Orders`, ao lado do `ShippingAddress`, sem criar módulo novo.
5. **Exclusão de conta (ADR 0033):** o endereço salvo é **apagado na mesma transação** que apaga a conta. Ao contrário do snapshot do pedido, ele não tem obrigação legal de guarda; continua só nos backups até 29 dias, como o resto da conta.
6. **Admin:** não vê nem usa endereços salvos (a página de envio continua mostrando o endereço do pedido).

## Alternativas consideradas

- **Vários endereços (até 5) com um padrão:** mais útil para quem envia para casa e trabalho, mas exige escolher na lista, trocar o padrão e manter "sempre exatamente um padrão". O autor preferiu começar com um.
- **Pedido apontando para o endereço salvo (sem cópia):** mudaria pedidos antigos e quebraria a exclusão de conta e o prazo legal.
- **Salvar só pela "Minha conta":** menos mudança no checkout, mas o cliente teria de digitar o endereço duas vezes.
- **Salvar dentro da transação do "Finalizar":** juntaria uma conveniência à peça central da reserva de estoque. Salvar depois, separado, mantém o `CheckoutService` igual; se o salvamento falhar, o pedido já existe e nada se perde.

## Consequências

- **Positivas:** a segunda compra não exige digitar o endereço; nenhuma mudança no pedido, na reserva ou no pagamento.
- **Negativas / custos:** mais um dado pessoal guardado (entra na política de privacidade); mais uma tabela apagada na exclusão de conta; duas abas salvando ao mesmo tempo podem disputar o primeiro registro (o banco recusa o segundo).
- **Como saber se deu errado:** um pedido cujo endereço muda depois de criado; um endereço salvo que sobrevive à exclusão da conta; um cliente vendo o endereço de outro.
