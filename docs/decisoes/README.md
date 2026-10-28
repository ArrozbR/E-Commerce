# Registros de Decisão de Arquitetura (ADRs)

Cada arquivo registra **uma** decisão importante: contexto, decisão, alternativas rejeitadas e consequências. Formato em `TEMPLATE.md`.

**Regras:**
- Uma decisão aceita não é editada para mudar de ideia. Crie um novo ADR e marque o antigo como "Substituído por NNNN".
- Escreva com as suas palavras. Se não conseguir explicar a decisão numa entrevista, o ADR ainda não está pronto.

| # | Decisão |
|---|---|
| [0001](0001-projeto-portfolio-modo-teste.md) | Portfólio em modo teste, pronto para produção, com custo zero |
| [0002](0002-escopo-v1-e-roadmap.md) | Escopo da v1 (keycaps, fatia vertical) e roadmap incremental |
| [0003](0003-stripe-checkout-hospedado.md) | Stripe Checkout hospedado na v1 |
| [0004](0004-webhook-fonte-da-verdade.md) | Webhook assinado como única fonte da verdade do pagamento |
| [0005](0005-idempotencia-e-concorrencia-no-banco.md) | Idempotência e concorrência garantidas pelo banco |
| [0006](0006-reserva-de-estoque-e-expiracao.md) | Reserva em "Finalizar", liberada só por evento da Stripe |
| [0007](0007-cartao-e-pix.md) | Cartão e Pix, com decisão pelo `payment_status` |
| [0008](0008-maquina-de-estados-no-dominio.md) | Máquina de estados no `Order`; o admin só executa o envio |
| [0009](0009-clean-architecture-quatro-projetos.md) | Clean Architecture em 4 projetos, módulos por pastas |
| [0010](0010-mvc-razor-na-v1.md) | MVC + Razor na v1; SPA na v4 |
| [0011](0011-postgresql.md) | PostgreSQL |
| [0012](0012-hospedagem-oracle-vm-descartavel.md) | Oracle Always Free, VM A1 descartável, Docker Compose |
| [0013](0013-identity-e-primeiro-admin.md) | Identity com cookies; primeiro admin por comando |
| [0014](0014-gestao-de-segredos.md) | Gestão de segredos |
| [0015](0015-dados-pessoais-e-exclusao.md) | Dados pessoais mínimos e política de exclusão |
| [0016](0016-estrategia-de-testes.md) | Estratégia de testes focada no risco |
| [0017](0017-ci-cd.md) | CI com merge bloqueado; CD com aprovação e SSH restrito |
| [0018](0018-observabilidade-backups-atualizacoes.md) | Observabilidade externa, backups fora do provedor, atualizações |
| [0019](0019-codigo-em-ingles.md) | Código em inglês; documentação e interface em português |
| [0020](0020-vm-e2-micro-provisoria.md) | VM E2.1.Micro provisória, até haver capacidade A1 |
| [0021](0021-atualizacoes-automaticas-diarias.md) | Atualizações automáticas diárias (comuns + Docker) às 03:30 |
| [0022](0022-chaves-data-protection-no-postgresql.md) | Chaves do Data Protection no PostgreSQL |
| [0023](0023-admin-pelo-comando-tambem-no-pc.md) | Admin pelo comando também no PC (sem seed); conta existente é promovida |
| [0024](0024-frete-gratis-por-estado.md) | Frete por estado: grátis no Centro-Oeste, SP e RJ; R$ 15,00 nos demais |
| [0025](0025-roadmap-v2-funcionalidades-v3-visual.md) | Roadmap: v2 com tudo o que não é visual (parte legal primeiro), v3 visual em Razor, sem v4 |
| [0026](0026-validade-da-sessao-a-partir-do-pedido.md) | Validade da sessão do Checkout: criação do pedido + 31 minutos |
| [0027](0027-eventos-do-webhook-na-versao-do-sdk.md) | Eventos do webhook na versão da API do SDK (snapshot, `endive`) |
| [0028](0028-pagina-minima-de-envio-para-o-admin.md) | Página mínima de envio para o admin (em vez de endpoints sem interface) |
| [0029](0029-comando-para-trocar-a-senha-do-admin.md) | Comando `reset-admin-password` para trocar a senha de um admin |
| [0030](0030-backups-na-cloudflare-r2.md) | Backups na Cloudflare R2, com bucket lock no lugar da credencial só de escrita |
| [0031](0031-cancelamento-pelo-cliente-antes-do-envio.md) | Cancelamento pelo cliente antes do envio, com reembolso automático |
| [0032](0032-direito-de-arrependimento.md) | Direito de arrependimento: reembolso na hora e devolução conferida pelo admin |
| [0033](0033-exclusao-de-conta.md) | Exclusão de conta: a conta some, os pedidos ficam sem dono e o endereço espera o prazo legal |
| [0034](0034-cadastro-de-produtos-pelo-admin.md) | Cadastro de produtos pelo admin: estoque por ajuste atômico e produto ativo/inativo |
| [0035](0035-endereco-salvo-na-conta.md) | Endereço salvo na conta: um por cliente, só para preencher o checkout |
| [0036](0036-endereco-salvo-cadastrado-pela-conta.md) | Endereço salvo também cadastrado e alterado pela "Minha conta" |
| [0037](0037-conciliacao-com-a-stripe.md) | Conciliação com a Stripe: pedidos parados conferidos a cada 10 minutos e corrigidos pelo mesmo caminho do webhook |
| [0038](0038-site-em-ingles.md) | Site em inglês: todas as páginas, português por padrão, formatos americanos em inglês e a Stripe no idioma do pedido |
| [0039](0039-login-com-google.md) | Login com Google: conta nova na hora, nunca junta sozinho com uma conta com senha, admin só com senha |
