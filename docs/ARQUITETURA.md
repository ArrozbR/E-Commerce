# Arquitetura: KeycapStore

Visão técnica do sistema. O **porquê** de cada escolha está nos ADRs (`docs/decisoes/`). O escopo e o fluxo completo estão em `docs/DEFINICOES.md`.

---

## 1. Visão geral

```
                 Internet
                    │ 443 (HTTPS)
          ┌─────────▼─────────┐        ┌───────────────────┐
          │ Nginx + Certbot   │        │      Stripe       │
          │ (host da VM)      │◄───────┤ webhooks assinados│
          └─────────┬─────────┘        └─────────▲─────────┘
                    │ 127.0.0.1                  │ API (sessões, reembolsos)
   ┌────────────────▼────────────────────────────┴──────┐
   │ Docker Compose                                      │
   │  ┌──────────────────────────┐   ┌────────────────┐  │
   │  │ app: KeycapStore.Web     │──►│ db: PostgreSQL │  │
   │  │ (ASP.NET Core, ARM64)    │   │ (sem porta     │  │
   │  └──────────────────────────┘   │  publicada)    │  │
   │                                 └────────────────┘  │
   └─────────────────────────────────────────────────────┘
          VM Oracle Cloud Always Free, Ampere A1 (descartável)
```

- **Monolito**: um único processo web, um banco.
- **Stripe Checkout hospedado**: o cliente paga na página da Stripe, e o resultado chega por **webhook**.
- **A VM é descartável**: um script em `deploy/` a reconstrói do zero, e o banco volta do backup (que fica fora do provedor).

---

## 2. Solution

```
KeycapStore.sln
├── src/
│   ├── KeycapStore.Domain/           regras de negócio puras (sem NuGet)
│   ├── KeycapStore.Application/      casos de uso + interfaces (portas)
│   ├── KeycapStore.Infrastructure/   EF Core, Identity, Stripe, jobs, comandos
│   └── KeycapStore.Web/              MVC + Razor, webhook, admin, Program.cs
├── tests/
│   ├── KeycapStore.UnitTests/        Domain, Application, testes de arquitetura
│   └── KeycapStore.IntegrationTests/ WebApplicationFactory + Testcontainers
├── deploy/                           compose, deploy.sh, reconstrução da VM, backups
├── .github/workflows/                CI e CD
├── Directory.Build.props             .NET 10, Nullable, TreatWarningsAsErrors
└── .editorconfig
```

### Regra de dependência

```
            ┌────────────┐
            │    Web     │
            └──┬──────┬──┘
               ▼      ▼
   ┌──────────────┐  ┌────────────────┐
   │ Application  │◄─┤ Infrastructure │
   └──────┬───────┘  └───────┬────────┘
          ▼                  ▼
        ┌──────────────────────┐
        │        Domain        │
        └──────────────────────┘
```

As setas apontam para dentro. **Por quê:** as regras de negócio (o centro) não podem mudar quando a tecnologia (a borda) muda, e precisam ser testáveis sem banco, sem rede e sem Stripe. A `Application` define **o que precisa** (`IPaymentGateway`), e a `Infrastructure` **fornece** (`StripePaymentGateway`). Isso é a inversão de dependência.

### Módulos (pastas dentro de cada camada)

| Módulo | Responsabilidade |
|---|---|
| `Catalog` | Produtos, estoque disponível, listagem pública |
| `Cart` | Carrinho persistido por cliente |
| `Orders` | `Order`, máquina de estados, reserva de estoque, envio |
| `Payments` | Sessão do Checkout, webhook, idempotência, reembolso |
| `Identity` | Conta, login, papéis (o Identity em si vive na `Infrastructure`) |

---

## 3. Modelo de domínio (núcleo)

```
Order
├── Id
├── CustomerId?            (anulável: o pedido sobrevive à exclusão da conta, na v2)
├── Status : OrderStatus   (private set; muda só por métodos)
├── Items[]                (snapshot: ProductId, nome, preço unitário, quantidade)
├── ShippingFee            (fixo)
├── ShippingAddress        (value object; snapshot com o nome do destinatário)
├── StripeSessionId?
├── TrackingCode?
└── ConcurrencyToken

Product
├── Id, Name, Description, Price
└── Stock                  (disponível para venda; reservar = decrementar)

ProcessedStripeEvent
└── EventId (UNIQUE), ProcessedAt
```

Máquina de estados: `docs/DEFINICOES.md` §8. Tabela de transições: `.claude/rules/pagamentos.md` §4.

---

## 4. Fluxos principais

### Checkout

```
Cliente ──POST /checkout──► Web ──► Application.PlaceOrder
                                     │ transação:
                                     │   cria Order (AwaitingPayment, snapshot)
                                     │   UPDATE stock ... WHERE stock >= qty (por item)
                                     │ commit
                                     ├──► IPaymentGateway.CreateSessionAsync
                                     │      (brl, metadata.order_id, expires 30min,
                                     │       pix 1800s, idempotency session-{id})
                                     │ grava StripeSessionId
Cliente ◄──302 checkout.stripe.com──┘
```

### Webhook

```
Stripe ──POST /webhooks/stripe──► Web (corpo bruto)
                                   └► Infrastructure: verifica assinatura ──✗──► 400
                                   └► Application.HandleStripeEvent
                                        transação:
                                          INSERT ProcessedStripeEvents(event.id) ──dup──► 200
                                          carrega Order por metadata.order_id
                                          confere amount/currency
                                          order.ConfirmPayment() / Expire() / ...
                                          (libera estoque em Expire/Failed)
                                        commit ──► 200      exceção ──► rollback ──► 500
```

### Retorno do cliente

`/orders/{id}/return` → consulta `GET /orders/{id}/status` (só o dono do pedido) até o status mudar. **Nunca altera estado.**

---

## 5. Transações e concorrência

| Garantia | Mecanismo |
|---|---|
| Evento processado uma única vez | `ProcessedStripeEvents.EventId` UNIQUE, inserido na mesma transação da transição |
| Transição aplicada uma única vez | token de concorrência no `Order` (concorrência otimista) + validação de origem na entidade |
| Estoque nunca negativo | `UPDATE ... WHERE stock >= qty` atômico; 0 linhas afetadas = esgotado |
| Nenhuma chamada duplicada à Stripe | chaves de idempotência `session-{orderId}` e `refund-{orderId}` |

**Nada de lock em memória.** Garantias entre processos só existem no banco.

---

## 6. Segurança

- Identity com cookies (`HttpOnly`, `Secure`, `SameSite`) + antiforgery. Papel `Admin` só pelo comando `create-admin`.
- Segredos: `dotnet user-secrets` (desenvolvimento) e `.env` fora do repositório (produção), com `ValidateOnStart`.
- Nenhum dado de cartão no sistema. Logs só com IDs.
- Rede: só as portas 22, 80 e 443 abertas; PostgreSQL sem porta publicada; aplicação só em `127.0.0.1`.

---

## 7. Entrega e operação

```
PR ──► CI (build, unit, integration, gitleaks, format) ──► merge na main
   ──► build da imagem ARM64 ──► GHCR (tag = hash do commit)
   ──► Environment "production" (aprovação manual + checklist ponta a ponta)
   ──► SSH (usuário deploy, comando forçado) ──► deploy.sh <tag>
          pull pelo digest ──► migrations (passo explícito) ──► restart
```

- **Observabilidade:** Serilog em JSON (com rotação no Docker) · `/health` (banco + disco) · monitor externo · alertas de webhook da Stripe.
- **Backups:** `pg_dump` diário → `age` → armazenamento de outro provedor (credencial só de escrita) · dead man's switch · restauração testada todo mês.
- **VM descartável:** script de reconstrução em `deploy/`, testado pelo menos uma vez.
