# 0034. Cadastro de produtos pelo admin: estoque por ajuste atômico e produto ativo/inativo

- **Status:** Aceito
- **Data:** 2026-10-22
- **Relacionado:** ADR 0025 (roadmap da v2), ADR 0028 (página mínima de envio), `docs/DEFINICOES.md` D05, §12; `.claude/rules/pagamentos.md` §5

## Contexto

Primeiro item da segunda parte da v2 (ordem decidida em 22/10). Hoje os produtos só mudam por migration (seed, D05). O admin precisa criar produtos, mudar nome e preço, repor estoque e tirar um produto de venda pela página de admin.

Dois riscos:
1. **Estoque editado com "ler, calcular e gravar".** Se o admin abre a página com estoque 5, um cliente compra 1 (o banco vai para 4) e o admin salva "15", o banco fica com 15, e não com 14. A compra some do estoque e a loja vende uma unidade que não existe. É a mesma corrida que a reserva atômica do M2 resolve.
2. **Tirar de venda** sem quebrar pedidos antigos: um produto não pode ser apagado (os itens de pedido apontam para ele, FK `Restrict`).

## Decisão

1. **Campos:** continuam só nome, preço e estoque. Descrição e fotos ficam para a v3.
2. **Criar e editar nome/preço:** pela entidade (`Product`), com as mesmas regras do construtor (nome de 1 a 150 caracteres, preço > 0).
   - Mudar o preço **não** mexe em pedidos (eles guardam o snapshot desde o M2). Um carrinho aberto passa a mostrar o preço novo, porque o carrinho nunca guardou preço.
   - Só as colunas alteradas vão para o `UPDATE` (o EF Core acompanha cada propriedade), então editar nome/preço não sobrescreve o estoque.
3. **Estoque por ajuste, nunca por valor final.** O admin informa quanto entrou (`+10`) ou saiu (`-2`). O banco aplica numa única operação atômica:
   ```sql
   UPDATE products SET stock = stock + @delta WHERE id = @id AND stock + @delta >= 0
   ```
   Zero linhas afetadas → a saída deixaria o estoque negativo → recusada. Uma compra no meio nunca se perde. A tela não tem campo "estoque = N".
4. **Ativo / inativo** (`Product.IsActive`, verdadeiro para todos os produtos existentes):
   - inativo **some do catálogo** e **não entra no carrinho** (404, como um produto inexistente);
   - **nenhuma reserva acontece para produto inativo:** a condição da reserva passa a ser `WHERE id = @id AND stock >= @qty AND is_active`, tanto no "Finalizar" quanto na nova reserva do pagamento atrasado (`TryReserveStockAsync`). Uma regra só: inativo não está à venda;
   - um carrinho com item inativo falha no "Finalizar" com a mensagem de indisponível, e nada é reservado (a transação inteira volta, como hoje);
   - **pedidos já reservados ou pagos não mudam:** a separação, o envio, o cancelamento e as devoluções continuam normais, e a devolução de estoque (cancelamento, expiração, devolução) soma no produto mesmo inativo;
   - reativar devolve o produto à venda com o estoque que ele tiver.
5. **Página:** dentro da área de admin (`[Authorize(Roles = Roles.Admin)]`), um `POST` por ação (criar, editar, ajustar estoque, ativar, desativar), como na página de envio. Nenhum `PATCH` genérico.
6. **D05 substituído a partir da v2:** o seed continua criando os 6 kits iniciais, e o resto passa a ser pela página.

## Alternativas consideradas

- **Digitar o estoque final com token de concorrência:** mais intuitivo, mas o admin receberia "o estoque mudou, recarregue" toda vez que uma venda acontecesse enquanto a página estava aberta, e a regra de "nunca ler, calcular e gravar" teria uma exceção.
- **Tirar de venda zerando o estoque:** não exigiria mexer na reserva, mas apagaria a informação de quantas unidades existem e confundiria "esgotado" com "fora de venda".
- **Excluir produto:** impossível com pedidos apontando para ele; e um produto sem pedidos não precisa de exclusão, basta ficar inativo.
- **Inativo só some do catálogo, sem mexer na reserva:** quem já tivesse o item no carrinho conseguiria comprar um produto fora de venda.

## Consequências

- **Positivas:** o admin cuida do catálogo sem migration; o estoque continua protegido pela mesma ideia da reserva do M2.
- **Negativas / custos:**
  - mudança na reserva de estoque (regra inegociável), com testes de integração novos;
  - coluna `IsActive` (migration com padrão verdadeiro);
  - um pagamento atrasado de produto que ficou inativo vira `PaidOutOfStock` e é reembolsado automaticamente.
- **Como saber se deu errado:** um pedido novo com produto inativo; um estoque negativo; uma compra que "some" depois de uma reposição.
