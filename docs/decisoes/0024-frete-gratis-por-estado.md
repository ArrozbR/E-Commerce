# 0024. Frete por estado: grátis no Centro-Oeste, SP e RJ; R$ 15,00 nos demais

- **Status:** Aceito
- **Data:** 2026-10-08
- **Relacionado:** ADR 0002 (substitui o item "frete fixo"), `docs/DEFINICOES.md` D06

## Contexto

O ADR 0002 e o D06 definiram um frete fixo único para a v1, para não trazer a complexidade de um cálculo real (Correios, Melhor Envio). Ao modelar o endereço de entrega do pedido (M2), o autor decidiu diferenciar o frete por estado, como acontece em lojas reais com frete grátis para algumas regiões. Isso continua sem integração externa: é só uma tabela pequena de regra de negócio.

## Decisão

1. **Frete grátis** para os estados do Centro-Oeste (**DF, GO, MT, MS**) e para **SP** e **RJ**.
2. **R$ 15,00** para todos os outros estados.
3. A regra mora no **domínio** (`ShippingPolicy`): recebe a UF e devolve o valor. Sem banco, sem configuração, sem chamada externa.
4. **O servidor calcula o frete** a partir da UF do endereço. Nenhum valor de frete vindo do formulário é aceito.
5. **O frete é snapshot no pedido**, como os preços dos itens: o `Order` guarda o valor cobrado, e o `Total` = itens + frete. Se a regra mudar, os pedidos antigos não mudam (o webhook confere o valor pago contra esse total).
6. O CEP é guardado **só com os 8 dígitos**, sem traço; o traço é só formatação na tela.

## Alternativas consideradas

- **Frete fixo único (decisão anterior):** mais simples, mas o autor quer a regra por região, que é comum em lojas reais e custa pouco.
- **Cálculo real (Correios, Melhor Envio):** continua fora de escopo (`DEFINICOES.md` §3): integração trabalhosa, dependência externa e nada a ver com o objetivo do projeto.
- **Tabela de fretes no banco ou no `appsettings`:** permitiria mudar sem deploy, mas exige tela ou processo de manutenção. Com 27 UFs e dois valores, uma regra no código, coberta por testes, é mais simples. Mudar a regra = PR + deploy.

## Consequências

- **Positivas:** regra de negócio pura e testável; o pedido continua imune a mudanças futuras na regra (snapshot).
- **Negativas / custos:** mudar estados ou valores exige um deploy; a UF passa a ser obrigatória e validada (lista das 27 siglas).
- **Como saber se deu errado:** pedido com frete diferente do que a regra daria para a UF do endereço; total na Stripe diferente do total do pedido.
