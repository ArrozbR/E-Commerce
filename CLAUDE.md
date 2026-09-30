# KeycapStore

Loja de kits de keycaps em lotes limitados, com pagamento via Stripe (cartão + Pix) em **modo teste, pronto para produção**. É um projeto de portfólio .NET: o diferencial é o backend (domínio, webhooks, idempotência, concorrência, testes, CI/CD).

- Definições, escopo e decisões: `docs/DEFINICOES.md`
- Visão de arquitetura: `docs/ARQUITETURA.md`
- Decisões (ADRs): `docs/decisoes/`
- Regras detalhadas: `.claude/rules/` (arquitetura, código, testes, pagamentos)

## Regras absolutas

1. **Nunca executar `git commit`, `git push`** nem nada que publique no remoto (merge, tag, PR). Comandos git de leitura (`status`, `diff`, `log`) são permitidos. Ao terminar, liste os arquivos alterados e sugira uma mensagem de commit. O autor faz o commit.
2. **Modo aprendizado.** O autor quer entender, não receber pronto:
   - Explique o **porquê** de cada escolha, não só o quê.
   - As peças centrais (`Order`, o caso de uso `ConfirmPayment`, o handler do webhook, a reserva de estoque) são **escritas pelo autor**. Proponha a abordagem, revise e aponte os problemas, mas não escreva a implementação completa sem um pedido explícito.
   - Trabalhe em passos pequenos, que o autor consiga acompanhar.
3. **Escopo.** Nada da lista "Fora de escopo" (`docs/DEFINICOES.md` §3) entra sem uma decisão explícita. Mudança de decisão → novo ADR em `docs/decisoes/`.
4. **Custo zero.** Nenhum recurso pago, nenhum serviço novo sem verificar o plano gratuito.
5. **Fornecedores.** Afirmações sobre Stripe, Oracle, GitHub e afins devem ser verificadas na documentação oficial, e não tiradas da memória. Na dúvida, diga que não confirmou.

## Stack

.NET 10 · ASP.NET Core MVC + Razor · EF Core + PostgreSQL · ASP.NET Core Identity (cookies) · Stripe Checkout hospedado · xUnit + Testcontainers · Docker Compose · GitHub Actions · Oracle Cloud Always Free (ARM64)

## Comandos do projeto

- `/retomar`: início de sessão. Lê o estado atual e propõe o próximo passo.
- `/encerrar`: fim de sessão. Atualiza `docs/STATUS.md` e lista o que o autor precisa commitar.
- Subagente `revisor`: revisão crítica das mudanças contra as regras e as definições.

## Estado atual

@docs/STATUS.md
