# 0053. Limite de tentativas por IP no login, no cadastro e no "esqueci minha senha"

- **Status:** Aceito
- **Data:** 2026-11-11
- **Relacionado:** ADR 0044 (correções da auditoria; limite de cotações), ADR 0050 (esqueci minha senha), decisões de 06/10 (bloqueio de 3 tentativas por conta)

## Contexto

Reforço da auditoria de 04/11 escolhido para a V4. Hoje a conta é bloqueada depois de 3 senhas erradas, mas nada impede um robô de testar **uma** senha em **muitas** contas a partir do mesmo endereço, de criar contas em massa ou de pedir muitos links de troca de senha.

Conferido na documentação da Microsoft (11/11):
- o middleware de limite já vem no ASP.NET Core (`AddRateLimiter`, `UseRateLimiter` depois do `UseRouting`, `[EnableRateLimiting]` por ação);
- a recusa padrão é **503**, e a 429 se configura em `RejectionStatusCode`;
- o `UseStatusCodePagesWithReExecute` precisa vir **antes** do `UseRouting` (no projeto, ele já vem);
- os contadores ficam na memória de cada processo;
- partições por IP são sujeitas a IP falsificado.

Com `ASPNETCORE_FORWARDEDHEADERS_ENABLED`, o ASP.NET lê o IP do `X-Forwarded-For` processando da direita para a esquerda, com `ForwardLimit` padrão de 1, ou seja, só o último valor. O Nginx da VM usa `$proxy_add_x_forwarded_for`, que **acrescenta** o IP real ao final. Um valor inventado pelo visitante fica antes dele e é ignorado. A loja só escuta em `127.0.0.1`, então só o Nginx chega nela.

## Decisão

1. **Limite por IP**, janela fixa, sem fila, só no `POST` de cada formulário (decisão do autor: "moderado"):
   - **entrar:** 10 por minuto;
   - **criar conta:** 5 por hora;
   - **esqueci minha senha:** 5 a cada 15 minutos.
2. **Os limites são separados** por formulário: estourar o do login não bloqueia o "esqueci minha senha".
3. **Quem passa do limite** recebe **429** com a página de erro da loja ("Muitas requisições") e o cabeçalho `Retry-After`. O bloqueio de 3 senhas erradas por conta continua valendo junto.
4. **Os números ficam em configuração** (`AccountLimits`), validados na subida. Os testes de integração sobem os limites para 1000, para não esbarrar neles, e os testes do limite usam números baixos de propósito.

## Alternativas consideradas

- **Números rígidos** (5/min, 3/h, 3/h): uma família no mesmo Wi-Fi poderia esbarrar no limite.
- **Números folgados** (30/min, 20/h, 10/15 min): freiam pouco um robô.
- **Limite no Nginx** (`limit_req`): funcionaria, mas fica fora do código e dos testes; o do ASP.NET é testado junto com o resto.
- **Limite global para o site inteiro:** atrapalharia a navegação comum e não é o problema da auditoria.

## Consequências

- **Positivas:** testar senhas em muitas contas, criar contas em massa e encher a caixa de alguém com links de troca de senha ficam lentos; a mesma página de erro da loja.
- **Negativas / custos:** quem divide o IP (uma rede de empresa ou de faculdade) divide o limite; os contadores somem num reinício (aceitável); com mais de um processo, o limite vale por processo.
- **Como saber se deu errado:** 429 para clientes comuns no log; muitas tentativas de login vindas do mesmo IP sem 429; o IP registrado sendo o do Nginx (`127.0.0.1`) para todo mundo, o que juntaria todos num limite só.
