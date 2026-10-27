# 0038. Site em inglês: todas as páginas, português por padrão, formatos americanos em inglês e a Stripe no idioma do pedido

- **Status:** Aceito
- **Data:** 2026-10-27
- **Relacionado:** ADR 0025 (roadmap da v2), ADR 0034 (preço digitado no admin, convertido em pt-BR), ADR 0026 (parâmetros fixos da sessão por causa da idempotência); `docs/DEFINICOES.md` §3 e D36

## Contexto

Item 4 da segunda parte da v2: "site em inglês, troca pt-BR/en por bandeira no menu" (`DEFINICOES.md` §3). Hoje todos os textos estão em português, escritos direto nas views, nos modelos de formulário (mensagens de validação) e em alguns resultados de serviço (por exemplo, a recusa do "Finalizar" com o nome do produto). Os preços e as datas são formatados com uma cultura `pt-BR` fixa em cada view.

Conferido na documentação oficial do ASP.NET Core (27/10, "Strategies for selecting language and culture"):
- o middleware de localização escolhe a cultura na ordem endereço (`?culture=`) → cookie (`.AspNetCore.Culture`, gravado com `CookieRequestCultureProvider.MakeCookieValue`) → cabeçalho `Accept-Language` → padrão do site; a ordem e a lista de provedores podem ser trocadas;
- `UICulture` escolhe os textos (arquivos de recurso); `Culture` escolhe os formatos de número, data e moeda;
- a própria documentação diz que o `Accept-Language` "isn't an infallible way" de saber a preferência e recomenda um jeito de o usuário escolher.

Conferido na documentação da Stripe (objeto da sessão do Checkout): o parâmetro `locale` aceita `pt-BR` e `en`; vazio ou `auto` segue o navegador.

## Decisão

1. **Escopo: todas as páginas, inclusive as do admin** (decisão do autor): textos das views, menus, botões, mensagens de validação dos formulários, mensagens de resultado (`TempData`, erros do checkout, do cancelamento, da exclusão de conta) e os nomes das situações do pedido (`OrderStatusText`). Fica de fora o que não é interface: logs, ADRs e documentação (continuam em português, D36), e os **nomes dos produtos** (vêm do banco, cadastrados pelo admin; traduzir catálogo seria outro item).
2. **Português por padrão** (decisão do autor): a primeira visita é sempre em português, sem olhar o navegador. A **bandeira no menu** grava o cookie `.AspNetCore.Culture` (1 ano) e volta para a mesma página. Só `pt-BR` e `en` são aceitos; qualquer outro valor cai no português. O `Accept-Language` é removido da lista de provedores.
3. **Formatos acompanham o idioma** (decisão do autor): em inglês, números e datas no formato americano (`10/27/2026`, `R$349.90`), em português no brasileiro (`27/10/2026`, `R$ 349,90`). **A moeda é sempre o real:** o símbolo continua `R$` nos dois idiomas (nunca `$`), num formatador de preço único usado por todas as views, no lugar das culturas `pt-BR` fixas de hoje. O preço digitado no admin passa a ser lido no formato do idioma da página (`49,90` em português, `49.90` em inglês), substituindo a leitura sempre em pt-BR do ADR 0034; os testes cobrem os dois formatos e as recusas (`49.90` em português continua recusado).
4. **Textos traduzidos com os recursos do ASP.NET Core** (`IStringLocalizer`/`IViewLocalizer`, arquivos `.resx`), usando o **texto em português como chave**: só existe o arquivo em inglês; o que faltar traduzir aparece em português, nunca uma chave vazia. Mensagens de serviço que hoje são texto pronto passam a ser códigos de resultado traduzidos na camada Web (a Application não conhece idioma).
5. **A Stripe no idioma do pedido** (decisão do autor): o pedido guarda o idioma em que foi feito (`pt-BR` ou `en`, nova coluna com migration; pedidos antigos ficam `pt-BR`), e a sessão do Checkout é criada com esse `locale`. Guardar no pedido, e não ler do cookie na hora, mantém os parâmetros da sessão iguais numa nova tentativa com a mesma chave de idempotência `session-{orderId}` (mesmo motivo do `expires_at` fixo do ADR 0026).
6. **Testes:** os testes de página atuais continuam em português (o padrão). Testes novos para: a bandeira grava o cookie e volta à página; uma página em inglês mostra os textos e os formatos americanos com `R$`; uma mensagem de validação em inglês; o preço digitado no admin nos dois idiomas; a sessão da Stripe criada com o `locale` do pedido.

## Alternativas consideradas

- **Só as páginas do cliente:** menos texto para traduzir; o autor preferiu o site inteiro.
- **Seguir o navegador na primeira visita:** natural para estrangeiros, mas menos previsível; a documentação avisa que o cabeçalho não é confiável.
- **Formato brasileiro também em inglês:** mudaria só os textos e manteria a leitura de números igual em todo lugar (menos risco no preço digitado); o autor preferiu o formato natural de quem lê em inglês.
- **URL com o idioma (`/en/...`):** melhor para buscadores, mas muda todas as rotas, os testes e os `success_url`/`cancel_url`; o cookie atende a bandeira do `DEFINICOES.md`.
- **Stripe pelo navegador (`auto`):** nada muda no pagamento, mas o cliente pode trocar de idioma ao pagar.

## Consequências

- **Positivas:** loja utilizável por quem não lê português; textos num lugar só por idioma; preço sempre com `R$`.
- **Negativas / custos:** todas as views e mensagens passam pelo localizador (trabalho grande, em vários passos); o formato do preço digitado no admin muda conforme o idioma (risco já conhecido, coberto por testes); uma migration no pedido; textos novos daqui em diante precisam de tradução.
- **Como saber se deu errado:** um preço com `$` ou `US$`; um preço digitado no admin gravado 100 vezes maior ou menor; uma página em inglês com textos em português (falta de tradução, não erro grave); a página da Stripe num idioma diferente do pedido.
