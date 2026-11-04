# 0044. Correções da auditoria de segurança (04/11/2026)

- **Status:** Aceito
- **Data:** 2026-11-04
- **Relacionado:** ADR 0006 (reserva e expiração), ADR 0033 (exclusão de conta), ADR 0039 (login com Google), ADR 0041 (frete real), ADR 0042 e ADR 0043 (pré-venda); `.claude/rules/pagamentos.md` §5

## Contexto

Uma auditoria de segurança feita só pela leitura do código (skill `security-audit`, perfil standard, 32 agentes; relatório fora do repositório, em `~/security-audit-skill/E-Commerce/run-1/`) não achou falha confirmada. Deixou seis pontos para validar. Esta decisão corrige os seis, cada um com um teste que mostra o problema:

1. Um cliente podia reservar todo o estoque sem pagar e repetir a cada 31 minutos. Numa pré-venda, isso nos últimos minutos da campanha prendia as vagas depois do fim e podia derrubar o lote inteiro.
2. As cotações de frete não tinham limite. Como o token do Melhor Envio é um só para a loja, um cliente podia gastar a cota de todos; se o Melhor Envio bloqueasse o token, ninguém conseguia comprar.
3. O frete era cotado com uma leitura do carrinho e o pedido nascia de outra, feita segundos depois. Se o carrinho mudasse nesse intervalo, o pedido saía com mais itens e o frete do carrinho antigo.
4. O "Salvar datas" (ADR 0043) e o fechamento da pré-venda não se trancavam: uma reabertura no meio do fechamento deixava entrar um pedido que ficava pago, sem envio e sem reembolso, num lote cancelado.
5. Depois de excluir a conta, outra sessão aberta continuava valendo até 30 minutos (o padrão do ASP.NET para reconferir o cookie) e ainda criava carrinho, pedido e endereço.
6. Promover a admin uma conta que entra pelo Google podia transformar a sessão do Google em sessão de admin, contra a regra "admin só com senha" (ADR 0039).

## Decisão

1. **Reserva sem pagamento (decisão do autor):**
   - Cada cliente tem **no máximo 1 pedido aguardando pagamento** (`AwaitingPayment` ou `AwaitingConfirmation`). Quem garante é o banco, com um índice único parcial (`IX_orders_CustomerId_Open`); a conferência antes de cotar o frete serve só para dar a mensagem certa sem gastar a cotação.
   - Cada kit vai no **máximo 10 unidades** por pedido (`Cart.MaxQuantityPerItem`). O carrinho recusa com uma mensagem; o "Finalizar" confere de novo, por causa de carrinhos antigos.
   - Limite conhecido: nada impede alguém de criar várias contas (o cadastro é aberto e não confirma e-mail). O limite só encarece o abuso.
2. **Limite de cotações de frete:** até **10 por minuto por cliente** e **100 por minuto somando todos os clientes**, nas duas ações que cotam ("Ver opções de frete" e "Finalizar"). Passou do limite → **429** com uma mensagem, sem chamar o Melhor Envio. Os números ficam na configuração (`ShippingQuoteLimits`) e usam o limitador que já vem no ASP.NET (sem custo e sem pacote novo). O limite real do Melhor Envio **não está confirmado** na documentação oficial (só num fórum antigo: 250 por minuto); o total de 100 fica abaixo dele.
3. **Frete e carrinho:** dentro da transação, o "Finalizar" compara os itens cotados com os do carrinho. Se mudaram, recusa e mostra as opções de frete de novo, já cotadas para o carrinho novo.
4. **Fechamento da pré-venda:** a primeira coisa do fechamento, dentro da transação, é um `UPDATE` condicional que tira o produto de venda **só se** o lote ainda está sem resultado e com o fim há mais de 1 hora. Esse `UPDATE` tranca a linha do produto: uma reabertura ou uma reserva ao mesmo tempo espera, e depois falha na própria condição. A conferência de pagamentos pendentes passou para dentro da transação, depois da tranca. O "Salvar datas" também passou a gravar com condição (`WHERE` sem resultado do lote) e é recusado se o lote fechou nesse meio-tempo.
5. **Sessões:** o cookie é reconferido no banco **a cada requisição** (`SecurityStampValidatorOptions.ValidationInterval = 0`). Uma conta excluída, uma senha de admin trocada ou uma promoção a admin derrubam as outras sessões na requisição seguinte. Custo: uma leitura a mais no banco e um cookie renovado por requisição, aceitável com o tráfego da loja.
6. **Promoção a admin:** o `create-admin` **recusa** uma conta ligada ao Google ("use outro e-mail para o admin", como o admin real já faz com o `+admin`) e troca o carimbo de segurança ao promover, para que as sessões abertas antes tenham de entrar de novo.

## Alternativas consideradas

- **Só 1 pedido aberto, sem limite por kit:** um cliente ainda reservaria todo o estoque num pedido só.
- **Reservar só depois do pagamento:** a Stripe só segura cartão por ~7 dias e o Pix não tem reserva (ADR 0042); mudaria o desenho do ADR 0006 inteiro.
- **Guardar a cotação em cache:** reduz chamadas, mas não impede um cliente de variar o CEP; o limite por cliente resolve com menos código.
- **Token de concorrência no produto (`xmin`):** resolveria a corrida do fechamento, mas toda reserva muda a linha do produto e faria as edições do admin falharem à toa.
- **Reconferir o cookie a cada 1 minuto:** quase a mesma proteção com menos leituras; a janela de 1 minuto ainda deixaria a sessão de uma conta excluída gravar dados.

## Consequências

- **Positivas:** os seis pontos da auditoria ficam fechados por regra no banco ou no servidor, cada um com teste; a regra "admin só com senha" passa a valer também na promoção.
- **Negativas / custos:** quem fecha a aba do pagamento só pode fazer outro pedido depois de pagar ou de o pedido expirar (até 31 minutos), e a mensagem aponta para "Meus pedidos"; uma migration nova (o deploy falha se houver, em produção, dois pedidos abertos do mesmo cliente, o que não deve existir porque eles expiram em 31 minutos); um 429 em texto simples para quem cota demais; uma leitura a mais no banco por requisição logada.
- **Como saber se deu errado:** um cliente com dois pedidos aguardando pagamento; um pedido com mais de 10 unidades de um kit; um pedido com frete de um carrinho diferente; um pedido pago num lote cancelado; uma sessão que continua depois da exclusão da conta; um admin com login do Google ligado; 429 no log para clientes comuns.
