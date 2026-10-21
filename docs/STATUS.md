# Status

> Atualizado pelo comando `/encerrar`. Lido pelo `/retomar`.

**Marco atual:** 🎉 **v2, parte legal (ADR 0025) concluída em 21/10/2026**: cancelamento (ADR 0031), direito de arrependimento (ADR 0032) e exclusão de conta (ADR 0033), todos em produção. O go-live ainda depende de itens fora do código (prazo de guarda, termos, política de privacidade, nota fiscal; `DEFINICOES.md` §11). Próximo: melhorias pequenas e a escolha da ordem da segunda parte da v2. 🎉 **v1 concluída em 15/10/2026**: todos os critérios de pronto do `DEFINICOES.md` §1 cumpridos em produção. **M6 concluído em 15/10/2026** (`/health`, Serilog, Dependabot, backup diário criptografado na Cloudflare R2 com restauração testada, healthchecks.io, UptimeRobot). **M5 concluído em 15/10/2026** (página mínima de envio só para o papel Admin, separar → enviar com rastreio → entregar, comando `reset-admin-password`; pedido levado até "Entregue" em produção). **M4 concluído em 14/10/2026** (concorrência otimista, expiração, Pix, pedidos órfãos, pagamento atrasado com reembolso automático, filtro 409/404/400). **M3 concluído em 12/10/2026** (sessão do Checkout, webhook assinado e idempotente, confirmação do pagamento, página de retorno; compra paga com cartão em produção). **M2 concluído em 08/10/2026** (carrinho, pedido com snapshot e endereço, "Finalizar" com reserva atômica, teste de concorrência). **M1 concluído em 06/10/2026.** **M0 concluído em 01/10/2026.** Loja no ar em https://keycapstore.duckdns.org.
**Data de início da v1:** 30/09/2026
**Horas acumuladas:** M0: 7h (estimativa era 25–35h) · M1: 8,5h (estimativa era 15–25h) · M2: 6h (estimativa era 20–30h) · M3: 5h (estimativa era 20–30h) · M4: 5,5h (estimativa era 15–25h) · M5: 3h (estimativa era 8–12h) · M6: 2h (estimativa era 15–20h) · **total da v1: 37h** · **v2 até agora: 3,5h** (parte legal inteira) · **total do projeto: 40,5h**
**Estimativa da v1:** ~~120–180h~~ → ~~55–85h (08/10)~~ → ~~45–60h (12/10)~~ → ~~40–50h (14/10)~~ → ~~40–45h (15/10)~~ → **real: 37h em 16 dias** (30/09 a 15/10). Todos os marcos saíram com 10% a 30% do estimado. Para a v2, estimar com esse histórico.

## v2 (ADR 0025)

**Parte legal (libera o go-live):**
- [x] **Cancelamento antes do envio** (PR #73, ADR 0031, 19/10): `Cancel()` de `Paid`/`Picking` (só o dono; outro cliente → 404, depois do envio → 409), estoque devolvido na mesma transação, reembolso pelo job (`RefundProcessor`, que substituiu o `OutOfStockRefunder`); o `pi_` passa a ser guardado em toda confirmação e, nos pedidos antigos, é buscado na Stripe pela sessão. **Escrito pelo autor:** `Cancel`/`MarkRefunded`/`??=` no `Order`, `RefundProcessor`, `CancellationService`. **Em produção:** pedido `6ea74fa5` (Pix de 15/10, sem `pi_` guardado) cancelado → reembolsado pela busca da sessão
- [x] **Direito de arrependimento** (PR #74, ADR 0032, 19/10): texto do CDC art. 49 e do Decreto 7.962 art. 5º conferido no Planalto; `DeliveredAt` (migration `AddOrderDeliveredAt`); `Withdraw(now)` de `Shipped` ou `Delivered` há até 7 dias (`CanWithdraw`, mesma regra para o botão e o domínio) → `Withdrawn`; job reembolsa na hora → `AwaitingReturn`; admin confirma a devolução e escolhe "volta ao estoque" → `Returned`. **Escrito pelo autor:** as 5 lacunas do `Order`, o `RefundProcessor`, o `WithdrawalService` e o `ConfirmReturnAsync` (este sem esqueleto, só com a explicação). **256 testes.** **Em produção:** pedido `f3cccfbd` (cartão) entregue → desistência → reembolso em ~3 min → devolução com estoque de 1 para 2
- [x] **Exclusão de conta** (PR #76, ADR 0033, 21/10): LGPD art. 5º, 15, 16 e 18 conferidos no Planalto (texto colado pelo autor). Página "Minha conta" com a senha atual (conta para o bloqueio de 3 tentativas); admin recusado; bloqueio com pedido não finalizado (`IsFinished(now)`: `Expired`/`Failed`/`Refunded`/`Returned`, ou `Delivered` fora dos 7 dias); numa transação, pedidos sem dono (`DetachCustomer`, `CustomerId` opcional, migration `MakeOrderCustomerIdOptional`), carrinho e conta apagados; sessão encerrada. Nome e endereço ficam até o prazo legal (job pendente da Q5). **Escrito pelo autor:** `IsFinished`/`DetachCustomer` (faltou o `throw`, visto antes de rodar) e as 4 lacunas do `AccountDeletionService`, todas certas. **279 testes.** **Em produção:** senha errada → recusa; pedido pago → recusa; cancelado e reembolsado → conta excluída, login recusado, pedido `367475d3` no banco com `CustomerId` vazio e o nome ainda guardado

**Depois:** tela de admin com cadastro de produtos · endereços salvos · conciliação · site em inglês · login com Google · Payment Element · frete real · pré-venda (ordem sugerida em 19/10, ainda não decidida)

## Marcos da v1

- [x] **M0: esqueleto que anda** (25–35h estimadas; **7h reais**, concluído em 01/10/2026)
- [x] **M1:** catálogo (seed) + Identity + `create-admin` (15–25h estimadas; **8,5h reais**, concluído em 06/10/2026)
- [x] **M2:** carrinho, pedido, reserva atômica + testes de concorrência (20–30h estimadas; **6h reais**, concluído em 08/10/2026)
- [x] **M3:** Stripe Checkout com cartão + webhook idempotente + página de retorno (20–30h estimadas; **5h reais**, concluído em 12/10/2026)
- [x] **M4:** Pix, expiração, falha, reembolso por falta de estoque (15–25h estimadas; **5,5h reais**, concluído em 14/10/2026)
- [x] **M5:** endpoints de admin para o envio (8–12h estimadas; **3h reais**, concluído em 15/10/2026)
- [x] **M6:** backups, monitoramento, checklist de go-live (15–20h estimadas; **2h reais**, concluído em 15/10/2026)

## Critérios de pronto da v1 (`DEFINICOES.md` §1), todos em produção

- [x] catálogo (M1) · conta (M1) · compra com cartão `4242` → `Pago` via webhook (M3, 12/10) · **compra com Pix** (`succeed_immediately@…`) → `Pago` via webhook, pela loja (pedido `6ea74fa5`, 15/10) · admin leva o pedido até `Entregue` (M5, 15/10) · HTTPS público (M0) · CI verde · **backup restaurado com sucesso** (15/10, contagem por status igual à de produção)

## Checklist do M6

- [x] Q4 respondida na documentação oficial: B2 (10 GB, sem cartão) × R2 (10 GB-mês, cartão, sem teto de gasto); o autor escolheu a **R2** (ADR 0030); healthchecks.io (Hobbyist, 20 checks) e UptimeRobot (50 monitores, 5 min)
- [x] `/health` (PR #67): `DatabaseHealthCheck` (`CanConnectAsync`) + `DiskSpaceHealthCheck` (< 85%), 200 `Healthy` / 503 `Unhealthy` sem detalhes; testes de borda 84/85% e banco fora → 503
- [x] Backup: par de chaves `age` (privada só na pasta `OneDrive\Documentos\Backups Keycapstore`), `deploy/backup.sh` (`pg_dump` custom → `age` → `/var/backups/keycapstore`, 2 cópias locais, `.partial` + `mv`, `pipefail`) (PR #68); **restauração** no banco do PC com contagem igual à de produção
- [x] Envio para a R2 (PR #69, ADR 0030): bucket `keycapstore-backups` (Standard, sem acesso público), ciclo de vida `daily/` 8 dias e `weekly/` 29 dias, bucket lock 7 e 28 dias, token da conta só de objetos e só desse bucket, alerta de orçamento de US$ 0,01; `rclone` com `no_check_bucket` e `no_head` (o `HEAD ?versionId` recebia 501 da R2; PR #71); timer systemd às 02:30; parte 7 do `bootstrap-vm.sh`; `docs/runbooks/backup.md`
- [x] healthchecks.io (`keycapstore-backup`, 1 dia + 2 h de folga, alerta por e-mail; recriado depois de a primeira URL aparecer no chat) e UptimeRobot no `/health` (5 min, e-mail)
- [x] Serilog em JSON em produção (texto no PC), SQL do EF só em aviso/erro; Dependabot semanal (NuGet, Docker, actions) (PR #69). O primeiro PR do Dependabot (#70, pacotes de teste) está aberto com CI verde
- [x] Alertas de webhook: a Stripe envia e-mail sozinha quando um destino falha (documentação)

## Checklist do M5

- [x] ADR 0028: **página mínima de envio** em vez de endpoints sem interface (o antiforgery global e o login por cookie tornariam os endpoints puros desajeitados ou inseguros); `DEFINICOES.md` atualizado (D05, Envio)
- [x] `StartPicking`, `MarkShipped(trackingCode)` (vazio recusado, até `Order.MaxTrackingCodeLength` = 50), `MarkDelivered`, **escritos pelo autor** (o `TrackingCode = trackingCode` faltou de primeira; o teste pegou); 8 testes de unidade; migration `AddOrderTrackingCode`
- [x] `/admin/orders` (PR #64): `[Authorize(Roles = Roles.Admin)]` (nome do papel na Application), fila `Paid`/`Picking`/`Shipped` do mais antigo ao mais novo com o endereço, link "Envio" no menu só para admins; cliente logado → **403** (`OnRedirectToAccessDenied`), visitante → login. Sabotagens: sem o papel → 200 para o cliente; sem o ajuste → 302
- [x] Botões (PR #64): `ShippingService`, **escrito pelo autor** (lambdas `Action<Order>`, `?? throw new OrderNotFoundException`), um `POST` por ação, 404/409/400 pelo filtro. Testes: fluxo completo, clique duplo → 409, rastreio em branco → 400, pedido inexistente → 404, cliente → 403, 5 cliques simultâneos → só 1 passa. `PageClient` em `Fixtures` (4ª cópia dos ajudantes de página)
- [x] Comando `reset-admin-password` (PR #65, ADR 0029): só para quem já é Admin, senha nova gerada e mostrada uma vez, troca numa transação, `SecurityStamp` muda (sessões antigas caem). **A senha que vazou em 06/10 não vale mais** (trocada duas vezes em produção, a segunda sem nenhum print)
- [x] **Em produção:** pedido `04bd9d69` levado de Pago → Em separação → Enviado (`BR000000001BR`) → Entregue pelo admin; `pedro2@test.com` recebeu 403 em `/admin/orders`. **216 testes**

## Checklist do M4

- [x] Token de concorrência no `Order` (`xmin` do PostgreSQL, migration sem DDL) + `ConfirmPayment` que não faz nada se já estiver `Paid`; `checkout.session.expired` → `Expire()` + estoque devolvido na mesma transação. Teste com 10 eventos `expired` **diferentes** em paralelo: sem o token o estoque ia de 3 para 23, com o token fica certo. ADR 0027 (eventos snapshot na versão `endive`). Em produção, `stripe checkout sessions expire` levou o pedido a `Expired` (PR #55)
- [x] `async_payment_succeeded` / `async_payment_failed` (Pix), **escrito pelo autor** (6 lacunas certas): o ramo `unpaid` só vale a partir de `AwaitingPayment` (evento fora de ordem não desfaz pagamento); falha → `Failed` + estoque devolvido (PR #56)
- [x] **Q1b confirmada em produção:** Pix com `fill_never@…` nunca pago → `checkout.session.expired` às 19:51:27, 31 min depois da criação (19:20:26), pedido `Expired`
- [x] Devolução de estoque num lugar só (`StockRelease.ReleaseStockAsync`, refatoração sem mudar comportamento) + job do **D35** (`OrphanOrderReleaser`, **escrito pelo autor**, 6 lacunas; `<` × `>` corrigido pelos testes): `AwaitingPayment` sem `StripeSessionId` há mais de 10 min → `Failed` + estoque, uma transação por pedido, confere de novo depois de carregar, `ChangeTracker.Clear()` na concorrência. `BackgroundService` a cada 1 min, desligado nos testes (`PostgresFixture`). Sabotagem sem a transação → estoque 13 em vez de 5. Em produção, liberou sozinho o pedido preso desde 09/10 (PR #57)
- [x] Pagamento depois da reserva liberada (PR #58), **escrito pelo autor**: `ConfirmLatePayment`, `MarkOutOfStock(paymentIntentId)`, `MarkRefunded` (9 testes de unidade), coluna `StripePaymentIntentId` (migration `AddOrderPaymentIntentId`); webhook reserva de novo com **savepoint** (`TryReserveStockAsync`) → `Paid` ou `PaidOutOfStock` + `LogError`; outros estados → ignorado com 200 (antes virava 500). Sabotagem sem o `RollbackToSavepointAsync` → reserva pela metade (3 em vez de 5). **185 testes.** Pela documentação da Stripe, uma sessão expirada não aceita pagamento: é defesa
- [x] Reembolso automático (PR #60): `IPaymentGateway.RefundAsync` (`RefundService`, `StripeRefundFactory` com a chave `refund-{orderId}`), `OutOfStockRefunder` **escrito pelo autor** (faltou o `SaveChangesAsync` de primeira; os testes pegaram); falha → continua `PaidOutOfStock` + `LogError` e nova tentativa no minuto seguinte. Os dois jobs viraram um só (`OrderMaintenanceJob`, cada tarefa no seu `try`). **Reembolso real no sandbox:** pagamento com `4242`, pedido posto em `PaidOutOfStock` à mão no banco do PC, job reembolsou em menos de 1 min (Dashboard "Reembolsado", pedido `Refunded`)
- [x] Filtro de exceções (PR #61): `[DomainExceptionFilter]` nos controllers de páginas (não no webhook, que precisa responder 500): `InvalidOrderTransitionException` → 409, `ProductNotFoundException` (nova, conferida no `CartService`) e `CartItemNotFoundException` → 404, `ArgumentException` → 400, o resto continua 500. Sabotagem sem o atributo → 500 de volta. Em produção, `ChangeQuantity` de produto alheio pelo console → **404**. **196 testes**
- [x] (fora do plano) .NET **10.0.13** (PR #59): alerta de segurança CVE-2026-96960 publicado em 13/10 fez o build falhar (`NU1903`, avisos como erros). Migrations não foram alteradas (a anotação `ProductVersion` fica como estava)

## Checklist do M3

- [x] `StripeOptions` (`SecretKey` obrigatória e no formato `sk_test_`/`sk_live_`) com `ValidateDataAnnotations` + `ValidateOnStart`; testes provam que a aplicação não sobe sem a chave nem com uma `pk_` por engano (sabotagem sem `ValidateOnStart` → 2 falhas). Chave de teste em `user-secrets` no PC e em `STRIPE_SECRET_KEY` no `.env` da VM (`compose.prod.yaml` recusa subir sem ela) (PR #47)
- [x] `Order.StripeSessionId` + `AttachStripeSession`, **escrito pelo autor**: a mesma sessão de novo não faz nada, outra sessão é recusada, só `AwaitingPayment` recebe sessão; migration `AddOrderStripeSessionId`; 7 testes unitários + 1 de persistência (PR #48)
- [x] `IPaymentGateway` (Application) + `StripePaymentGateway` (Infrastructure, `Stripe.net` 53.0.0) + `StripeCheckoutSessionFactory`: sessão montada do **snapshot** do pedido (centavos em `brl`, linha "Frete" só se houver), `client_reference_id` e `metadata.order_id`, Pix 1800 s, chave de idempotência `session-{orderId}`; `expires_at` = **criação do pedido + 31 min** (ver Questões em aberto); 8 testes (branch `feat/stripe-gateway`)
- [x] `StartPaymentAsync`, **escrito pelo autor**: só o dono do pedido; se a Stripe falhar, **compensação** numa transação (`MarkFailed` + estoque devolvido em ordem de `ProductId`); se der certo, `AttachStripeSession`. `FakePaymentGateway` nos testes (padrão em toda fábrica: nenhum teste fala com a Stripe real); 3 testes, sabotagem sem a devolução do estoque (branch `feat/stripe-gateway`)
- [x] "Finalizar compra" redireciona para a página de pagamento da Stripe (`success_url` = página do pedido por enquanto, `cancel_url` = carrinho, `Request.Scheme` correto atrás do Nginx por `ASPNETCORE_FORWARDEDHEADERS_ENABLED`); Stripe fora → página do pedido "Pagamento não aprovado"; sabotagem sem o `if` → 500. **139 testes** (PR #49, junto com o ADR 0026)
- [x] Pagamento com o cartão `4242` no sandbox, no PC e em produção (volta em `https`, valores do snapshot, Pix e cartão vindos do Dashboard)
- [x] Webhook `POST /webhooks/stripe` (PR #50): corpo bruto + `EventUtility.ConstructEvent`, `[IgnoreAntiforgeryToken]`, 400 sem assinatura, com outro segredo, com corpo alterado ou com mais de 5 min; `Stripe:WebhookSecret` obrigatório (`ValidateOnStart`), no `.env` da VM e no compose. Destino no Dashboard em **`2026-09-30.endive`** (a família do Stripe.net 53; a conta é `dahlia`, e no PC usa-se `stripe listen --latest`). Testes assinam eventos localmente com HMAC-SHA256
- [x] `ProcessedStripeEvents` (PR #51), handler **escrito pelo autor**: INSERT do `event.id` (chave primária) primeiro, na transação; violação de unicidade → `Duplicate` → 200 sem efeito; outros erros → 500. Testes de duplicata em sequência e em paralelo (5 simultâneas). Em produção, um "Reenviar" no Dashboard gerou 200 e **uma** linha só
- [x] Confirmação do pagamento (PR #52), **escrita pelo autor**: `order_id` dos metadados, pedido, conferência de `amount_total` (centavos, `ToCents`) e `currency`, `paid` → `ConfirmPayment`, `unpaid` → `MarkAwaitingConfirmation`; divergente → `LogError` + 200; pedido inexistente → `LogWarning` + 200; motivo da recusa de assinatura no log. Pedido "Pago" em produção
- [x] Página de retorno `/orders/{id}/return` + `GET /orders/{id}/status` (JSON), só para o dono (404 para os outros), consulta a cada 2 s por até 1 min; `success_url` aponta para ela; `OrderStatusText` num lugar só (PR #53). **156 testes.** Em produção, a página mudou sozinha para "Pago"

## Checklist do M2

- [x] Aquecimento: `PostgresFixture.CreateFactory()` / `CreateMigratedFactoryAsync()` substituem as 7 cópias da montagem da `WebApplicationFactory` (PR #35)
- [x] Entidade `Order` (`Domain/Orders`), escrita pelo autor: `OrderStatus` com os 10 estados, `private set`, `InvalidOrderTransitionException(from, to)`, transições `ConfirmPayment`, `MarkAwaitingConfirmation`, `Expire`, `MarkFailed`; 12 testes unitários (PR #36)
- [x] Entidade `Cart` (`AddItem` soma, `ChangeQuantity` lança `CartItemNotFoundException`, `RemoveItem`), `CartItem` com construtor `internal`; 7 testes (PR #37)
- [x] Módulo renomeado `Cart` → `Carts`; tabelas `carts` (índice **único** em `CustomerId`) e `cart_items` (chave `CartId` + `ProductId`, FK para `products` com `Restrict`), migration `AddCarts`; 3 testes de persistência (PR #38)
- [x] `ICartService` + `CartView` (Application) e `CartService` (Infrastructure): adicionar e ver o carrinho com **preços do catálogo** (o carrinho não guarda preço); 3 testes (PR #39)
- [x] Página do carrinho (`[Authorize]`, cliente pelo claim `NameIdentifier`, nunca pelo formulário), botão "Adicionar ao carrinho" no catálogo (some quando esgotado), link no menu; testes de página com sabotagem (PR #40)
- [x] Mudar a quantidade e remover itens (serviço + página; remover é idempotente); testes com sabotagem (`SaveChangesAsync`, somar × trocar) (PR #41). **62 testes.**
- [x] Pedido no banco (PR #43): `OrderItem` com snapshot de nome e preço (8 testes); `Order` com itens (vazio e produto repetido são recusados), `ShippingAddress` (UF entre as 27, CEP só com 8 dígitos; 15 testes), frete por estado com `ShippingPolicy` (ADR 0024; 11 testes) guardado no pedido, `Total` = itens + frete; tabelas `orders` (endereço em colunas `Shipping*`, `CustomerId` com índice **não** único e sem FK para `AspNetUsers`) e `order_items` (FK para `products` com `Restrict`), migration `AddOrders`; 2 testes de persistência (o preço do pedido não muda quando o catálogo muda)
- [x] "Finalizar" (`CheckoutService`, PR #44), **escrito pelo autor** com lacunas guiadas: transação, carrinho vazio → falha, reserva por `ExecuteUpdateAsync` com `WHERE Id = @id AND Stock >= @qty` (0 linhas → falha e tudo é desfeito), itens em ordem de `ProductId` (evita deadlock), `TimeProvider` para a data, cria o pedido e apaga o carrinho; `PlaceOrderResult` (falha de negócio é resultado, não exceção). Testes: caminho feliz, "tudo ou nada", carrinho vazio
- [x] Teste de concorrência (`Task.WhenAll`): dois clientes disputando a última unidade, só um consegue e o estoque fica em 0. Sabotagem (sem o `Stock >= qty`) → estoque -1 e o teste falha
- [x] Página de checkout (`CheckoutViewModel` com os mesmos limites das colunas, UF por lista, CEP com ou sem traço; botão "Finalizar compra" no carrinho) e página do pedido criado, **só para o dono** (`IOrderQueries` filtra por `Id` **e** `CustomerId`; outro cliente recebe **404**). Sabotagens: sem `ModelState.IsValid` o CEP "123" vira 500; sem o filtro por cliente, um curioso vê o pedido alheio (PR #44). **116 testes.**

## Checklist do M1

- [x] Entidade `Product` (`Domain/Catalog`): `private set`, construtor valida nome, preço > 0 e estoque ≥ 0; 9 testes unitários (PR #14)
- [x] `AppDbContext` + `ProductConfiguration` (Npgsql, EF Core 10.0.12 alinhado), migration `InitialCatalog`, `dotnet-ef` como ferramenta local (`dotnet-tools.json`), migrations marcadas como código gerado no `.editorconfig`; teste de integração salva e lê um `Product` (PR #15)
- [x] Consulta do catálogo: `ICatalogQueries` + `ProductSummary` (DTO, na Application) e `CatalogQueries` (Infrastructure), com teste de integração independente do seed (PR #16)
- [x] Seed dos 6 kits (migration `SeedCatalog`, Ids fixos) + teste que garante o seed (PR #18)
- [x] PostgreSQL de desenvolvimento: `compose.override.yaml` publica `127.0.0.1:5432` só no PC; conexão em user-secrets (`ConnectionStrings:Default`) (PR #18)
- [x] `AddInfrastructure` (DI, conexão obrigatória) + `CatalogController` fino + página `/Catalog` (preço em pt-BR, "Esgotado" / "Últimas N unidades") (PR #18)
- [x] Produção: `ConnectionStrings__Default` no `compose.prod.yaml` (senha do `.env`); comando `migrate` (`dotnet KeycapStore.Web.dll migrate`); `deploy.sh` roda `APP_TAG=<novo> docker compose run --rm -T app migrate` **antes** de trocar a versão. Primeiro deploy aplicou `InitialCatalog` + `SeedCatalog` na VM (PR #18)
- [x] Identity parte 1: `AppDbContext` herda de `IdentityDbContext<IdentityUser>` (`base.OnModelCreating` primeiro), migration `AddIdentity` (7 tabelas `AspNet...`, aplicada em produção pelo deploy), `AddIdentityCore` + `AddRoles` + `AddEntityFrameworkStores` no `AddInfrastructure` (e-mail único), teste de integração que prova o hash da senha (visto falhando com sabotagem) (PR #22)
- [x] Nome do `Product` com no máximo 150 caracteres (`Product.MaxNameLength`, usada também no `ProductConfiguration`); testes de borda 150/151 escritos antes da regra (PR #26)
- [x] Cadastro (PR #27): porta `IAccountService` + `AccountResult` (Application), `AccountService` com `UserManager` (Infrastructure), `PortugueseIdentityErrorDescriber` (e-mail duplicado com mensagem única, sem repetir o e-mail), `AccountController` + `RegisterViewModel` + `Views/Account/Register.cshtml`, antiforgery global (`AutoValidateAntiforgeryTokenAttribute`). Testes: serviço (válido, duplicado, senha fraca) e página (POST com token → 302 `/Catalog`; sem token → 400), com sabotagens. Primeira conta criada em produção.
- [x] Chaves do Data Protection no PostgreSQL (`IDataProtectionKeyContext`, migration `AddDataProtectionKeys`, `SetApplicationName`); teste que simula um deploy e confere a chave no banco; aviso `may not be persisted` sumiu da produção e a chave sobreviveu a um deploy (PR #29, ADR 0022 no PR #30)
- [x] Login (PR #31): `FrameworkReference` do ASP.NET na Infrastructure, `SignInManager`, `SignInAsync`/`SignOutAsync` na porta, bloqueio após **3** tentativas (5 min), mensagem genérica "E-mail ou senha inválidos.", cookie `HttpOnly` + `Secure` + `SameSite=Lax` (8h, renovável), `UseAuthentication`; testes de cookie e senha errada
- [x] Logout por POST + menu "Olá, e-mail"/"Sair" (PR #32); testes do bloqueio (2 erros, 3º bloqueia, senha certa barrada) e do ciclo login → menu → logout (cliente de teste em `https` por causa do `Secure`), com sabotagens
- [x] Comando `create-admin --email` (PR #33, ADR 0023): papel `Admin`, senha de 20 caracteres com `RandomNumberGenerator` mostrada uma vez, conta existente promovida sem trocar a senha, idempotente; 3 testes. Sem seed de admin (no PC usa-se o mesmo comando). Admin real criado na VM com e-mail separado (`+admin`)

## Checklist do M0

**Spike de premissas (fazer primeiro, 1–2h):**
- [x] Criar a conta Stripe e ativar o Pix no Dashboard (sandbox "Área restrita de KeycapStore", conta BR, Pix habilitado)
- [x] Gerar um pagamento Pix de teste e anotar quais eventos de webhook chegam (Q1, caminho de sucesso; ver "Resultado do spike" abaixo)
- [x] Testar o caminho de **falha do Pix** e anotar os eventos
- [ ] (M4) Confirmar `checkout.session.expired` com Pix pendente, numa sessão de 30 minutos criada pelo código (Q1b)
- [x] Criar a VM na Oracle (Q3 respondida: **sem capacidade A1** em São Paulo em 30/09). Criada a `keycapstore` como **E2.1.Micro** (x86_64, 1 GB), provisória (ADR 0020). Acesso: `ssh keycapstore` (alias em `~/.ssh/config`).
- [x] Confirmar os planos gratuitos do que o M0 usa. O restante (R2/B2, monitor externo, healthchecks.io) **passou para o M6**, onde é usado (Q4). Já confirmados: gitleaks-action gratuito para conta pessoal; GitHub Actions e rulesets no repositório público; GHCR gratuito ("currently free"). Runners ARM não são mais necessários enquanto durar a Micro (x86_64).

**Esqueleto:**
- [x] Solution `KeycapStore.slnx` com os 4 projetos + 2 de testes, `global.json` (SDK 10), `Directory.Build.props`, `.editorconfig` (`insert_final_newline = true`)
- [x] Um teste unitário (arquitetura: `Domain` sem dependências proibidas, visto falhando com sabotagem) e dois de integração (`WebApplicationFactory` + PostgreSQL 17 via Testcontainers) passando
- [x] Workflow de CI com os 5 itens (`.github/workflows/ci.yml`, actions fixadas por hash) + ruleset "Proteger main" (PR obrigatório, 2 checks, branch atualizado, sem bypass). PR #1 juntado com CI verde.
- [x] Dockerfile multi-stage (usuário sem privilégios) + `compose.yaml` (PostgreSQL sem porta, app em `127.0.0.1`, senha via `.env`). PR #3. Com a Micro (x86_64), o build ARM64 não é necessário por enquanto.
- [x] Script de reconstrução da VM `deploy/bootstrap-vm.sh`, idempotente, 5 de 5 partes (PRs #4 e #6): swap 2 GB; fuso de Brasília + atualizações + `unattended-upgrades` (reinício às 04:00); Docker + rotação de logs; portas 80/443 no iptables, SSH só por chave e sem root, fail2ban; Nginx + HTTPS (Let's Encrypt, `certonly` com configuração escrita pelo script, renovação testada com `--dry-run`, `server_tokens off`). Primeira execução exige `LETSENCRYPT_EMAIL`.
- [x] Subdomínio DuckDNS (`keycapstore.duckdns.org` → VM) + HTTPS com redirecionamento de http para https
- [x] CD completo: `publish-image` → GHCR com a etiqueta do hash do commit (PR #7); `compose.prod.yaml` com `image:` (PR #8); `deploy.sh` com validação do hash, pull antes de trocar, verificação do site e rollback (PR #10); usuário `deploy` com `sudo` só para o `deploy.sh` e chave com comando forçado + `restrict` (PR #11, testado: `ls /` recusado, sem terminal); job `deploy` com Environment `production` (revisor obrigatório, sem bypass de admin, só `main`, chave e `known_hosts` no cofre) (PR #12).
- [x] Página publicada **via CD com aprovação**: `APP_TAG=47591507...` (merge do PR #12) na VM. Reinício manual da VM testado: a loja volta sozinha.

### Resultado do spike da Stripe (30/09/2026, Payment Link no sandbox)

| Pagamento | Eventos observados (ordem de chegada) | `checkout.session.completed` |
|---|---|---|
| Cartão `4242` | `charge.succeeded`, `payment_intent.succeeded`, `checkout.session.completed`, `payment_intent.created`, `charge.updated` | `payment_status = paid` |
| Pix (simulado) | `payment_intent.requires_action`, `payment_intent.created`, `payment_intent.requires_action`, `payment_intent.succeeded`, `charge.succeeded`, `checkout.session.completed`, `charge.updated` | `payment_status = paid` |

**Conclusões:**
- No Checkout com Pix, `checkout.session.completed` **só chegou depois do Pix pago**, já com `paid`. **Não** chegou `completed` com `unpaid`, nem `async_payment_succeeded`. O cliente fica na página do Checkout com o QR até pagar.
- O design continua válido, porque o handler decide pelo `payment_status`. O ramo `unpaid` → `AwaitingConfirmation` e os eventos `async_payment_*` ficam como **defesa**, e podem nunca ocorrer com Pix no Checkout. Reavaliar no M4.
- **Eventos chegam fora de ordem, na prática:** no cartão, `payment_intent.created` chegou **depois** de `payment_intent.succeeded`.
- **Pix que falha ou expira** (teste 10:06): chegaram só `payment_intent.requires_action`, `payment_intent.created`, `payment_intent.payment_failed` e `charge.failed`. O PaymentIntent volta para `requires_payment_method`, e **a sessão continua `open` / `unpaid`**. O cliente volta ao Checkout ("Seu cartão foi recusado", uma mensagem genérica da Stripe mesmo para Pix) e pode tentar de novo. **Nenhum evento `checkout.session.*` é emitido.**
  - Consequência: uma falha de Pix **não** libera o estoque, e isso é o correto, porque o cliente ainda pode pagar na mesma sessão. A liberação acontece em `checkout.session.expired` (30 minutos no nosso código). A reserva máxima fica limitada pela expiração da sessão.
  - `async_payment_failed` → `Failed` também fica como defesa.
- Pendente (Q1b, para o M4, com o código definindo `expires_at` de 30 minutos): confirmar que uma sessão expirada com Pix pendente (`fill_never@…`) emite `checkout.session.expired`.

> **Plano B do M0:** se passar de 2 semanas, trocar o CD completo por um deploy manual simples e mover o endurecimento para o M6.

## Próximos passos

1. **Melhorias pequenas vistas nos testes de 19/10:** lista **"Meus pedidos"** (só os do cliente logado, com teste do curioso) e "Últimas **1 unidades**" → "Última unidade".
2. **Pendências da v1:** merge do PR #70 do Dependabot; **teste mensal de restauração a partir da R2 até 15/11** (runbook `docs/runbooks/backup.md`).
3. **Escolher a ordem da segunda parte da v2** (sugestão de 19/10: cadastro de produtos pelo admin → endereços salvos → conciliação → inglês → Google → Payment Element → frete real → pré-venda) e registrar no `DEFINICOES.md`.

## Bloqueios

_Nenhum._

## Questões em aberto

- **Exclusão de conta, pendente para o go-live (ADR 0033):** prazo de guarda (Q5) e o **job de anonimização** de nome e endereço; **avisar a Stripe** da eliminação (LGPD art. 18, § 6º; conferir na documentação dela o que dá para apagar); os dados da conta ficam nos **backups** até 29 dias (informar na política de privacidade). Corrida conhecida: pedido criado em outra aba no instante da exclusão.
- **`.gitleaksignore`** (21/10): um falso positivo registrado (senha de teste `Senha-Forte-123` no commit `330c28f`, regra `generic-api-key`). Senhas de teste usam `PageClient.Password`.
- **GitHub Actions travado (21/10):** o job do gitleaks ficou "in progress" com a execução já "success", travando o merge; resolvido com "Re-run all jobs". Se repetir, fechar e reabrir o PR.
- **Arrependimento, pendente para o go-live (ADR 0032):** o Decreto 7.962 § 4º pede que a loja **envie** a confirmação (hoje ela só aparece na página; a loja não envia e-mail); quem paga o **frete de volta** e como o cliente devolve o pacote (a lei não diz no texto conferido; definir com orientação jurídica e informar nos termos de uso). Prazo contado como 7 × 24 h exatas a partir do `DeliveredAt`.
- Pedidos entregues antes de 19/10 não têm `DeliveredAt` e não mostram o botão de desistência (só o `04bd9d69`, de teste).
- **Dados pessoais reais em produção:** um pedido de 16/10 (Pix de 17/10) tem nome e endereço que parecem reais (teste de outra pessoa). Não publicar prints da página do admin sem cortar. A pessoa pode excluir a conta quando o pedido estiver finalizado; nome e endereço só somem com o job de anonimização.
- **Q1b respondida em 13/10** (ver checklist do M4). Ainda não confirmado na documentação: o que acontece com um Pix **já gerado** quando a sessão expira.
- **Reembolso depois de 24 h:** a Stripe guarda a chave de idempotência por 24 h. Se o `SaveChangesAsync` depois do reembolso falhar e o pedido ficar `PaidOutOfStock` por mais de um dia, a nova tentativa recebe um erro ("já reembolsado") e vira `LogError` a cada minuto. Risco baixo; tratar o código de erro da Stripe como sucesso se acontecer.
- As 409/404/400 do filtro respondem com a página em branco (o 403 aparece com a página padrão do navegador). Melhorar com páginas de erro amigáveis (v2 ou v3).
- **Teste instável (14/10):** `Webhook_SameEventInParallel_IsRecordedOnce_AndAllGet200` falhou 1 vez; em 15/10, cerca de 70 rodadas (inclusive 3 suítes ao mesmo tempo) sem reproduzir, e a leitura do código não mostrou corrida. O teste agora imprime o corpo das respostas que não forem 200 (PR #63): se falhar de novo, a causa estará na mensagem.
- **Clique duplo simultâneo no admin:** o token de concorrência garante que o pedido avança uma vez só, mas, se dois cliques chegarem exatamente juntos, o segundo recebe 500 (e não 409). Os dados ficam certos; o tratamento foi tirado porque nenhum teste conseguia prová-lo. Reavaliar se aparecer no log.
- **Eventos criados antes de existir o destino** nascem na versão padrão da conta (`dahlia`) e são recusados pelo webhook (ADR 0027), mesmo reenviados pelo CLI. Visto em 15/10 com o pedido `04bd9d69`.
- Q3: **respondida** (sem capacidade A1 em 30/09); seguir tentando para migrar da Micro
- Q4: **respondida em 15/10** (ADR 0030): R2, healthchecks.io e UptimeRobot.
- **R2 com cartão e sem teto de gasto:** a proteção é o uso mínimo, o ciclo de vida e o alerta de US$ 0,01. Se o alerta chegar, ver a descrição dele (o que conferir).
- **Teste de restauração mensal** (ADR 0018): o próximo, a partir da R2, até 15/11/2026; registrar no runbook.
- Q5: prazo legal de retenção dos pedidos (produção, não é código)
- Cookie do antiforgery sem `Secure` (o de login tem). Risco baixo; endurecer exige que os testes de página usem `https`.
- **Corrida no primeiro carrinho:** duas abas criando o primeiro carrinho do mesmo cliente ao mesmo tempo → o índice único recusa o segundo (correto), mas aquele clique vê uma página de erro. Melhoria: tentar de novo uma vez. Baixa prioridade.
- O login ignora o `ReturnUrl` (quem tenta abrir `/Cart` sem login cai no catálogo depois de entrar, e não no carrinho).
- O `migrate` no deploy mostra `libgssapi_krb5.so.2: cannot open shared object file` (aviso do driver do PostgreSQL; já aparecia antes; a migration roda normalmente). Investigar quando houver tempo (não bloqueia).
- O padrão da conta Stripe é `2026-08-26.dahlia`; o SDK é `endive`. No PC o `stripe listen` precisa de `--latest`; em produção o destino está fixado em `endive`. Ao atualizar o Stripe.net de família, atualizar o destino junto (avaliar se vale subir o padrão da conta).
- A Stripe recomenda eventos **thin** para integrações novas; usamos **snapshot** (o handler lê a sessão inteira do evento). Reavaliar se a troca de versão virar um problema.
- O `ssh` na VM avisa que existe o Ubuntu `26.04.1 LTS`. **Não** fazer `do-release-upgrade` sem plano (testar antes com a reconstrução da VM pelo `bootstrap-vm.sh`).
- **Melhoria futura (pedido do autor em 09/10):** quando a Stripe estiver fora, mostrar um aviso claro ("tente de novo em alguns minutos") e um jeito de refazer a compra a partir do pedido que falhou (hoje o carrinho já foi apagado e o cliente só vê "Pagamento não aprovado").
- Os auxiliares dos testes de página já existem em `Fixtures/PageClient` (15/10), mas `CartPageTests`, `CheckoutPageTests` e `OrderPageTests` ainda têm cópias próprias. Migrar quando mexer nessas classes.

**Correção de dados em produção (15/10):** os 3 pedidos `AwaitingPayment` de 12/10 tinham sido **pagos** (testes com o `4242` enquanto o webhook era construído). `ae4211ec` e `d13dead5` foram confirmados pelo caminho oficial (reenvio do evento no Dashboard; para o `d13dead5`, apagada antes só a anotação `evt_1UPlEFLUSDq1G5KIhLvMFb0q` em `processed_stripe_events`). O `04bd9d69` foi marcado `Paid` **à mão** (`UPDATE ... WHERE "Status" = 'AwaitingPayment'`), com o autor de acordo, porque o evento `evt_1UPkCnLUSDq1G5KINtgNJ4Fx` (`paid`, R$ 639,80, `pi_3UPkCmLUSDq1G5KI0vmfOZBW`) era anterior ao destino e só existia em `dahlia`. O estoque não mudou (as unidades já estavam reservadas para pedidos pagos).

**Decisões de 06/10:** bloqueio após **3** tentativas (não 5), ciente de que facilita bloquear a conta de outra pessoa (5 min); chaves no PostgreSQL (ADR 0022); admin pelo comando também no PC e conta existente promovida (ADR 0023).

**Decisões de 05/10 (2ª sessão):** os branches **não** são apagados no GitHub (só localmente, depois do merge), para guardar o histórico; o nome do `Product` limitado a 150 caracteres **é regra de negócio**; atualizações automáticas diárias (ADR 0021).

## Diário de sessões

### 21/10/2026: exclusão de conta e **parte legal da v2 concluída** (1h)
- Aquecimento: por que não apagar os pedidos com a conta (acertou a ideia: "precisam ser rastreados"; corrigido: pelo prazo da obrigação legal, não para sempre). O Planalto recusou a conexão do Claude de novo e o autor colou a LGPD.
- Decisões do autor (todas as recomendadas): bloqueio por qualquer pedido não finalizado, inclusive entregue dentro dos 7 dias; nome e endereço só depois do prazo legal; confirmação pela senha; admin não se exclui. ADR 0033; `DEFINICOES.md` §6 corrigido (dizia que o `CustomerId` já aceitava vazio desde a v1).
- Código em 3 passos (PR #76): domínio (autor; o `throw` faltou no `DetachCustomer`, apontado antes de rodar os testes), serviço (autor, 4/4 lacunas de primeira; respondeu na prática por que conferir todos os pedidos antes de mexer em algum), página (Claude; sabotagem sem o "sair da conta" → teste vermelho). **279 testes.** Teste em produção completo, com o pedido sem dono conferido no `psql`.
- Tropeços: senha de teste com hífens escrita pelo Claude disparou o gitleaks (`generic-api-key`; corrigido com `PageClient.Password` + `.gitleaksignore`); o job do gitleaks ficou travado "em andamento" no GitHub (resolvido com "Re-run all jobs").
- Verificações: `Cancelled` conta como finalizado (sem resposta), conferir tudo antes de tirar o dono (respondida pelo código), pedido sem dono invisível para clientes (explicada com o resultado do `psql`). O autor diz que nada ficou confuso.

### 19/10/2026: v2 começou: cancelamento e direito de arrependimento em produção (2,5h)
- Pasta de memórias apagada do repositório (era só para sincronizar PC e notebook). O autor dispensou a revisão e o README e foi direto para a v2.
- **Cancelamento** (PR #73, ADR 0031): o autor escolheu "Pago ou Em separação" e "guardar o `pi_` + buscar pela sessão" para os pedidos antigos. Escreveu `Order` (lacunas certas), `RefundProcessor` e `CancellationService`. Em produção, o pedido `6ea74fa5` foi cancelado e reembolsado pela busca da sessão (o Claude tinha dito que esse caminho não seria testado em produção, e foi).
- **Arrependimento** (PR #74, ADR 0032): o Planalto recusou a conexão do Claude e **o autor colou os textos da lei**; decisões do autor: reembolso na hora, admin decide o estoque, vale também a caminho. Escreveu 5/5 lacunas do `Order` de primeira, `RefundProcessor`, `WithdrawalService` (faltou o `cancellationToken` no `SaveChangesAsync`; commit de correção) e o `ConfirmReturnAsync` **sem esqueleto**, só pela explicação. Refatoração `CanWithdraw` (De Morgan) para a página e o domínio usarem a mesma regra. **256 testes.** Teste em produção completo (compra → entrega → desistência → reembolso → devolução com estoque).
- Tropeços: `if(` sem espaço (o `dotnet format` corrige); commit feito antes da correção (resolvido com outro commit); explicação resumida demais do passo 4 ("Que?"; refeita com tabela comparando com o `CancellationService`); o CI da `main` demorou a aparecer depois do merge do #74 (apareceu sozinho).
- Achados no teste: falta "Meus pedidos" no menu; "Últimas 1 unidades".
- Verificações: por que o reembolso fica no job (sem resposta), `is not null` antes do `.Value`, 302 × 409 no esqueleto e estoque com `ConfirmReturn` recusado (sem resposta). O autor diz que nada ficou confuso.

### 15/10/2026 (2ª sessão): M6 inteiro e **v1 concluída** (2h)
- Aquecimento: por que o backup não fica na VM (acertou: "se perder a VM, os backups vão junto"; completado com "fora da Oracle" e "backup não restaurado é esperança").
- Tabela do que faltava no M6 e na v1. Q4 pesquisada na documentação; o autor escolheu a **R2** (o Claude recomendou a B2, sem cartão); sem chave só de escrita na R2 → bucket lock + `age` (ADR 0030).
- `/health` (PR #67), backup com `age` e **restauração no PC com os números iguais aos de produção** (PR #68), envio para a R2 + timer + runbook + Serilog + Dependabot (PR #69), correção do `rclone` (PR #71: o 501 vinha do `HEAD ?versionId`; a primeira suspeita do Claude, a trava e depois a data de modificação, estava errada, e o `--dump headers` mostrou o pedido certo). Contas criadas: Cloudflare (alerta de US$ 0,01, bucket, regras, token só do bucket), healthchecks.io, UptimeRobot. **Compra com Pix pela loja → Pago.** **221 testes.**
- Tropeços: `cmd` no lugar do PowerShell (`$env:` e aspas simples); `\"` no fim de um caminho no PowerShell 5.1 (erro do Claude); `*` expandido pelo usuário sem permissão (`sudo sh -c`); chave do `age` gerada fora da pasta escolhida (movida); a primeira URL do healthchecks apareceu no chat (check recriado).
- Decisões do autor: R2 em vez de B2; chave privada do `age` só na pasta do OneDrive (sem gerenciador de senhas).
- Verificações: disco a 90% e monitor (sem resposta; explicado). O autor diz que nada ficou confuso.

### 15/10/2026: pedidos presos resolvidos, teste instável investigado e **M5 concluído** (3h)
- Aquecimento: visitante × cliente em `/admin/orders` (acertou que nenhum entra; explicado 401/login × 403). Revisão: reembolso fora do webhook (acertou o centro: "não depender da Stripe").
- Pedidos presos em produção: os 3 tinham sido pagos; 2 confirmados reenviando eventos (Dashboard e CLI), 1 corrigido à mão com a prova (ver "Correção de dados"). Correção do Claude: o "Kit Maré (modificadores) Esgotado" vem do seed (estoque 0), e não de pedidos presos.
- Teste instável: ~70 rodadas sem reproduzir; o teste passou a mostrar o erro quando falhar (PR #63). O Claude tinha descumprido a regra do `DEFINICOES.md` em 14/10 (rodou de novo e só anotou).
- M5 (PR #64): ADR 0028 (página mínima), transições de envio (autor), `/admin/orders` com 403, `ShippingService` (autor; um `;` antes do `??` corrigido). Comando `reset-admin-password` (PR #65, ADR 0029), pedido pelo autor ao não lembrar a senha do admin. **216 testes.** Em produção, um pedido foi de Pago a Entregue pela página.
- Tropeços: `UPDATE` digitado no bash e não no `psql` ("command not found"); senha nova mostrada borrada num print (borrão é reversível; o autor gerou outra, sem print).
- Verificações: conferir antes de guardar (acertou a ideia, mas disse "transação"; corrigido: é a ordem das linhas, a memória não tem rollback); teste instável (não tinha entendido; explicado com a balança). O autor diz que ficou sem dúvidas.

### 14/10/2026: reembolso automático, filtro 409/404/400 e **M4 concluído** (1,5h)
- `/encerrar` de 13/10 feito no início (4h). Aquecimento: por que o reembolso fica fora do webhook (não sabia; explicado com o porteiro e o tesoureiro).
- Imprevisto: alerta de segurança do .NET (CVE-2026-96960) quebrou o build; .NET 10.0.13 (PR #59). O "Substituir em todos os arquivos" do Visual Studio trocou também 11 arquivos de migration (`ProductVersion`); pego pelo `git status` e desfeito com `git restore` só na pasta.
- Reembolso automático (PR #60): autor escreveu o `OutOfStockRefunder` (faltou o `SaveChangesAsync`, os testes pegaram). Reembolso **real** no sandbox com o banco do PC. Filtro de exceções (PR #61), 404 conferido em produção. **196 testes.**
- Tropeços: `docker compose` rodado fora da pasta do projeto ("no configuration file provided"); código errado colado no chat (o `CartService` em vez do `OutOfStockRefunder`; o print mostrava o certo).
- Achado: um teste antigo (`Webhook_SameEventInParallel...`) falhou uma vez e passou nas 36 seguintes; anotado.
- Verificações: reembolso fora do webhook (não sabia), servidor caindo entre reembolso e gravação (sem resposta). O autor diz que ficou sem dúvidas; as duas vão para a revisão.

### 13/10/2026: concorrência, expiração, Pix, pedidos órfãos e pagamento atrasado (4h)
- Aquecimento: webhook sem antiforgery nem login (parcial: "é um robô"; explicado que quem barra o impostor é a assinatura).
- PR #55 (escrito pelo Claude a pedido do autor, com o ADR 0027): token de concorrência, `ConfirmPayment` idempotente, `expired` devolvendo o estoque; teste com 10 eventos diferentes em paralelo. Expirada uma sessão em produção pelo CLI.
- PR #56 (autor, 6 lacunas certas): eventos assíncronos do Pix. PR #57 (autor, 6 lacunas; `>` no lugar de `<` e `Clear(cancellationToken)` corrigidos): devolução de estoque num lugar só + job do D35, que liberou em produção o pedido preso desde 09/10. PR #58 (autor, lacunas certas): pagamento atrasado com savepoint. **185 testes.**
- **Q1b fechada:** Pix nunca pago → `expired` 31 min depois, pedido `Expired`.
- Conferido na documentação da Stripe: sessão expirada não pode ser concluída; Adaptive Pricing mantém `amount_total` e `currency` na moeda da integração.
- Sabotagens: sem a transação no D35 (estoque 13 em vez de 5), sem o savepoint (reserva pela metade), sem o token (3 → 23).
- Tropeços: `git add` + `push` sem `commit` ("Total 0"); um aviso de build passageiro (não era código); mensagem de log dizendo "pedido reembolsado" antes do reembolso (corrigida para "reembolso necessário").
- Verificações: caderno × token (não sabia), `AwaitingConfirmation` × expiração (parcial), receita num lugar só e `<` × `>` (não sabia), conferir antes de mudar e savepoint (sem resposta). Todas vão para a revisão. O autor não relatou nada confuso.

### 12/10/2026: webhook assinado e idempotente, pagamento confirmado e **M3 concluído** (2h)
- Revisão de 09/10: idempotência (quase: "não criar duas sessões"; completado com a nova tentativa do servidor), devolução sem `>=` (acertou; perigo = devolver duas vezes), página do pedido com a Stripe fora (errou: "criar nova sessão"; explicado que o carrinho já foi apagado).
- ADR 0026 (rascunho do Claude): validade da sessão = criação do pedido + 31 min. PR #49 fechado com teste no sandbox (PC e produção).
- Webhook em 4 PRs (#50 a #53): assinatura, `ProcessedStripeEvents` (handler do autor, 6 lacunas certas), confirmação do pagamento (autor; 5 de 6 lacunas certas de primeira), página de retorno com consulta da situação. **156 testes.** Em produção: "Reenviar" no Dashboard gravou o evento uma vez só, e a página de retorno virou "Pago" sozinha.
- Achado: a conta Stripe está na família `dahlia` e o SDK na `endive`; o `ConstructEvent` recusava os eventos (400). Resolvido com `stripe listen --latest` no PC e o destino de produção fixado em `endive`.
- Sabotagens: sem `[IgnoreAntiforgeryToken]` (o evento verdadeiro recebe 400), sem o `catch` da duplicata (500), sem a conferência do valor.
- Tropeços: `db.AddAsync(cancellationToken)` no lugar de `SaveChangesAsync` (compilou, porque o `AddAsync` aceita qualquer objeto, e deu 500); ajudante de teste colado dentro de outro método (instrução ambígua do Claude; resolvido com o arquivo inteiro); rotina pós-merge rodada antes do commit (o branch não tinha commits, nada se perdeu); comando do `psql` com `-f compose.prod.yaml` (na VM o arquivo se chama `compose.yaml`; erro do Claude); começo do `whsec` do CLI num print borrado (risco baixo, só serve para o `localhost`).
- Verificações: metadados no pagamento × na sessão (o Claude mandou olhar no lugar errado), `success_url` não prova pagamento (não sabia), antiforgery × assinatura (não sabia), versão `dahlia` no destino (não sabia), inserir primeiro × consultar (acertou), 200 × 500 no valor divergente (não sabia), 404 do curioso no status (não sabia; achou que voltava ao catálogo). O autor diz que nada ficou confuso, mas as 4 de hoje vão para a revisão.

### 09/10/2026: M3 parte 1 e 2, configuração da Stripe, sessão do Checkout e "Finalizar" levando à Stripe (3h)
- Revisão de 08/10: `CommitAsync` (errou, depois acertou com a analogia do lápis e da caneta), 404 × 403 (acertou). Pedido de teste em produção confirmou o deploy do PR #44.
- `StripeOptions` com `ValidateOnStart` e chave no `.env` da VM (PR #47); `StripeSessionId` + `AttachStripeSession` escrito pelo autor (PR #48; a primeira versão tinha as regras 2 e 3 trocadas, corrigido depois de rastrear o código).
- Branch `feat/stripe-gateway` (3 commits, sem push): `IPaymentGateway` + `StripePaymentGateway` (`Stripe.net` 53.0.0), `StartPaymentAsync` com **compensação** escrito pelo autor (6 lacunas, todas certas), "Finalizar" redireciona para a Stripe. **139 testes.**
- Conferido na documentação da Stripe: `ui_mode` padrão `hosted_page`, `expires_at` entre 30 min e 24 h, Pix de 10 s a 14 dias, chaves de idempotência (mesmos parâmetros, guardadas 24 h).
- Sabotagens: sem `ValidateOnStart` (2 falhas), sem devolver o estoque, sem o `if` da falha da Stripe (500).
- Tropeços: pasta `Paymens`; sabotagem deixada no lugar uma vez; CI do PR #47 falhou por ordem de `using` (resolvido com `dotnet format`); rotina pós-merge rodada antes do push (o `-d` recusou, corretamente).
- Verificações: fail-fast (acertou), `.env` na VM (acertou), sessão repetida (parcial: pensou em "duas abas"; o caso real é a nova tentativa do servidor), compensação (acertou), idempotência, devolução sem `>=` e página do pedido com a Stripe fora (as 3 últimas em "não sei", explicadas pelo Claude). O autor diz que nada ficou confuso, mas as 3 vão para a revisão.

### 08/10/2026: pedido no banco, "Finalizar" com reserva atômica e **M2 concluído** (3h)
- Revisão de 07/10: carrinho não reserva (acertou: "só garante na hora da compra"); escopos separados nos testes (não sabia; explicado e confirmado na pergunta seguinte).
- Decisão do autor: frete grátis no Centro-Oeste, SP e RJ; R$ 15 nos demais (ADR 0024, substitui o frete fixo do ADR 0002).
- Pedido no banco (PR #43): `OrderItem`, `ShippingPolicy`, `ShippingAddress` com TDD; mapeamento com `OwnsOne`/`OwnsMany`, migration `AddOrders`. Primeira versão do `OrderItem` validava as propriedades em vez dos parâmetros (corrigido).
- "Finalizar" escrito pelo autor, passo a passo e com lacunas (PR #44); páginas de checkout e do pedido. **116 testes.** Testado no navegador de ponta a ponta (CE → frete R$ 15,00).
- Sabotagem: comentar o `.Where` inteiro faria o `UPDATE` valer para **todos** os produtos; os testes de concorrência e de "tudo ou nada" pegaram.
- Tropeços: o esqueleto do `CheckoutService` não compilava (parâmetro `clock` sem uso + `TreatWarningsAsErrors`; erro do Claude, que só testou a versão completa); o `GetAsync` inteiro foi colado **dentro** do método (instrução ambígua "olhe o `GetAsync`"); arquivos do item 5 no lugar errado (testes colados sobre o `CheckoutPageTests`, view em `Views/Checkout`, controller faltando) e o `git add` recusou tudo; resolvido com `git restore` e recriando os arquivos.
- Verificações: atomicidade do "ler e depois gravar" (acertou), `IsUnique` no `CustomerId` do pedido (não sabia), `db.Carts` × `db.Orders` (não sabia; depois acertou "só a cesta existe"), `reserved == 0` (acertou com ajuste), `CommitAsync` (não sabia), `ModelState` × navegador (acertou), 404 × 403 (não sabia), quando o estoque cai (acertou: só no "Finalizar"). O autor diz que nada ficou confuso, mas pediu para ir mais devagar no esqueleto ("tô entendendo nada"): explicar linha a linha **antes** de pedir código.

### 07/10/2026: `Order`, carrinho completo do domínio à página (3h)
- Aquecimento: fábrica de testes num lugar só (PR #35). `Order` escrito pelo autor, com 12 testes (PR #36); o arquivo de teste estava no projeto de integração e foi movido para o de unidade.
- Carrinho em 6 PRs (#37 a #41): entidade (o `RemoveItem` saiu primeiro com a lógica invertida; o teste pegou), banco com índice único por cliente, serviço com preços do catálogo, página com `[Authorize]`, mudar a quantidade e remover. **62 testes.** Módulo renomeado para `Carts` por conflito de nome com a classe.
- Sabotagens: sem `[Authorize]` a página abriu com 200 e "carrinho vazio" (o `!` escondeu o `null`); sem `SaveChangesAsync` o item continuou no banco.
- Tropeços: `CartService` criado primeiro na Application (movido para a Infrastructure); uma linha de teste colada dentro do teste errado (instrução ambígua do Claude; resolvido trocando o arquivo inteiro).
- Verificações: carrinho sem estoque (parcial: "ainda não verificamos"; explicado que é de propósito e que a proteção é a reserva no "Finalizar"); `SaveChangesAsync` (não sabia); somar × trocar (não sabia). O autor diz que nada ficou confuso, mas as duas últimas respostas foram "não sei" no fim de 3h: revisar no início da próxima sessão.

## Histórico

_(sessões antigas resumidas em uma linha cada)_

- **06/10/2026 (2h):** chaves do Data Protection no PostgreSQL (PR #29, ADR 0022), login com bloqueio em 3 tentativas (PR #31), logout + menu (PR #32), `create-admin` (PR #33, ADR 0023); **M1 concluído**.
- **05/10/2026 (3ª sessão, 2h):** regra dos 150 caracteres no `Product` com TDD (PR #26); cadastro de clientes em 5 passos (PR #27); a garantia de e-mail único é o índice do `UserName`.
- **05/10/2026 (2ª sessão, 2h):** runner do CI fixado, Identity parte 1 (PR #22), `deploy.sh` que se atualiza (PR #23), atualizações automáticas diárias às 03:30 (PR #24, ADR 0021).
- **05/10/2026 (1h):** seed dos 6 kits, PostgreSQL de desenvolvimento, `AddInfrastructure`, página do catálogo e comando `migrate` no deploy (PR #18); catálogo no ar.
- **02/10/2026 (1,5h):** revisão do deploy; `Product` com invariantes (PR #14), `AppDbContext` + `InitialCatalog` (PR #15), consulta do catálogo; ficou confuso o que é um DTO (revisado em 05/10).
- **01/10/2026 (2ª sessão, 1,5h):** `deploy.sh` com rollback (PR #10), usuário `deploy` com comando forçado (PR #11), job `deploy` com aprovação obrigatória (PR #12); reinício da VM testado; **M0 concluído**.
- **01/10/2026 (1,5h):** revisão do bootstrap, DuckDNS, Nginx + HTTPS (PR #6), imagem no GHCR (PR #7), compose de produção (PR #8) e loja no ar; início da pergunta de verificação a cada passo.
- **30/09/2026 (tarde, 2h):** Dockerfile + compose (PR #3), VM E2.1.Micro provisória (ADR 0020), bootstrap partes 1–4 (PR #4); CRLF quebra bash (`.editorconfig` com LF); o script de bootstrap ficou confuso e foi revisado depois.
- **30/09/2026 (manhã, 2h):** ambiente (WSL 2, Docker, Stripe CLI), spike da Stripe, solution com 4 projetos, teste de arquitetura, smoke tests com Testcontainers, CI + ruleset na `main` (sempre branch + PR).
- **29/09/2026:** definição do projeto (entrevista de arquitetura, `DEFINICOES.md`, `CLAUDE.md`, regras, comandos, ADRs). Nenhum código.
