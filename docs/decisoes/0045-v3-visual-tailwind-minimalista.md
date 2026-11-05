# 0045. V3: reformulação visual com Tailwind CSS, estilo minimalista técnico e ilustrações dos kits

- **Status:** Aceito
- **Data:** 2026-11-05
- **Relacionado:** ADR 0025 (roadmap: a V3 é a reformulação visual em Razor), `docs/DEFINICOES.md` D18, ADR 0044 (CSP e cabeçalhos ficaram como notas de reforço)

## Contexto

A loja ainda usa o modelo do Visual Studio: Bootstrap 5.3.3 sem tema, a página inicial e a de privacidade do modelo, nenhuma imagem dos kits. O ADR 0025 decidiu que a V3 muda só a aparência, mantendo ASP.NET Core MVC + Razor e todas as regras do backend.

As escolhas foram feitas com a ajuda da skill **ui-ux-pro-max** (plugin gratuito, licença MIT, roda no PC com Python, sem acesso à rede), que sugere estilos, paletas e fontes a partir de uma base de dados. As sugestões foram conferidas antes de serem aceitas (duas delas vinham com fontes ou efeitos que não serviam a uma loja de keycaps).

Restrições: custo zero; nada de serviço novo; a loja roda numa VM pequena; os testes de página e o antiforgery não podem quebrar; tudo precisa funcionar em português e em inglês, no claro e no escuro.

## Decisão

1. **CSS com Tailwind CSS v4 (decisão do autor), instalado pelo npm.** Documentação oficial conferida (v4.3): pacotes `tailwindcss` e `@tailwindcss/cli`; o CSS de entrada tem `@import "tailwindcss";`; o programa lê os arquivos do projeto como texto (as views `.cshtml` funcionam) e ignora o que está no `.gitignore`.
   - **No PC:** `npm run css:watch` gera o CSS enquanto as views mudam.
   - **No Dockerfile:** uma etapa com Node gera o CSS final antes do `dotnet publish`. O arquivo gerado **não** vai para o git.
   - **Versões travadas** pelo `package-lock.json` (com a conferência de integridade dos pacotes) e atualizadas pelo Dependabot (ecossistema `npm`), como o NuGet.
   - **O Bootstrap sai** (CSS e JavaScript). A validação dos formulários continua com o jQuery Validation, que não depende do Bootstrap.
   - **Regra para as views:** toda classe do Tailwind aparece **inteira** no código (`@(pago ? "text-green-700" : "text-red-700")`); nunca montada em pedaços, porque o Tailwind não a encontra e ela some do CSS.
2. **Estilo minimalista técnico (decisão do autor, direção A):** estilo "Minimalism & Swiss" da skill (risco de acessibilidade baixo, custo baixo): muito espaço, grade, bordas finas, quase sem sombra, **uma cor de destaque só**. O colorido fica por conta das ilustrações dos kits.
   - **Cores** (neutros do Tailwind + laranja), com o contraste medido (mínimo 4,5:1):
     - claro: fundo branco, texto `#0A0A0A`, texto secundário `#52525B` (7,7:1), botão laranja `#C2410C` com texto branco (5,2:1);
     - escuro: fundo `#0A0A0A`, texto `#FAFAFA`, texto secundário `#A1A1AA` (7,7:1), botão laranja `#F97316` com texto preto (7,1:1).
     - O laranja sugerido (`#EA580C`) foi recusado: com texto branco dá só 3,6:1.
   - **Fontes:** Space Grotesk (títulos) e DM Sans (texto), par "Tech Startup" da skill. Licença **SIL Open Font License** (conferida no repositório oficial do Google Fonts), que permite guardá-las no projeto com o `OFL.txt`. Ficam **na própria loja** (`wwwroot`), não carregadas do Google: o navegador do cliente não manda o IP dele a um terceiro a cada visita (LGPD) e a loja não depende de um serviço de fora.
3. **Claro e escuro, seguindo o sistema, com botão para trocar (decisão do autor).** Pela documentação do Tailwind: a variante `dark` passa a depender de uma classe no `<html>` (`@custom-variant`), e um script pequeno no `<head>` aplica a escolha guardada no navegador (claro, escuro ou "como o sistema") antes de a página aparecer, para não piscar.
4. **Ilustrações SVG dos kits (decisão do autor):** cada kit é desenhado em SVG (um conjunto de teclas) a partir da **paleta do kit**, sem foto. Custo zero, sem licença de imagem, leve, nítido em qualquer tamanho e certo no claro e no escuro.
5. **A paleta é escolhida pelo admin (decisão do autor):** o produto ganha **3 cores** (base das teclas, legendas e tecla de destaque), em formato `#RRGGBB` validado no domínio, editadas no cadastro de produtos com um seletor de cor. Migration nova; os 6 kits do seed e os produtos já existentes recebem paletas prontas (os criados pelo admin, uma neutra, que ele pode mudar).
6. **O que não muda:** controllers, rotas, regras, textos (e traduções), nomes dos campos dos formulários e o antiforgery. Os testes de página, que procuram textos e campos e não classes de CSS, devem continuar passando sem mudança.
7. **Ordem:** (1) esteira do Tailwind + layout novo sem Bootstrap + fontes; (2) tema claro/escuro; (3) paleta dos kits + ilustração SVG; (4) páginas, começando pelo catálogo (a vitrine), depois carrinho, endereço e pagamento, pedidos, conta, e por fim as páginas do admin. Cada passo vai para produção e é conferido antes do próximo. As páginas `Home` e `Privacy` do modelo são substituídas por conteúdo da loja.

## Alternativas consideradas

- **Bootstrap com tema próprio** (recomendação inicial do Claude): sem etapa de compilação nova e com menos mudança no deploy, mas o autor preferiu o Tailwind, pelo visual mais livre e por ser o padrão atual do mercado.
- **CSS próprio, sem framework:** controle total, mas grade, formulários e responsividade feitos à mão; caminho mais longo.
- **Executável avulso do Tailwind (sem Node):** dispensa o Node, mas a versão teria de ser baixada e conferida à mão no Dockerfile e no CI, sem lockfile nem Dependabot.
- **Estilos "Vibrante em blocos" e "Retrô de teclado":** o primeiro tem risco de contraste nos blocos coloridos; o segundo tem o modo escuro marcado como "condicional" pela skill.
- **Fotos de bancos gratuitos:** mostrariam keycaps reais de outras marcas como se fossem os kits da loja (enganoso) e exigiriam conferir a licença de cada foto. **Foto enviada pelo admin:** realista, mas pede armazenamento, backup das imagens e validação de arquivos; fica para depois, se houver fotos.
- **Fontes carregadas do Google Fonts:** mais simples, mas envia o IP de cada visitante ao Google e cria uma dependência externa.

## Consequências

- **Positivas:** a loja ganha identidade sem mudar nenhuma regra; acessibilidade medida (contraste) desde o início; claro e escuro; kits com cara própria sem custo de imagem; o Tailwind e as fontes ficam com versão travada e sem serviço externo.
- **Negativas / custos:** Node passa a fazer parte do desenvolvimento e do Dockerfile (uma etapa e uma dependência a mais, atualizadas pelo Dependabot); todas as views são reescritas; quem esquecer o `npm run css:watch` no PC vê a página sem estilo; uma migration nova (paleta); as ilustrações não são fotos.
- **Como saber se deu errado:** um teste de página quebrando por causa de visual; classe do Tailwind que some no build (nome montado em pedaços); contraste abaixo de 4,5:1 em algum texto; a página piscando no tema errado ao carregar; o build da imagem Docker ficando muito mais lento ou maior; a reformulação travada por limitação do Razor (sinal, segundo o ADR 0025, para reconsiderar a SPA).
