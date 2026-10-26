# 0036. Endereço salvo também cadastrado e alterado pela "Minha conta"

- **Status:** Aceito
- **Data:** 2026-10-26
- **Relacionado:** ADR 0035 (endereço salvo na conta), que este complementa; `docs/DEFINICOES.md` §3, §6

## Contexto

O ADR 0035 definiu que o endereço salvo entra **só pelo checkout** (caixa "Salvar este endereço") e que a "Minha conta" apenas mostra e remove. No teste em produção (26/10), o autor achou estranho a página da conta dizer "Nenhum endereço salvo" sem oferecer um jeito de cadastrar: para salvar um endereço, o cliente precisava estar comprando.

A opção escolhida no ADR 0035 se chamava "Caixa no checkout + Minha conta", e o nome dava a entender mais do que a decisão registrou.

## Decisão

1. **A "Minha conta" também cadastra e altera** o endereço salvo, numa página própria (`/Account/Address`): formulário vazio quando não há endereço, preenchido quando há. Salvar **substitui** o endereço anterior (continua um por cliente, ADR 0035).
2. **A caixa do checkout continua.** São dois caminhos para o mesmo `SaveAsync` do serviço, com as mesmas validações.
3. **Os campos do formulário ficam num lugar só:** os campos do endereço e as suas validações passam para um modelo comum (`AddressViewModel`) e uma parte de tela comum, usados pelo checkout e pela conta. Assim um limite de tamanho ou uma regra de CEP nunca fica diferente entre as duas telas.
4. Todo o resto do ADR 0035 continua valendo: o pedido guarda a própria cópia, a exclusão de conta apaga o endereço salvo na mesma transação, o admin não vê.

## Alternativas consideradas

- **Formulário dentro da própria página "Minha conta":** a página já tem o formulário de exclusão de conta, com outro modelo e outra validação. Dois formulários com modelos diferentes na mesma página complicam as mensagens de erro (o erro de um aparece no outro). Uma página separada é mais simples.
- **Copiar os campos do checkout para a nova tela:** mais rápido, mas as regras ficariam duplicadas e poderiam divergir.
- **Deixar como estava (ADR 0035):** funciona, mas o cliente que quer cadastrar o endereço antes da primeira compra não consegue.

## Consequências

- **Positivas:** o cliente cadastra ou corrige o endereço a qualquer momento; o checkout e a conta usam exatamente as mesmas regras.
- **Negativas / custos:** uma página e uma ação a mais; o formulário do checkout é reorganizado (os testes de página do checkout precisam continuar passando).
- **Como saber se deu errado:** um endereço aceito numa tela e recusado na outra; um endereço salvo pela conta que não aparece no checkout.
