# 0019. Código em inglês; documentação e interface em português

- **Status:** Aceito
- **Data:** 2026-09-29
- **Relacionado:** `docs/DEFINICOES.md` D36, §8 (glossário); `.claude/rules/codigo.md`

## Contexto

A definição do projeto foi feita em português, e os termos do domínio nasceram em português (`Pedido`, `ConfirmarPagamento`). O projeto é um portfólio para empresas médias e grandes, e o autor tem inglês profissional.

## Decisão

- **Identificadores de código em inglês** (classes, métodos, variáveis, rotas, tabelas): `Order`, `ConfirmPayment`, `ShippingAddress`.
- **Documentação, ADRs e textos da interface em português (pt-BR).**
- A correspondência entre os termos fica num **glossário único** (`docs/DEFINICOES.md` §8). Sinônimos não são permitidos.
- Solution e namespace raiz: **`KeycapStore`**. Plataforma: **.NET 10**.

## Alternativas consideradas

- **Identificadores em português:** alinhados com a documentação, mas misturados com o framework em inglês (`PedidoController : Controller`), e menos legíveis para avaliadores de fora.

## Consequências

- **Positivas:** o código fica consistente com o ecossistema .NET e legível por qualquer avaliador.
- **Negativas:** existe uma tradução entre a documentação e o código, e o glossário precisa ser mantido.
- **Como saber se deu errado:** termos divergentes para o mesmo conceito (ex.: `Order` e `Purchase`).
