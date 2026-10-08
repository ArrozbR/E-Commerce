# 0025. Roadmap: v2 com tudo o que não é visual (parte legal primeiro), v3 é a reformulação visual em Razor, sem v4

- **Status:** Aceito
- **Data:** 2026-10-08
- **Relacionado:** `docs/DEFINICOES.md` §3, §9 (roadmap), §11, D05, D07, D17, D18

## Contexto

O roadmap original, definido em 29/09, era:
- v2: direitos do consumidor e do titular;
- v3: Payment Element;
- v4: Web API + SPA (React ou Angular);
- "futuro" sem versão: o resto.

Ao fim do M2, o autor revisou o que vem depois da v1, por dois motivos:
- **A aparência do site incomoda.** É o modelo padrão do Visual Studio, porque até aqui o foco foi o backend.
- **"Futuro, sem versão" não comunica nada.** O autor quer saber o que entra e em que ordem.

Ao discutir a v4, ficou claro que React ou Angular **não** deixam o site mais bonito. Eles mudam a arquitetura: o .NET passa a responder só JSON, e as telas são montadas por um segundo projeto no navegador. A aparência vem do design (CSS, layout, identidade da loja), que pode ser feito no Razor atual.

## Decisão

O projeto passa a ter **três versões**. **Não existe v4.**

1. **v1:** como está definida (M0 a M6).
2. **v2: tudo o que não é visual, com a parte legal primeiro.**
   1. **Primeiro (libera o go-live):** cancelamento, direito de arrependimento (CDC, 7 dias) e exclusão de conta (LGPD), como já definido em D17 e no §6.
   2. **Depois:**
      - site em **inglês**, com troca de idioma pt-BR/en por uma **bandeira no menu** (layout compartilhado);
      - **tela de admin com cadastro de produtos** (substitui o "só seed" do D05 a partir da v2);
      - **formulário de pagamento embutido** (Payment Element), no lugar do Checkout hospedado;
      - **conciliação** com a Stripe;
      - **frete real** (Correios ou Melhor Envio), substituindo a regra por estado do ADR 0024;
      - **pré-venda** (*group buy*);
      - **endereços salvos** na conta;
      - **login com Google**.
3. **v3: reformulação visual, mantendo ASP.NET Core MVC + Razor.** Design, CSS, layout, identidade da loja e fotos dos kits. Não troca de tecnologia.

**Continuam fora, de propósito:**
- variações de produto (SKU);
- boleto;
- compra sem conta (como convidado).

Os motivos de cada um não mudaram e estão no §3 do `DEFINICOES.md`.

## Alternativas consideradas

- **Manter o roadmap original (v2 legal, v3 Payment Element, v4 SPA):** deixava a melhoria visual para o fim de um caminho longo, e atrelada a uma reescrita (SPA) que o autor não precisa para o objetivo dele.
- **v3 em React ou Angular:** exigiria transformar o `Web` numa API JSON, criar um segundo projeto em TypeScript, refazer todas as telas, a autenticação entre os dois programas e os testes de página. O ganho visual viria do design, que também pode ser feito no Razor. Fica registrado que, se o objetivo passar a ser aprender frontend para o mercado, a decisão pode ser reaberta (os controllers finos continuam permitindo isso sem reescrever regras).
- **Dividir em v2 (legal) e v2.1 (resto):** o autor preferiu uma v2 só, grande, mas **com a parte legal feita primeiro**. Na prática, o go-live depende só dessa primeira parte.
- **Levar SKU, boleto e compra sem conta para a v2:** rejeitado pelo autor. SKU mexe na base do estoque, do carrinho, do pedido e da reserva; boleto prende estoque por dias numa loja de lote limitado; compra sem conta quebra a regra "só o dono vê o pedido" e exigiria envio de e-mail.

## Consequências

- **Positivas:**
  - o roadmap fica com uma ordem clara;
  - a melhoria visual não depende de reescrita;
  - o foco continua no .NET;
  - o go-live só espera a parte legal da v2.
- **Negativas / custos:**
  - a v2 fica **grande**, com itens de peso diferente (a pré-venda muda o modelo de estoque);
  - cada item da v2 que usa um serviço externo precisa ter o **plano gratuito verificado** antes (Correios/Melhor Envio, login do Google), pela regra de custo zero;
  - o frete real e o Payment Element **substituem** decisões já tomadas (ADR 0024 e D07): cada um terá seu próprio ADR quando for implementado;
  - a tradução exige tirar os textos fixos das views e das mensagens (validação, Identity, domínio) para arquivos de recursos.
- **Como saber se deu errado:**
  - a v2 se arrastar sem entregar a parte legal primeiro;
  - a reformulação visual ficar travada por limitações do Razor, e não por falta de design (sinal para reconsiderar a SPA).
