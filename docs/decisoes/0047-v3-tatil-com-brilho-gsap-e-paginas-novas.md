# 0047. V3 "juicy": estilo tátil com brilho, animações com GSAP e as páginas novas da loja

- **Status:** Aceito
- **Data:** 2026-11-05
- **Relacionado:** ADR 0045 (V3: Tailwind e estilo minimalista técnico), ADR 0046 (ilustração pelo tipo do kit), ADR 0040 (Payment Element)

## Contexto

Com o passo 1 da V3 no ar, o autor quis um site "mais juicy", que se diferencie do modelo padrão e seja moderno em todos os sentidos. A skill ui-ux-pro-max, com variação e movimento altos, sugeriu interface tátil (botões e cartões com profundidade, que afundam ao clicar) e cartões entrando em sequência; o autor pediu que fosse **tátil e tech com brilho ao mesmo tempo**, com **movimento alto (GSAP)**, e que a rodada incluísse home com vitrine, página de cada kit, carrinho no menu e carrinho/checkout modernos.

Conferido na fonte oficial: o GSAP está sob a licença "Standard No Charge" da Webflow desde 30/04/2025, **gratuito também para uso comercial e com todos os plugins** (ScrollTrigger, SplitText...); as restrições (não criar ferramenta visual de animação que concorra com a Webflow, não remover os avisos) não se aplicam à loja. A Stripe permite mudar a aparência do Payment Element com o objeto `appearance` em `initCheckoutElementsSdk({ elementsOptions: { appearance } })`; as cores não aceitam variáveis CSS.

## Decisão

1. **Estilo tátil com brilho**, por cima das cores do ADR 0045 (que continuam): botões e cartões com profundidade de tecla (uma "lateral" embaixo) que **afundam** ao clicar; **brilho laranja** no hover e no foco; cabeçalho fixo, translúcido e com desfoque; um brilho suave de fundo no topo da página. O contraste mínimo de 4,5:1 continua valendo.
2. **GSAP** (versão fixa no `package.json`, atualizada pelo Dependabot) para as animações: cartões entrando em sequência, o aviso de "adicionado", o teclado "digitando" na home e animações ao rolar a página (ScrollTrigger). Os arquivos vêm do pacote npm e são copiados para `wwwroot/lib/gsap` na geração dos arquivos (no PC e no Dockerfile); não vão para o git.
3. **Menos movimento é respeitado:** quem pede `prefers-reduced-motion` no sistema vê tudo no estado final, sem animação. Se o GSAP não carregar, o conteúdo aparece mesmo assim (nada depende da animação para ficar visível).
4. **Carrinho no menu:** contador de itens no link do carrinho; ao adicionar um kit, a loja volta para a página de onde veio (só endereços da própria loja) com o aviso "adicionado ao carrinho". Em erro (pré-venda misturada, limite, estoque), continua indo para o carrinho com a mensagem.
5. **Páginas novas, em PRs separados, nesta ordem:** (1) base tátil + GSAP + carrinho no menu; (2) página de cada kit; (3) home com vitrine; (4) carrinho e checkout com resumo ao lado, etapas e o formulário da Stripe nas cores da loja.

## Alternativas consideradas

- **Só CSS (sem GSAP):** sem dependência nova, mas sem as animações de rolagem e a sequência do teclado que o autor pediu.
- **Só tátil ou só brilho:** o autor quis os dois.
- **Manter o fluxo antigo do "Adicionar"** (sempre para a página do carrinho): mais simples, mas tira a pessoa do catálogo a cada kit.

## Consequências

- **Positivas:** loja com identidade própria e interação mais rica; GSAP gratuito e sem serviço externo; acessibilidade de movimento garantida.
- **Negativas / custos:** uma dependência a mais (GSAP) e um passo de cópia na geração dos arquivos; mais JavaScript para manter; uma consulta a mais ao banco em cada página para o contador do carrinho (cliente logado).
- **Como saber se deu errado:** conteúdo invisível se o JavaScript falhar; animação para quem pediu menos movimento; o site ficando lento no celular; o aviso de "adicionado" levando para fora da loja.
