# 0055. Content Security Policy: scripts só da loja e da Stripe, estilos inline liberados

- **Status:** Aceito
- **Data:** 2026-11-11
- **Relacionado:** ADR 0044 (auditoria de segurança), ADR 0040 (Payment Element), ADR 0039 (login com Google), ADR 0053 (limite de tentativas)

## Contexto

Reforço da auditoria de 04/11 escolhido para a V4: a loja não mandava CSP, `X-Frame-Options`/`frame-ancestors` nem `X-Content-Type-Options`. O CSP diz ao navegador de onde a página pode carregar scripts, estilos, imagens e frames. Se alguém conseguir injetar um `<script>` numa página, o navegador não roda.

Levantamento do código em 11/11:
- **1 `<script>` escrito dentro da página:** o tema no `<head>` do layout;
- **4 `onsubmit="return confirm(...)"`:** excluir conta, cancelar pagamento, cancelar pedido, desistir;
- **19 `style="..."`:** as cores das teclas;
- nenhum `eval`.

Documentação da Stripe conferida (11/11, *Integration security guide*): para Stripe.js/Payment Element,
- `script-src` `https://js.stripe.com` e `https://*.js.stripe.com`;
- `frame-src` esses dois e `https://hooks.stripe.com` (3D Secure);
- `connect-src` `https://api.stripe.com`;
- para o Link, `link.com` e `*.link.com` em `frame-src`, `connect-src` e `img-src`.

## Decisão

1. **Cabeçalho em toda resposta** (`Security/SecurityHeaders.cs`, antes de qualquer outro middleware):
   ```
   default-src 'self'
   script-src 'self' https://js.stripe.com https://*.js.stripe.com
   style-src 'self' 'unsafe-inline'
   img-src 'self' data: https://*.stripe.com https://*.link.com
   font-src 'self'
   connect-src 'self' https://api.stripe.com https://link.com https://*.link.com
   frame-src https://js.stripe.com https://*.js.stripe.com https://hooks.stripe.com https://link.com https://*.link.com
   form-action 'self' https://accounts.google.com
   frame-ancestors 'none'
   base-uri 'self'
   object-src 'none'
   ```
   Junto: `X-Content-Type-Options: nosniff` e `Referrer-Policy: strict-origin-when-cross-origin`. As páginas com link secreto continuam com `no-referrer` (ADR 0050).
2. **Scripts sem nenhuma exceção:**
   - o tema foi para `js/theme-init.js`, carregado no `<head>` sem `defer` para não "piscar" claro no modo escuro;
   - as confirmações viraram `data-confirm="..."`, com um único ouvinte no `site.js`.
3. **Estilos inline liberados** (`'unsafe-inline'` só em `style-src`, decisão do autor): estilo não executa código, e as cores das teclas continuam aparecendo mesmo sem JavaScript.
4. **`form-action`** inclui o Google: o "Entrar com o Google" é um formulário que redireciona para `accounts.google.com`, e o navegador aplica o `form-action` também ao redirecionamento.
5. **Um teste de guarda** lê todos os `.cshtml` e falha se aparecer `<script>` sem `src` ou um `on...=`. Assim, uma página nova não quebra o CSP sem ninguém ver.
6. **Ligado valendo** (decisão do autor), sem o modo só de observação, depois de testar todas as telas no PC, inclusive um pagamento com 3D Secure.

## Alternativas consideradas

- **Tirar também os estilos inline** (cores em `data-*` aplicadas por script): CSP sem exceção, mas sem JavaScript as teclas ficariam sem cor.
- **Nonce por requisição para scripts inline:** manteria os scripts nas páginas, mas espalharia o nonce pelas views; tirar os scripts é mais simples e o teste de guarda impede a volta.
- **Report-Only primeiro:** mais seguro, mas dois deploys e leitura do console; o autor preferiu testar no PC e ligar valendo.

## Consequências

- **Positivas:** um script injetado não roda; a loja não pode ser embutida em frame de outro site (*clickjacking*); o navegador não "adivinha" tipo de arquivo.
- **Negativas / custos:** cada serviço externo novo (análise de acesso, outro pagamento) exige mexer no CSP; no PC, a recarga automática do Visual Studio pode ser bloqueada (só no desenvolvimento).
- **Como saber se deu errado:** erro "Refused to..." no console do navegador; o formulário da Stripe ou o 3D Secure não aparecem; o "Entrar com o Google" não sai da loja.
