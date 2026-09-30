---
paths:
  - "**/*.cs"
  - "**/*.cshtml"
  - "**/*.csproj"
---

# Regras de código

## Idioma

- **Identificadores em inglês** (classes, métodos, variáveis, rotas, tabelas).
- **Textos da interface em português (pt-BR).** Documentação e ADRs também em português.
- Use o glossário de `docs/DEFINICOES.md` §8 ("Nomes no código"). **Não invente sinônimos**: é `Order`, nunca `Purchase` num lugar e `Order` em outro.

## C# e .NET

- **.NET 10**, `Nullable` habilitado, `ImplicitUsings` habilitado, **`TreatWarningsAsErrors`** (configurado num `Directory.Build.props` na raiz).
- Namespaces com escopo de arquivo (`namespace KeycapStore.Domain.Orders;`).
- Formatação pelo `.editorconfig` e pelo `dotnet format`. O CI rejeita código fora do padrão.
- Métodos assíncronos terminam em `Async` e recebem `CancellationToken` quando fazem I/O.

## Domínio

- Entidades **sem setters públicos**. O estado muda por métodos com intenção (`order.MarkShipped(trackingCode)`), que validam invariantes.
- Construtores e fábricas garantem que uma entidade nunca nasce inválida.
- Value objects (`ShippingAddress`, `Money`) como `record` imutável, com validação no construtor.
- **Nada de data annotations de banco ou de tela nas entidades** (`[MaxLength]`, `[Display]`). Mapeamento de banco fica na configuração do EF Core (`IEntityTypeConfiguration<T>`), e rótulos de tela ficam nos ViewModels.
- Dinheiro é `decimal` (ou `Money`), **nunca `double`**. Para a Stripe, converter para centavos (`long`) num único lugar.
- Erros de regra de negócio usam exceções de domínio específicas (`InvalidOrderTransitionException`, `OutOfStockException`), nunca `Exception` genérica.

## Camada Web

- Controllers recebem ViewModels e DTOs, **nunca entidades de domínio**, e nunca devolvem entidades para a view.
- Antiforgery em todo formulário. Autorização por `[Authorize]` / `[Authorize(Roles = "Admin")]`. A checagem de "dono do pedido" é feita na `Application`.
- Exceções de domínio são traduzidas em um único lugar (filtro ou middleware): transição inválida → `409`, não encontrado → `404`.

## Configuração e segredos

- Configuração tipada com o padrão Options (`IOptions<T>`) + **`ValidateOnStart`** para tudo que é obrigatório.
- **Nenhum segredo** em código ou em `appsettings*.json`: em desenvolvimento, `dotnet user-secrets`; em produção, variáveis de ambiente vindas do `.env` do servidor.

## Logs

- Serilog com **templates estruturados** (`_logger.LogInformation("Order {OrderId} paid by event {EventId}", ...)`), nunca interpolação de string.
- **Proibido logar dados pessoais**: e-mail, nome, endereço, CPF. Registrar **IDs** (`OrderId`, `CustomerId`, `EventId`).
- Nunca logar payloads completos da Stripe, headers de autenticação ou segredos.

## Banco

- EF Core com Npgsql. Nada de SQL específico do PostgreSQL fora da `Infrastructure`. Nada de stored procedures.
- Migrations com nomes descritivos (`AddOrderShippingAddress`). **Nunca editar uma migration que já foi aplicada em produção.**
- Unicidade e concorrência garantidas por **restrições do banco** (UNIQUE, token de concorrência), não por checagens em C#.
