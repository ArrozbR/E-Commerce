# 0041. Frete real pelo Melhor Envio: o cliente escolhe o serviço, peso e medidas em cada produto

- **Status:** Aceito
- **Data:** 2026-11-03
- **Relacionado:** substitui o ADR 0024 (frete por estado); ADR 0025 (roadmap da v2), ADR 0034 (cadastro de produtos), ADR 0040 (pagamento na mesma página do endereço); `docs/DEFINICOES.md` D06 e §3

## Contexto

Item 7 da segunda parte da v2: trocar a tabela por estado (ADR 0024) por um frete calculado de verdade. A regra de custo zero vem antes de tudo.

Conferido na documentação oficial (03/11):
- **Correios:** "A API Preço é restrita, sendo liberada somente para clientes de contrato", na modalidade a faturar (manual da API Preço). Sem contrato, não há cotação.
- **Melhor Envio:** "Para integrar com o Melhor Envio é gratuito, não cobramos taxas ou mensalidades pela utilização da nossa API" (introdução da API). Existe um **sandbox** separado da produção (`sandbox.melhorenvio.com.br`, só Correios e JadLog, dados sem relação com a produção). A cotação é `POST /api/v2/me/shipment/calculate`, com `from.postal_code`, `to.postal_code` e, por produto, `width`/`height`/`length` (cm, inteiros), `weight` (kg), `insurance_value` e `quantity`; a resposta traz, por serviço, `id`, `name`, `custom_price`, `custom_delivery_time` e `company` (a documentação recomenda os campos `custom_*`). São obrigatórios os cabeçalhos `Authorization: Bearer`, `Accept`/`Content-Type: application/json` e um `User-Agent` com o nome da aplicação e um e-mail de contato. Limite: 250 requisições por minuto por usuário.
- **Autenticação:** a documentação da API descreve só o OAuth de aplicativos (token de 30 dias, renovado por outro de 45). O **painel** da conta (Integrações → Permissões de acesso) gera **tokens pessoais**, "chaves de acesso entre a sua loja virtual e o Melhor Envio", com validade (visto pelo autor no painel do sandbox, `app-sandbox.melhorenvio.com.br`: um token chamado "Melhor Envio", **criado sozinho junto com a conta**, válido de 03/11/2026 a **03/11/2027**, ou seja, 1 ano). Esse token pessoal não aparece na documentação da API, só no painel. A validade do token que a loja gerar é conferida na hora de gerar.
- O cadastro no sandbox **não pediu cartão** (autor, 03/11).

**Visto numa cotação real no sandbox (03/11, token "KeycapStore cotacao" só com `shipping-calculate`; de 74003-010 para 01310-100, um kit de 0,8 kg, 30 × 6 × 20, R$ 349,90 de seguro):** a resposta é uma lista; cada serviço traz `custom_price` como **texto com ponto** (`"30.63"`), `custom_delivery_time` em dias, `name` (`"PAC"`, `"SEDEX"`, `".Package"`, `".Com"`) e `company.name` (`"Correios"`, `"Jadlog"`); um serviço que **não atende** vem na mesma lista **sem preço e com `error`** (`"Mini Envios"`: "Valor segurado ultrapassa o limite máximo de R$ 104,62."). O token pessoal com só essa permissão cotou normalmente.

## Decisão

1. **Melhor Envio**, só a **cotação**. Comprar e imprimir a etiqueta continua fora do código: o admin compra no painel do Melhor Envio e digita o rastreio, como hoje.
2. **Token pessoal do painel**, guardado como segredo (`MelhorEnvio:Token`, `user-secrets` no PC e `.env` na VM), com `ValidateOnStart`. A loja gera **o próprio token** ("KeycapStore"), separado do que o painel criou sozinho, para poder apagá-lo sem afetar nada. Sem OAuth nem rotina de renovação: o token vence em 1 ano e é trocado à mão (anotado nas questões em aberto do `STATUS.md` com a data). Até o go-live, a produção usa o **sandbox**, como a Stripe usa o modo de teste; a URL base é configuração (`MelhorEnvio:BaseUrl`).
3. **CEP de origem** em configuração: `74003-010` (Praça Cívica, Setor Central, Goiânia; endereço público, conferido no ViaCEP). E-mail de contato do `User-Agent` também em configuração.
4. **O cliente escolhe o serviço** (decisão do autor). O endereço ganha uma etapa: **"Ver opções de frete"** → lista com transportadora, serviço, preço e prazo → **"Continuar para o pagamento"**. O formulário envia só **qual** serviço foi escolhido (o `id`), **nunca o preço**. Ao criar o pedido, o servidor **cota de novo** e usa o preço dessa nova cotação para o serviço escolhido; se o serviço não vier mais na resposta, a página mostra as opções de novo com um aviso. O total com o frete aparece na etapa de pagamento (ADR 0040), antes de pagar.
5. **Se o Melhor Envio falhar ou demorar** (erro, resposta inválida, tempo esgotado), **a loja recusa** e pede para tentar de novo (decisão do autor). Nenhum pedido é criado e nenhum estoque é reservado. Nada de tabela de reserva: o ADR 0024 sai inteiro.
6. **Peso e medidas em cada produto** (decisão do autor): `Product` ganha peso (kg) e largura/altura/comprimento (cm, inteiros), todos maiores que zero, validados no domínio e editados no cadastro do admin (ADR 0034). Os 6 kits do seed recebem valores pela migration. A cotação vai **por produtos** (a Melhor Envio monta os pacotes); o valor segurado é o preço do snapshot do pedido.
7. **O frete grátis acaba** (decisão do autor): todo pedido paga o frete cotado.
8. **Snapshot no pedido:** além do valor do frete (que já existe), o pedido guarda o **nome do serviço** (por exemplo, "Correios PAC") e o **prazo** cotado em dias, para o cliente ver e o admin saber qual etiqueta comprar. Pedidos antigos ficam sem esses campos (vieram da tabela por estado).
9. **Nada muda no pagamento:** a linha "Frete" da sessão da Stripe já sai do snapshot; webhook, conferência de valor, conciliação e reembolsos continuam iguais.
10. **Testes:** a porta da cotação fica na `Application` com uma versão falsa nos testes (como o `FakePaymentGateway`); o cliente HTTP do Melhor Envio é testado com respostas fixas (sucesso, erro, tempo esgotado, serviço que sumiu na nova cotação); nenhum teste fala com o Melhor Envio real. O caminho completo é conferido à mão no sandbox.

## Alternativas consideradas

- **Correios direto:** exige contrato.
- **Manter a tabela por estado:** de graça e sem dependência, mas o item 7 é justamente o frete real.
- **Usar a tabela por estado quando o Melhor Envio cair:** o cliente nunca fica sem comprar, mas o frete cobrado poderia ser bem diferente do real; o autor preferiu recusar.
- **A loja escolhe o serviço mais barato:** menos tela e menos validação, mas o autor quer que o cliente escolha entre preço e prazo.
- **Uma caixa padrão em configuração:** nenhuma migration nem campo novo no admin, mas os kits têm tamanhos diferentes; o autor preferiu os dados em cada produto.
- **OAuth com renovação automática:** é o caminho da documentação para aplicativos usados por várias contas; aqui há uma conta só (a da loja), e o token pessoal de 1 ano evita guardar e renovar tokens no banco.

## Consequências

- **Positivas:** o cliente paga o frete real e escolhe entre preço e prazo; o admin sabe qual serviço comprar; o pagamento não muda.
- **Negativas / custos:** a primeira dependência externa no caminho da compra além da Stripe (se o Melhor Envio cair, a loja não vende); uma etapa a mais no checkout; campos novos no produto, no admin e nas traduções; um segredo novo que vence em 1 ano.
- **Como saber se deu errado:** pedido com frete diferente da cotação do serviço escolhido; preço vindo do formulário aceito; checkout recusado com frequência por falha do Melhor Envio; o token vencer sem aviso.
