# Regras de arquitetura

Visão completa em `docs/ARQUITETURA.md`. Decisão em `docs/decisoes/0009-clean-architecture-quatro-projetos.md`.

## Projetos e dependências

```
src/KeycapStore.Web            → Application, Infrastructure (só para composição no Program.cs)
src/KeycapStore.Infrastructure → Application, Domain
src/KeycapStore.Application    → Domain
src/KeycapStore.Domain         → (nada)
```

- As dependências apontam **sempre para dentro**. Nunca adicionar uma referência que inverta essa direção.
- **`Domain` não tem nenhum pacote NuGet** e não conhece EF Core, ASP.NET, Stripe, Identity nem `DateTime.Now` (o tempo chega por parâmetro ou por uma abstração de relógio da `Application`).
- **`Application`** contém os casos de uso e **define interfaces** para tudo que é externo (`IPaymentGateway`, `IClock`, persistência). Não referencia EF Core, Stripe nem ASP.NET.
- **`Infrastructure`** implementa essas interfaces: `DbContext` (PostgreSQL via Npgsql), Identity, SDK da Stripe, verificação de assinatura do webhook, jobs.
- **`Web`** só traduz HTTP ↔ casos de uso. **Controllers finos:** nenhuma regra de negócio, nenhum `DbContext` injetado em controller, nenhum `if` sobre estado de pedido. `Program.cs` é o único lugar que conhece todas as camadas.

## Módulos

Os módulos são **pastas** dentro de cada camada (não projetos): `Catalog`, `Cart`, `Orders`, `Payments`, `Identity`.

- Um módulo não acessa tipos internos de outro módulo. A comunicação passa pela `Application`.
- As fronteiras entre camadas e módulos são verificadas por **testes de arquitetura** em `tests/KeycapStore.UnitTests/Architecture`.

## Onde cada coisa mora

| Peça | Projeto |
|---|---|
| `Order`, `OrderStatus`, `Product`, `ShippingAddress`, exceções de domínio | Domain |
| Casos de uso (`PlaceOrder`, `ConfirmPayment`, `ShipOrder`...), interfaces de portas | Application |
| `AppDbContext`, configurações EF, migrations, repositórios, Identity, `StripePaymentGateway`, verificação de assinatura, job de pedidos órfãos, comando `create-admin` | Infrastructure |
| Controllers MVC, views Razor, endpoint do webhook, endpoints de admin, `Program.cs` | Web |

## O que não fazer

- Não criar novos projetos, camadas ou pastas de módulo sem um ADR.
- Não introduzir mediator, CQRS, event sourcing, mensageria, Redis, microsserviços ou cache distribuído. **Não resolvem nenhum problema da v1**, e custam atenção e dinheiro (ver `docs/DEFINICOES.md` §3 e §6).
- Não criar interface "só por criar". Interfaces existem na fronteira da `Application` com o mundo externo.
- Não adicionar dependência (NuGet, serviço externo, recurso de nuvem) sem checar a regra de custo zero e registrar um ADR se for estrutural.
- Não implementar nada da lista "Fora de escopo" (`docs/DEFINICOES.md` §3) sem uma decisão explícita do autor.

## Infraestrutura

- A aplicação e o PostgreSQL rodam em **Docker Compose**. O PostgreSQL **não publica nenhuma porta**. A aplicação publica só em `127.0.0.1`.
- As imagens precisam funcionar em **ARM64** (VM Ampere A1).
- As migrations são aplicadas por um **passo explícito do deploy**. **Nunca** usar `Database.Migrate()` na inicialização da aplicação.
- Os arquivos de deploy e operação ficam em `deploy/` (Compose, `deploy.sh`, script de reconstrução da VM, backups).
