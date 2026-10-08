# 0002. Escopo da v1: keycaps em lotes limitados, fatia vertical e roadmap incremental

- **Status:** Aceito
- **Data:** 2026-09-29
- **Relacionado:** `docs/DEFINICOES.md` D03–D06, D17, D34; §2 e §3

## Contexto

"Evoluir gradualmente" só funciona se existir um primeiro incremento **completo**. Sem ele, o projeto vira uma coleção de camadas pela metade. O produto também precisa exercitar o que o projeto quer demonstrar (estoque limitado e concorrência), sem trazer complexidade que não ensina nada (variações de produto, frete).

## Decisão

- **Produto:** kits de keycaps em lotes limitados, com marcas fictícias. **Cada kit é um produto separado, sem variações**, e só há venda com pronta entrega (sem pré-venda).
- **v1 = fatia vertical:** catálogo público → carrinho (no banco, por cliente, com login) → finalizar (endereço + reserva) → Stripe → webhook → envio pelo admin.
- **Conta obrigatória para comprar**; o catálogo é público.
- **Produtos via seed**; ~~frete fixo~~ frete por estado (**substituído pelo ADR 0024**); endereço como snapshot no pedido.
- **Roadmap:** v2 = cancelamento, arrependimento e exclusão de conta (bloqueios para produção); v3 = Payment Element; v4 = Web API + SPA; depois, admin, conciliação, frete, pré-venda. Cada item é independente.

## Alternativas consideradas

- **Café, prints, livros:** viáveis. As keycaps foram escolhidas por preferência do autor e porque dialogam com o público técnico.
- **Produto digital:** descartaria o design de estoque e concorrência, que são as partes mais fortes do projeto.
- **Produtos com variações (SKU):** complexidade sem ganho de aprendizado.
- **Pré-venda (group buy):** muda o modelo de estoque inteiro.
- **Cálculo de frete real:** integração trabalhosa e fora do objetivo.
- **Carrinho anônimo em cookie:** incoerente com "conta obrigatória para comprar", e duplicaria a lógica.

## Consequências

- **Positivas:** a v1 é demonstrável de ponta a ponta; a escassez ("últimas unidades") torna o controle de estoque visível na demonstração.
- **Negativas:** a v1 não pode ir para produção sem a v2 (cancelamento e exclusão são obrigações legais).
- **Como saber se deu errado:** se algo da lista "fora de escopo" começar a ser implementado sem um novo ADR.
