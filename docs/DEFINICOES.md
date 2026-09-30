# Definições do Projeto — E-Commerce de Keycaps

> Documento de definição produzido em sessão de entrevista de arquitetura (29/09/2026).
> É a fonte de verdade para escopo e decisões. Mudanças de decisão devem gerar um ADR em `docs/decisoes/`.

---

## 1. Visão do produto

Loja virtual de **kits de keycaps (teclas para teclados mecânicos) vendidos em lotes limitados**, com marcas fictícias, pagamento real via **Stripe** (cartão e Pix) operando em **modo teste, pronto para produção**.

**Objetivo real:** projeto de portfólio para vagas .NET em empresas médias e grandes. O diferencial não é a interface, e sim o backend: domínio bem modelado, integração de pagamento correta (webhooks, idempotência, concorrência), testes que cobrem os riscos reais, CI/CD e operação com custo zero.

**Como o projeto evolui:** incrementos pequenos e independentes, cada um publicável sozinho. Sem prazo de entrega, com ritmo planejado (ver §12).

**Modo de trabalho:** o autor aprende e decide; o assistente explica, revisa e não entrega tudo pronto. Peças centrais (`Pedido`, `ConfirmarPagamento`, handler do webhook) são escritas pelo autor.

---

## 2. Escopo do MVP (v1)

Fatia vertical: **catálogo → carrinho → pagamento → resultado → envio**.

| Área | Na v1 |
|---|---|
| Catálogo | Público (sem login). Produtos inseridos por **seed**. Cada kit é um produto separado, **sem variações**. Estoque visível ("últimas unidades", "esgotado"). |
| Conta | Cadastro e login com ASP.NET Core Identity. **Conta obrigatória para comprar.** |
| Carrinho | Guardado no banco, por cliente (exige login para adicionar). Adicionar, remover e alterar quantidade. |
| Checkout | Endereço de entrega digitado no checkout, com **frete fixo**. Clicar em "Finalizar" cria o pedido e **reserva o estoque**. |
| Pagamento | **Stripe Checkout hospedado**, com **cartão e Pix**. |
| Resultado | Página de retorno que consulta o próprio backend até o status mudar. |
| Envio | Admin avança `Pago → EmSeparacao → Enviado → Entregue` por **endpoints de admin mínimos** (sem interface), autorizados pelo papel `Admin`. |
| Admin | Primeiro admin criado pelo comando `criar-admin`. |
| Operação | Deploy na Oracle Cloud, CI/CD, monitoramento, backups. |

**Critério de pronto da v1:** um visitante navega pelo catálogo, cria conta, compra com cartão de teste **e** com Pix de teste, o pedido fica `Pago` via webhook, e o admin leva o pedido até `Entregue`. Tudo isso publicado em URL pública com HTTPS, com CI verde e backup restaurado com sucesso pelo menos uma vez.

---

## 3. Fora de escopo (de propósito)

| Item | Onde fica | Motivo |
|---|---|---|
| Cancelamento, direito de arrependimento (CDC, 7 dias) e exclusão de conta (LGPD) | **v2** | São bloqueios para produção, e não para a v1 em modo teste. A v2 reaproveita o reembolso da v1. |
| Payment Element (formulário embutido) | v3 | A interface do cartão não é o diferencial. O Checkout hospedado já resolve PCI, 3DS e expiração. |
| Web API + SPA (React ou Angular) | v4 | Aprender um framework de frontend ao mesmo tempo que o resto diluiria o foco. |
| Interface de admin e cadastro de produtos por tela | futuro | Seed resolve a v1. |
| Job de conciliação com a Stripe | futuro | O webhook é a fonte da verdade, e a conciliação é só rede de segurança. |
| Cálculo de frete (Correios, Melhor Envio) | futuro | É um poço de complexidade que não demonstra nada do objetivo do projeto. |
| Pré-venda (*group buy*) | futuro (v3+) | Muda o modelo de estoque (vagas, prazos longos, reembolsos parciais). |
| Variações de produto (SKU) | fora | Cada kit é um produto próprio, como no mercado real. |
| Boleto | fora | É lento, raramente é pago e prende estoque por dias. |
| Endereços salvos na conta | futuro | Conveniência, não essencial. |
| Login externo (Google etc.) | futuro | Dependência externa sem ganho de aprendizado na v1. |
| Compra como convidado | fora | Decisão: conta obrigatória para comprar. |
| Dinheiro real | fora (checklist em §11) | Exige conta ativada, obrigações fiscais, legais e de suporte. Além disso, um recrutador não consegue testar uma loja real sem gastar dinheiro. |
| Redis, filas, microsserviços | fora | O banco resolve concorrência e idempotência. Seria superdimensionado e teria custo. |

---

## 4. Usuários e fluxos

| Ator | Pode |
|---|---|
| **Visitante** | Ver catálogo e detalhes do produto. Criar conta. |
| **Cliente** (autenticado) | Tudo do visitante, mais: carrinho, finalizar compra, pagar e ver **os próprios** pedidos e status. |
| **Admin** (papel `Admin` no Identity) | Avançar as transições de envio de pedidos. **Não** altera estados de pagamento. |
| **Stripe** (sistema) | Envia webhooks assinados, que são a única fonte da verdade de pagamento. |

**Fluxo principal:** catálogo → adicionar ao carrinho → login ou cadastro → finalizar (endereço + reserva) → Stripe Checkout (cartão ou Pix) → webhook confirma → página de retorno mostra "Pago" → admin: separação → envio (com rastreio) → entregue.

---

## 5. Stack escolhida e justificativa

| Camada | Escolha | Por quê | Alternativa rejeitada |
|---|---|---|---|
| Backend | **.NET (ASP.NET Core)** | Área do autor e público-alvo (empresas .NET). | — |
| Frontend v1 | **ASP.NET Core MVC + Razor** | O autor já domina (projeto agenda-pessoal). Toda a energia vai para domínio, pagamento e testes. Cookie é o modelo de autenticação mais seguro para esse caso. | SPA (v4), Blazor (novidade sem retorno proporcional) |
| Banco | **PostgreSQL** | Portabilidade entre provedores gratuitos. Roda em x86 e ARM. | SQL Server: exige 2 GB de RAM e não roda em ARM, então não cabe na Oracle Always Free. |
| ORM | **EF Core** (provedor Npgsql) | Padrão do ecossistema, e o autor já tem experiência. | — |
| Autenticação | **ASP.NET Core Identity com cookies** | Hash de senha, bloqueio por tentativas, tokens e papéis prontos e auditados. | Cookie feito à mão (inseguro para múltiplos usuários); login externo (dependência). |
| Pagamento | **Stripe Checkout hospedado** (cartão + Pix) | PCI, 3DS e expiração de sessão já resolvidos. Pix disponível para contas BR (verificado na documentação). | Payment Element (v3) |
| Hospedagem | **Oracle Cloud Always Free**, VM **Ampere A1 (ARM64)**, separada da agenda | Custo zero sem prazo de validade. O autor já opera VM Oracle com Nginx e Certbot. | Azure for Students: o crédito acaba e a portabilidade exigiria trabalho duplo. |
| Execução | **Docker Compose** (app + PostgreSQL) | Ambiente idêntico local e produção. Portabilidade: migrar de provedor é "instalar Docker e subir". | systemd direto (como a agenda), que não cobre PostgreSQL nem ARM com a mesma facilidade. |
| Borda | **Nginx + Certbot no host**, subdomínio **DuckDNS** | Caminho já dominado pelo autor, com renovação automática já resolvida. | Caddy no Compose (novidade sem ganho). |
| Testes | **xUnit, Testcontainers, `WebApplicationFactory`, Shouldly ou `Assert`** | PostgreSQL real nos testes de integração, sem mocks de banco. | FluentAssertions (licença comercial nas versões novas). |
| CI/CD | **GitHub Actions** + GitHub Container Registry | Gratuito para repositório público. | — |
| Logs | **Serilog** (JSON na saída padrão) | Logs estruturados e consultáveis por ID. | — |

**Faz sentido para uma pessoa sozinha?** Sim, porque cada peça nova foi pesada contra o "orçamento de novidade" do autor. Tudo que não diferencia o projeto (interface, frete, infraestrutura exótica) usa o caminho conhecido ou foi cortado.

---

## 6. Arquitetura

**Monolito com Clean Architecture em 4 projetos, modular por pastas.**

```
            ┌────────────┐
            │    Web     │  MVC, controllers finos, endpoint do webhook,
            │ (ASP.NET)  │  endpoints de admin, Program.cs (composição)
            └──┬──────┬──┘
               │      │
               ▼      ▼
   ┌──────────────┐  ┌────────────────┐
   │ Application  │◄─┤ Infrastructure │  EF Core + DbContext + PostgreSQL,
   │ casos de uso │  │                │  Identity, SDK da Stripe, relógio
   └──────┬───────┘  └───────┬────────┘
          │                  │
          ▼                  ▼
        ┌──────────────────────┐
        │        Domain        │  Pedido, Produto, máquina de estados,
        │   zero pacotes NuGet │  value objects, exceções de domínio
        └──────────────────────┘
```

**Regras:**
- As dependências apontam **sempre para dentro**. O `Domain` não referencia ninguém.
  - *Por quê:* as regras de negócio não mudam quando a tecnologia muda (e ela já mudou uma vez: SQL Server → PostgreSQL), e podem ser testadas isoladamente, em milissegundos.
- A `Application` define interfaces (ex.: `IGatewayPagamento`), e a `Infrastructure` as implementa (inversão de dependência).
- **Controllers finos:** nenhuma regra de negócio no `Web`. Quando a SPA chegar (v4), nada de regra é reescrito.
- **Módulos como pastas** dentro de cada camada: `Catalogo`, `Carrinho`, `Pedidos`, `Pagamentos`, `Identidade`. As fronteiras são reforçadas por **testes de arquitetura**.
- O **Identity** vive na `Infrastructure`. O `Domain` conhece o cliente apenas pelo **ID**.

**Onde fica cada peça do webhook:**
- endpoint HTTP (lê o corpo bruto) → `Web`
- verificação de assinatura e tradução do evento da Stripe → `Infrastructure`
- caso de uso `ConfirmarPagamento` (idempotência, transição, estoque) → `Application`
- transições e invariantes → `Domain` (`Pedido`)

**Por que não algo mais complexo:** um monolito modular "puro" (camadas por módulo) daria 15+ projetos para uma pessoa só, com problemas de comunicação entre módulos que a v1 não tem. Microsserviços, filas e Redis resolvem problemas de escala e de equipes que este projeto não tem, e custariam dinheiro e atenção.

---

## 7. Fluxo de pagamento detalhado

### 7.1 Finalizar (criar pedido e reservar)

1. O cliente autenticado envia o carrinho e o endereço de entrega.
2. Tudo numa **única transação no banco**:
   - Cria o `Pedido` em `AguardandoPagamento`, com um **snapshot** dos itens (preço unitário do servidor, nunca do cliente), do frete fixo e do `EnderecoEntrega` (nome do destinatário + endereço).
   - Para cada item, faz a **reserva atômica**:
     ```sql
     UPDATE Produtos SET Estoque = Estoque - @qtd
     WHERE Id = @id AND Estoque >= @qtd
     ```
     Se afetar 0 linhas, a transação inteira é desfeita e o cliente vê "esgotado".
3. Commit.

> **Semântica de estoque:** `Estoque` é o disponível para venda. Reservar = decrementar. Liberar = incrementar. O pagamento confirmado não mexe no estoque, porque ele já foi reservado.

### 7.2 Criar a sessão na Stripe

4. Chama `POST /v1/checkout/sessions` com:
   - `mode=payment`, `line_items` em **`brl`**, montados a partir do snapshot do pedido
   - `client_reference_id` e `metadata.order_id` = ID do pedido
   - `expires_at` = agora + **30 minutos** (o mínimo permitido)
   - `payment_method_options.pix.expires_after_seconds` = **1800** (30 minutos, contados a partir do momento em que o cliente escolhe Pix; o padrão da Stripe seria 4 horas)
   - `success_url` → `/orders/{id}/return`, `cancel_url` → carrinho
   - **chave de idempotência** `sessao-{pedidoId}` na requisição
5. Grava o `StripeSessionId` no pedido e redireciona o cliente para a URL da Stripe.
6. **Se a criação da sessão falhar:** o pedido vai para `Falhou`, o estoque é liberado (compensação) e o cliente vê o erro.

### 7.3 Webhook (fonte da verdade)

Endpoint `POST /webhooks/stripe`:

1. Lê o **corpo bruto** e verifica a assinatura (`Stripe-Signature` + segredo `whsec_...`). Se for inválida ou estiver ausente, responde **`400`** sem efeito algum.
2. Tipos tratados: `checkout.session.completed`, `checkout.session.async_payment_succeeded`, `checkout.session.async_payment_failed`, `checkout.session.expired`. Qualquer outro tipo recebe **`200`** e é ignorado.
3. Numa **única transação**:
   1. `INSERT` do `event.id` em `EventosProcessados` (restrição **UNIQUE**). Se houver violação de unicidade, o evento é duplicado: responde **`200`** sem efeito.
   2. Carrega o pedido por `metadata.order_id`. Se o pedido não existir, registra um alerta e responde `200`.
   3. **Confere `amount_total` e `currency` contra o pedido.** Se divergirem, o pedido não é marcado como pago, e um alerta é registrado.
   4. Executa a transição pelo **método do domínio** (ver §8). A atualização é condicional ou usa concorrência otimista: se outra execução já fez a transição, nada acontece.
   5. Commit e resposta **`200`**.
4. Qualquer exceção desfaz a transação inteira e o endpoint responde **`500`**, e a Stripe tenta de novo (por até cerca de 3 dias).

**Decisão pelo status do pagamento, e não pelo meio:**

| Evento | Condição | Resultado |
|---|---|---|
| `checkout.session.completed` | `payment_status = paid` | → `Pago` |
| `checkout.session.completed` | `payment_status = unpaid` (Pix gerado) | → `AguardandoConfirmacao` |
| `checkout.session.async_payment_succeeded` | — | → `Pago` (também a partir de `AguardandoPagamento`, se chegar fora de ordem) |
| `checkout.session.async_payment_failed` | — | → `Falhou` e **libera o estoque** |
| `checkout.session.expired` | — | → `Expirado` e **libera o estoque** |

**Regra de ouro do estoque:** a reserva só é liberada por **evento da Stripe** (expiração ou falha), **nunca por timer próprio**. Uma sessão expirada não aceita mais pagamento, e é isso que torna impossível "pagar por um item já liberado" no cartão.

**Única exceção (D35):** um pedido `AguardandoPagamento` **sem `StripeSessionId`** há mais de 10 minutos (a aplicação caiu entre o commit do pedido e a criação da sessão) é marcado como `Falhou`, e a reserva é liberada por um job. É seguro porque, sem sessão, não existe forma de pagar esse pedido.

### 7.4 Página de retorno

`/orders/{id}/return` mostra "Confirmando seu pagamento…" e consulta o **próprio backend** (`GET /orders/{id}/status`, só para o dono do pedido) até o estado mudar. Ela **nunca** altera o estado. O redirecionamento não prova nada.

### 7.5 Pagamento confirmado sem estoque (defesa)

Se uma confirmação chegar para um pedido cuja reserva já foi liberada (estado inválido, que deveria ser impossível, mas pode acontecer por ajuste manual futuro de estoque):
1. Tenta reservar de novo, de forma atômica. Se conseguir → `Pago`.
2. Se não conseguir → `PagoSemEstoque` → reembolso via API da Stripe com a chave de idempotência `reembolso-{pedidoId}` → `Reembolsado`. Se o reembolso falhar, o pedido continua em `PagoSemEstoque` e gera um alerta.

O reembolso de Pix é aceito em até 90 dias depois do pagamento.

### 7.6 Modo teste e produção

- Desenvolvimento e demo pública usam **só chaves de teste**. Os webhooks locais chegam pela Stripe CLI (`stripe listen`).
- Nenhum `if (ambiente == teste)` no fluxo. Produção = trocar as chaves e cadastrar o webhook de produção.
- Pix em teste: CPF `000.000.000-00`, botão "Simular digitalização", e e-mails de cenário (`succeed_immediately@…`, `expire_immediately@…`, `fill_never@…`).
- Limites do Pix: **de R$ 0,50 a R$ 3.000 por transação.** Acima disso, o Checkout não oferece Pix.

---

## 8. Máquina de estados do pedido

```
                           ┌──► Expirado   (session.expired → libera estoque)
                           │
  [Finalizar]              │
  reserva estoque ──► AguardandoPagamento ──► AguardandoConfirmacao ──► Falhou
                           │                   (Pix gerado)      (async_failed → libera estoque)
                           │                        │
                           │   completed(paid)      │ async_succeeded
                           └────────────┐    ┌──────┘
                                        ▼    ▼
                                         Pago  ──────────► PagoSemEstoque ──► Reembolsado
                                          │    (só defesa §7.5)  (reembolso automático)
                              [admin]     ▼
                                     EmSeparacao ──► Enviado ──► Entregue
                                       [admin]    (rastreio) [admin]
```

**Regras:**
- `Status` tem **`private set`**. Cada transição é um método com intenção: `MarcarAguardandoConfirmacao()`, `ConfirmarPagamento(...)`, `Expirar()`, `MarcarFalha()`, `MarcarSemEstoque()`, `MarcarReembolsado()`, `IniciarSeparacao()`, `MarcarEnviado(rastreio)`, `MarcarEntregue()`.
- Uma transição fora do estado permitido lança uma exceção de domínio, que a API transforma em **`409 Conflict`**.
- **As transições de pagamento só podem ser disparadas pelo webhook.** O admin só tem acesso a `IniciarSeparacao`, `MarcarEnviado` e `MarcarEntregue`.
- **Um endpoint por ação** (ex.: `POST /admin/orders/{id}/ship`), sem PATCH genérico de status.
- Estados terminais na v1: `Entregue`, `Expirado`, `Falhou` e `Reembolsado`. O cancelamento entra na v2.

**Nomes no código (D36).** Este documento usa os nomes em português. No código, eles ficam em inglês:

| Documento | Código |
|---|---|
| `Pedido` / `StatusPedido` | `Order` / `OrderStatus` |
| `AguardandoPagamento` | `AwaitingPayment` |
| `AguardandoConfirmacao` | `AwaitingConfirmation` |
| `Pago` | `Paid` |
| `PagoSemEstoque` | `PaidOutOfStock` |
| `Reembolsado` | `Refunded` |
| `Expirado` | `Expired` |
| `Falhou` | `Failed` |
| `EmSeparacao` | `Picking` |
| `Enviado` | `Shipped` |
| `Entregue` | `Delivered` |
| `ConfirmarPagamento()` | `ConfirmPayment()` |
| `IniciarSeparacao()` / `MarcarEnviado()` / `MarcarEntregue()` | `StartPicking()` / `MarkShipped()` / `MarkDelivered()` |
| `EnderecoEntrega` | `ShippingAddress` |
| `EventosProcessados` | `ProcessedStripeEvents` |
| `IGatewayPagamento` | `IPaymentGateway` |
| comando `criar-admin` | comando `create-admin` |

---

## 9. Segurança e LGPD

**Autenticação e autorização**
- ASP.NET Core Identity com cookies (`HttpOnly`, `Secure`, `SameSite`) e antiforgery nos formulários.
- Autorização por papel (`Admin`). O cliente só vê os próprios pedidos (checagem de dono na `Application`).
- **Primeiro admin:** comando `criar-admin` (senha pedida de forma interativa ou gerada e exibida uma vez), sem senha guardada. Em `Development`, há um seed de admin de teste. **Nenhuma rota atribui o papel de admin.**

**Segredos**
- Em desenvolvimento: `dotnet user-secrets`. Em produção: `.env` fora do repositório (`chmod 600`), carregado pelo `env_file` do Compose. **Segredos nunca entram na imagem Docker nem no git.**
- A aplicação **falha ao iniciar** se faltar um segredo obrigatório (`ValidateOnStart`).
- **gitleaks** no CI. Chaves de teste e de produção nunca convivem no mesmo ambiente.

**Dados de cartão:** nunca passam pelo sistema. O sistema guarda só os IDs da Stripe (`cs_…`, `pi_…`).

**Dados pessoais na v1 (minimização)**

| Dado | Onde | Finalidade |
|---|---|---|
| E-mail, hash da senha | Identity | Conta e login |
| Nome do destinatário, endereço | Snapshot `EnderecoEntrega` no `Pedido` | Entrega |
| Histórico de pedidos | `Pedido` | Execução do contrato, financeiro |

- **Logs registram só IDs**, nunca e-mail, nome ou endereço.
- **Exclusão de conta (definida agora, implementada na v2):** apaga a conta do Identity; mantém os pedidos com `ClienteId = null`; mantém o snapshot de nome e endereço pelo prazo legal e depois anonimiza; bloqueia a exclusão enquanto houver pedidos em andamento.
- **Desde a v1:** o `Pedido` não depende da existência da conta (`ClienteId` anulável, endereço como snapshot).

**Infraestrutura**
- O PostgreSQL **não publica nenhuma porta**. A aplicação escuta só em `127.0.0.1`. O Docker passa por cima do UFW, então nunca se publica em `0.0.0.0`.
- A rede da Oracle libera só as portas 22, 80 e 443. SSH só por chave, root não loga pelo SSH, fail2ban ativo.
- `unattended-upgrades` aplica as atualizações de segurança. O Dependabot cuida das imagens Docker e das actions.

---

## 10. Qualidade

### 10.1 Estratégia de testes (foco no risco, não no formulário)

**Unitários** (`Domain` e `Application` com uma Stripe falsa; muitos, em milissegundos)
- Todas as transições válidas, e todas as inválidas lançando exceção.
- `payment_status` `paid` e `unpaid` levam aos estados corretos.
- Confirmação sem estoque → `PagoSemEstoque` + um único pedido de reembolso, com chave de idempotência.
- Valor ou moeda divergente → o pedido não é marcado como pago.
- Validação do `EnderecoEntrega`.

**Integração** (PostgreSQL real via Testcontainers + `WebApplicationFactory`; eventos assinados com um segredo de teste; sem rede)
- Assinatura inválida ou ausente → `400`, sem efeito.
- Evento válido → `200`, pedido `Pago`, evento registrado.
- Mesmo evento 2x **em sequência** → efeito único.
- Mesmo evento 2x **em paralelo** → efeito único.
- Dois checkouts em paralelo disputando o último item → um reserva, o outro recebe "esgotado".
- `expired` → `Expirado` e estoque devolvido. Se duplicado, o estoque **não** é devolvido duas vezes.
- Evento fora de ordem (`async_payment_succeeded` antes de `completed`) → estado final correto.
- Tipo desconhecido → `200` e ignorado.
- Falha no meio da transação → tudo desfeito e `500`.
- Testes de arquitetura (dependências entre camadas e módulos).

**Ponta a ponta** (checklist manual antes de cada deploy; Stripe em modo teste + Stripe CLI)
- Cartão `4242…` aprovado → `Pago`.
- Cartão recusado → continua no Checkout, e o pedido continua aguardando.
- Pix de teste → `AguardandoConfirmacao` → `Pago`.
- Pix de teste expirado → `Falhou` e estoque liberado.

### 10.2 CI (GitHub Actions)

Bloqueiam o merge na `main`:
1. build sem erros (avisos como erro)
2. testes unitários
3. testes de integração
4. gitleaks
5. `dotnet format --verify-no-changes`

Trabalho **via branches e PRs**, com **proteção de branch sem bypass, inclusive para administradores**. Teste que falha aleatoriamente é consertado na hora, nunca rodado de novo até passar.

### 10.3 CD (deploy contínuo com aprovação)

CI verde na `main` → imagem **ARM64** publicada no GitHub Container Registry (tag = hash do commit) → Environment **`production` com aprovação obrigatória do autor** (é o momento do checklist ponta a ponta) → SSH com usuário `deploy` limitado a um **comando forçado** → `deploy.sh <tag>`, que valida a tag, faz o deploy **pelo digest**, aplica as migrations como **passo explícito** (nunca na inicialização da aplicação) e reinicia a aplicação.

Endurecimento do workflow: `permissions` mínimas, actions fixadas por **hash de commit**, nada de `pull_request_target`, segredos só no Environment, `known_hosts` fixado e **2FA** na conta do GitHub. O usuário `deploy` **não** entra no grupo `docker`; ele só pode executar o `deploy.sh` via `sudo`.

Voltar para uma versão anterior = executar o workflow de deploy com a tag anterior.

### 10.4 Ambientes

| Ambiente | Onde | Stripe | Banco |
|---|---|---|---|
| Local | Docker Compose na máquina do autor | Chaves de teste + `stripe listen` | PostgreSQL em container |
| CI | GitHub Actions | Nenhuma (eventos assinados localmente) | Testcontainers |
| Produção (demo pública) | VM Oracle A1 | Chaves de teste | PostgreSQL em container |

### 10.5 Logs e observabilidade

- **Serilog** em JSON na saída padrão. **Rotação de logs no Docker** (`max-size`, `max-file`). Cada linha do fluxo de pagamento tem o ID do evento e o ID do pedido.
- **`/health`** verifica a conexão com o banco e o espaço em disco (alerta acima de cerca de 85%).
- **Monitor externo gratuito** consulta o `/health` e avisa quando o status muda.
- **E-mails de falha de webhook da própria Stripe** (independentes do servidor).
- A aplicação **não** manda e-mail por erro (evita fadiga de alertas e o problema do sistema tentar reportar a própria falha).

### 10.6 Backups

- `pg_dump` diário via timer do systemd, criptografado com **`age`** (só a chave pública fica na VM; a privada fica com o autor).
- Enviado para armazenamento de **outro provedor** (Cloudflare R2 ou Backblaze B2) com credencial **só de escrita**. A retenção (7 diários + 4 semanais) é feita por regra de ciclo de vida **do bucket**.
- **Dead man's switch** (healthchecks.io): se o backup não reportar sucesso, chega um alerta.
- **Teste de restauração mensal**, documentado em runbook. O primeiro acontece **antes** do go-live.
- 2 cópias locais na VM, só como restauração rápida complementar.
- O ID do pedido nos metadados da Stripe permite reconstruir pedidos pagos entre o último backup e um desastre.

### 10.7 VM descartável

A VM pode ser recuperada pela Oracle por ociosidade: em 7 dias, CPU p95 < 20%, rede < 20% e memória < 20% (só A1), todas ao mesmo tempo. Por isso ela é tratada como **descartável**: um **script versionado de reconstrução** (Docker, Nginx, Certbot, usuário `deploy`, hardening, `unattended-upgrades`, timers de backup) + restauração do backup. O script é executado de verdade pelo menos uma vez no M0. A VM A1 fica dimensionada no necessário (ex.: 1 OCPU e 6 GB).

**Regras de custo:** tudo precisa ser gratuito. Nada de Redis, filas ou VMs extras. Todo recurso novo tem o custo verificado antes de ser criado.

---

## 11. O que falta para produção (checklist)

A v1 é **pronta para produção**, mas **não vai para produção**. Para ir, todos os itens abaixo precisam estar feitos:

**Código e configuração**
- [ ] gitleaks verde; nenhum segredo no repositório
- [ ] nenhum desvio de fluxo por ambiente; toda a configuração por ambiente

**Stripe**
- [ ] conta Stripe **ativada** (dados da pessoa ou empresa + conta bancária)
- [ ] chaves de produção num `.env` separado; webhook de produção com o próprio segredo
- [ ] Pix ativado no modo de produção

**Legal (bloqueios)**
- [ ] **v2 entregue:** cancelamento, direito de arrependimento (CDC) e exclusão de conta (LGPD)
- [ ] termos de uso
- [ ] política de privacidade (incluindo o compartilhamento de dados com a Stripe e seus parceiros)
- [ ] prazo legal de retenção dos pedidos definido
- [ ] emissão de nota fiscal

**Operação**
- [ ] teste de restauração de backup bem-sucedido
- [ ] monitor externo e dead man's switch ativos
- [ ] checklist ponta a ponta executado com chaves de produção num pedido real de baixo valor, depois reembolsado

---

## 12. Plano de execução da v1

**Ritmo:** de 2 a 3 horas por dia útil (**10 a 15 horas por semana**), constante inclusive em semana de provas.
**Estimativa da v1:** 120 a 180 horas, **cerca de 3 meses**. Registrar a data de início no `STATUS.md` e revisar a estimativa ao fim do M2.

| Marco | Entrega | Horas estimadas |
|---|---|---|
| **M0** | Spike de premissas; solution nos 4 projetos; CI verde; página "Olá" publicada na VM com HTTPS via CD; script de reconstrução da VM. **Timebox: 2 semanas.** Se estourar, o CD completo vira deploy manual e o endurecimento volta no M6. | 25–35h |
| **M1** | Catálogo (seed) + Identity (cadastro, login, papéis) + `criar-admin` | 15–25h |
| **M2** | Carrinho, pedido, reserva atômica + testes de concorrência | 20–30h |
| **M3** | Stripe Checkout com cartão + webhook idempotente + página de retorno | 20–30h |
| **M4** | Pix, expiração, falha e reembolso por falta de estoque | 15–25h |
| **M5** | Endpoints de admin para o envio | 8–12h |
| **M6** | Backups, monitoramento, checklist de go-live | 15–20h |

**Roadmap depois da v1** (cada item é independente; a ordem é decidida depois da v1):
- **v2:** direitos do consumidor e do titular (cancelamento, arrependimento, exclusão de conta)
- **v3:** Payment Element embutido
- **v4:** Web API + SPA (React ou Angular)
- **Depois:** interface de admin, conciliação, frete, pré-venda, endereços salvos, login externo

---

## 13. Decisões tomadas

| # | Decisão | Alternativas rejeitadas |
|---|---|---|
| D01 | Projeto de portfólio, sem prazo, com incrementos independentes | — |
| D02 | Modo teste, pronto para produção | Dinheiro real |
| D03 | Keycaps em lotes limitados, marcas fictícias, sem variações, sem pré-venda | Café, prints, livros, produto digital |
| D04 | Catálogo público; conta obrigatória para comprar | Catálogo fechado; compra como convidado |
| D05 | Produtos via seed; endpoints de admin só para o envio | Interface de admin na v1 |
| D06 | Frete fixo; endereço como snapshot no pedido | Integração com Correios ou Melhor Envio |
| D07 | Stripe Checkout hospedado (Payment Element na v3) | Payment Element na v1 |
| D08 | Cartão + Pix; o handler decide pelo `payment_status` | Boleto |
| D09 | Webhook assinado como fonte da verdade; a página de retorno só consulta o backend | Confiar no redirecionamento; polling na Stripe como fonte principal |
| D10 | Idempotência pelo ID do evento (UNIQUE) + estado do pedido | Heurística por "pedido parecido" |
| D11 | Concorrência garantida pelo banco (transação, UNIQUE, atualização condicional) | Lock em memória; Redis |
| D12 | Reserva atômica em "Finalizar"; liberação só por evento da Stripe | Reservar no carrinho; baixar só no pagamento; timer próprio |
| D13 | Valor e moeda conferidos no webhook; ID do pedido nos metadados | — |
| D14 | Confirmação sem estoque → reembolso automático e idempotente | Cancelar o pedido de outro cliente |
| D15 | Sessão de 30 minutos; Pix de 30 minutos (reserva máxima de cerca de 1 hora) | Padrões da Stripe (24 horas e 4 horas) |
| D16 | Máquina de estados no `Pedido` (modelo rico); admin só executa o envio; um endpoint por ação | PATCH genérico de status |
| D17 | Cancelamento, arrependimento e exclusão de conta na v2 (bloqueio para produção) | Na v1 |
| D18 | .NET com MVC e Razor na v1; SPA na v4 | React, Angular ou Blazor na v1 |
| D19 | PostgreSQL | SQL Server (não cabe no destino) |
| D20 | Oracle Always Free A1, VM separada e descartável, com script de reconstrução | Azure for Students; VM compartilhada com a agenda; conta Pay As You Go; carga artificial |
| D21 | Docker Compose para a app e o banco; Nginx e Certbot no host; DuckDNS | systemd direto; Caddy |
| D22 | Clean Architecture em 4 projetos; módulos por pastas + testes de arquitetura | Projeto único; monolito modular com camadas por módulo |
| D23 | ASP.NET Core Identity com cookies | Autenticação feita à mão; login externo |
| D24 | Primeiro admin via comando `criar-admin`; seed só em `Development` | Promoção por SQL; seed por variável de ambiente |
| D25 | Segredos: user-secrets + `.env` protegido + `ValidateOnStart` + gitleaks | `.env` no projeto; cofre gerenciado |
| D26 | Minimização de dados; logs só com IDs; política de exclusão definida | — |
| D27 | Testes focados no risco: unitários + integração com Testcontainers + checklist ponta a ponta | Ponta a ponta como garantia principal |
| D28 | CI com 5 itens bloqueando o merge; PRs; proteção de branch sem bypass | Push direto na `main` |
| D29 | CD com aprovação manual, SSH com comando forçado, deploy pelo digest, migrations explícitas | Deploy manual; CD sem proteção |
| D30 | Observabilidade: Serilog, `/health`, monitor externo, alertas da Stripe | E-mail de erro enviado pela aplicação |
| D31 | Backups criptografados fora do provedor, dead man's switch, teste de restauração mensal | Backup só na VM |
| D32 | `unattended-upgrades` (segurança) + Dependabot | Atualização manual |
| D33 | Custo zero como regra | — |
| D34 | Carrinho guardado no banco, por cliente (exige login para adicionar ao carrinho) | Carrinho anônimo em cookie ou sessão |
| D35 | Única exceção ao "nunca timer próprio": um job libera pedidos `AwaitingPayment` **sem `StripeSessionId`** há mais de 10 minutos (sem sessão, nenhum pagamento é possível) | Reserva presa para sempre |
| D36 | Código (identificadores) em **inglês**; documentação e textos da interface em **português (pt-BR)**. Solution `KeycapStore`, **.NET 10** | Identificadores em português |

---

## 14. Questões em aberto

| # | Questão | Como resolver |
|---|---|---|
| Q1 | Quais eventos o Checkout emite com Pix na prática (`checkout.session.async_payment_*` e/ou `payment_intent.*`)? | Spike no M0 com a Stripe CLI. O handler deve tratar o que chegar de forma idempotente. |
| Q2 | Conta Stripe criada e Pix ativado no Dashboard em modo teste | Spike no M0, com acompanhamento |
| Q3 | Disponibilidade de capacidade A1 na região da conta Oracle | Spike no M0 |
| Q4 | Disponibilidade dos runners ARM do GitHub e condições dos planos gratuitos do R2/B2, do monitor externo e do healthchecks.io | Verificar na documentação de cada um antes de usar |
| Q5 | Prazo legal de retenção dos pedidos | Checklist de produção (§11); não é decisão de código |

> Resolvidas depois do fechamento: onde fica o carrinho (→ D34) e o pedido órfão sem sessão (→ D35).

---

## 15. Riscos

| # | Risco | Prob. | Impacto | Mitigação |
|---|---|---|---|---|
| R1 | **Abandono**: M0 pesado (só infraestrutura, sem "ver a loja") + expectativa de "semanas" contra uma realidade de cerca de 3 meses | Alta | Alto | Timebox de 2 semanas no M0, com plano B; marcos curtos com entrega visível; horas e marco registrados no `STATUS.md`; revisão da estimativa no M2 |
| R2 | **Dependência do assistente**: entendimento raso ("sei a regra, não sei o porquê") e erros do assistente sobre fornecedores | Média | Alto | O autor escreve as peças centrais; explica cada marco de volta antes de fechá-lo; ADRs escritos com as próprias palavras; afirmações sobre fornecedores sempre verificadas na documentação oficial |
| R3 | **Premissas externas**: Pix, capacidade A1, política de ociosidade, planos gratuitos | Média | Médio | Spike de premissas no início do M0, antes de construir em cima delas |
| R4 | **VM recuperada por ociosidade** | Alta | Médio | VM descartável, script de reconstrução testado, backup fora do provedor, monitor externo |
| R5 | **Aumento de escopo** (variações, pré-venda, "só mais um detalhe") | Média | Médio | Lista de fora de escopo (§3); subagente revisor aponta desvios; toda mudança de escopo gera um ADR |
| R6 | **Detalhes da integração com a Stripe** (corpo bruto na assinatura, eventos fora de ordem, duplicatas) | Média | Alto | Camada de testes de integração (§10.1); Stripe CLI no desenvolvimento |
| R7 | **Operar sozinho uma VM pública com endpoint de pagamento** | Média | Alto | Hardening de SSH, portas mínimas, `unattended-upgrades`, deploy com comando forçado, segredos fora do repositório |
| R8 | **Vazamento de segredos** | Baixa | Alto | user-secrets, `.env` fora do repositório, gitleaks bloqueando o merge, Environment com aprovação |
| R9 | **Planos gratuitos mudarem ou acabarem** | Média | Médio | Portabilidade via Docker + PostgreSQL; nenhuma dependência de serviço proprietário de um único provedor |
