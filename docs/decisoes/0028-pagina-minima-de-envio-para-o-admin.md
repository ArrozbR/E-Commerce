# 0028. Página mínima de envio para o admin (em vez de endpoints sem interface)

- **Status:** Aceito
- **Data:** 2026-10-15
- **Relacionado:** `docs/DEFINICOES.md` §2 (Envio), §7 (máquina de estados), D05, D16; ADR 0013, ADR 0025; `.claude/rules/pagamentos.md` §4

## Contexto

O `DEFINICOES.md` previa que o admin avançaria os pedidos (`Paid → Picking → Shipped → Delivered`) por **endpoints de admin mínimos, sem interface**, autorizados pelo papel `Admin`.

Na hora de começar o M5, apareceu um problema prático:
- O site usa **login por cookie** e **antiforgery global** (`AutoValidateAntiforgeryTokenAttribute`, desde o PR #27). Todo `POST` exige o cookie e um token que só existe dentro de uma página do próprio site.
- Sem nenhuma página, o admin teria que chamar os endpoints pelo console do navegador (F12), copiando o token de outra página, a cada pedido. Ou então teríamos que desligar o antiforgery nesses endpoints, abrindo uma brecha de CSRF justamente na área mais sensível.
- O critério de pronto da v1 inclui "o admin leva o pedido até `Entregue`". Isso precisa ser algo que se demonstre num portfólio.
- O ADR 0025 deixou para a v2 a **tela de admin com cadastro de produtos**. Ele não fala de uma página operacional de envio.

## Decisão

O M5 terá **uma página mínima de envio**, `GET /admin/orders`, acessível **só ao papel `Admin`**, com **um formulário `POST` por ação**. Os endpoints continuam sendo **um por ação**, como o `DEFINICOES.md` exige.

1. **A página lista a fila de trabalho:** pedidos `Paid`, `Picking` e `Shipped`, do mais antigo para o mais novo. Para cada um: número, data, total, itens e o **endereço de entrega** (sem ele, não há como enviar).
2. **Os endpoints, cada um com o seu botão:**
   - `POST /admin/orders/{id}/start-picking` → `StartPicking()`
   - `POST /admin/orders/{id}/ship` (campo `trackingCode`) → `MarkShipped(trackingCode)`
   - `POST /admin/orders/{id}/deliver` → `MarkDelivered()`

   Depois de cada ação, o admin volta para a lista.
3. **Segurança:**
   - `[Authorize(Roles = "Admin")]` no controller;
   - visitante sem login → página de login;
   - cliente logado sem o papel → **403** (e não a página de login, porque logar de novo não adianta);
   - o antiforgery continua valendo, porque os formulários são do próprio site.
4. **Transição inválida → 409**, pelo filtro de exceções do PR #61 (por exemplo, "enviar" um pedido que não está em separação, ou clicar duas vezes).
5. **Fora desta decisão, de propósito:**
   - nenhuma ação de pagamento na página: nada de marcar como pago, reembolsar ou expirar. Isso continua exclusivo do webhook e dos jobs;
   - nada de cadastro ou edição de produtos (v2, ADR 0025);
   - nada de busca, filtros ou paginação. O volume da v1 é pequeno.
6. **Aparência:** a mesma do resto do site (o modelo padrão). O visual é da v3.

## Alternativas consideradas

- **Endpoints puros, chamados pelo console do navegador:** é fiel ao texto original, mas é desajeitado no dia a dia e na demonstração. Também convida a desligar o antiforgery "só nesses endpoints", o que seria um erro de segurança.
- **Comandos na VM, como o `create-admin`** (`docker compose run --rm app ship --order X --tracking Y`): não abre nenhuma superfície web nova, mas exige SSH para cada pedido. Também não usa o papel `Admin`, que o `DEFINICOES.md` prevê para o envio, e não é algo que se demonstre num portfólio.
- **Tela de admin completa** (com produtos, busca e filtros): é escopo da v2 (ADR 0025).

## Consequências

- **Positivas:**
  - usa o login, o papel e o antiforgery que já existem;
  - testável como as outras páginas (cliente comum → 403, admin → 200, ação repetida → 409);
  - o fluxo inteiro da v1 passa a ser demonstrável no navegador, da compra até "Entregue".
- **Negativas / custos:**
  - uma página e um controller a mais para manter;
  - a página mostra **endereços de clientes**, então ela só existe atrás do papel `Admin`, e nenhum log registra esses dados (regra de `pagamentos.md` §7).
- **Como saber se deu errado:** se a página começar a ganhar ações que não são de envio (editar pedido, mexer em pagamento, cadastrar produto), ela virou a "tela de admin" da v2 antes da hora. Nesse caso, parar e decidir em um novo ADR.
