# 0048. Adicionar ao carrinho sem recarregar, teclas clicáveis e o endereço da página do kit

- **Status:** Aceito
- **Data:** 2026-11-05
- **Relacionado:** ADR 0047 (V3 "juicy"; muda o item 4 e detalha o item 5, passo 2), ADR 0046 (ilustração pelo tipo do kit)

## Contexto

No teste do passo 1 do ADR 0047, o autor notou que adicionar um kit **pisca a tela**: o formulário vai ao servidor e a página inteira é carregada de novo, mesmo voltando para o catálogo. Pediu também para poder **clicar nas teclas** do desenho do catálogo e, depois, da página do kit. O passo 2 do ADR 0047 (página de cada kit) precisava de um endereço e de um conteúdo; o produto não tem descrição nem um nome curto para o endereço guardados no banco.

## Decisão

1. **"Adicionar" sem recarregar, com melhoria progressiva:** o JavaScript (`cart.js`) intercepta o envio do formulário e manda o **mesmo** `POST /Cart/Add` com `fetch` e `Accept: application/json` (com o token antiforgery do próprio formulário). O servidor responde `200 { count }` ou `409 { message }` (pré-venda misturada, limite, estoque); a página atualiza o contador, mostra o aviso (sucesso ou erro) e uma tecla "voa" até o carrinho. Sem JavaScript, o formulário continua funcionando como no ADR 0047 (volta para a página com o aviso, ou vai ao carrinho com a mensagem). Se a sessão caiu, a página segue para o login.
2. **Teclas clicáveis:** cada tecla do desenho tem uma "parte de cima" que afunda ao clicar ou tocar. Na página do kit, as teclas também afundam quando a pessoa **digita no próprio teclado** (`data-key` com o nome da tecla; nada acontece dentro de campos de formulário e nenhum atalho do navegador é bloqueado). O desenho continua decorativo para leitores de tela.
3. **Endereço da página do kit:** `/Catalog/{id}/{slug}`. O `id` é quem manda; o `slug` vem do nome na hora (sem acento, minúsculo, com hífens) e serve só para o endereço ser legível. Endereço sem o slug ou com um slug antigo (nome mudou) → **301** para o endereço certo; a página tem `link rel="canonical"`. Nenhuma coluna nova no banco.
4. **Conteúdo da página do kit:** desenho grande, tipo do kit, preço, disponibilidade (os mesmos textos do catálogo, num pedaço de página só), botão de adicionar, "O que vem no kit" (um texto fixo **por tipo**, que descreve as teclas do desenho), as três cores do conjunto e "Veja também" (até 3 kits, primeiro os do **mesmo conjunto de cores**). Nenhuma informação inventada (material, perfil das teclas).

## Alternativas consideradas

- **Recarregar a página com transição** (View Transitions do navegador): esconde o piscar só em parte dos navegadores e continua carregando a página inteira.
- **Endereço só com o nome** (`/Catalog/kit-aurora-base`): mais bonito, mas exige uma coluna nova, única e editável no admin, e quebra os links quando o nome muda.
- **Descrição escrita pelo admin para cada kit:** mais rica, mas é uma coluna nova + tela no admin; fica para depois, se fizer falta.

## Consequências

- **Positivas:** adicionar ao carrinho fica instantâneo e com resposta visual; o mesmo endpoint serve os dois jeitos (com e sem JavaScript); a página do kit sai sem migration.
- **Negativas / custos:** mais JavaScript para manter (`cart.js`, teclas e digitação no `motion.js`); a página do kit lê o catálogo inteiro para achar o kit e os relacionados (o catálogo é pequeno; trocar por uma consulta própria se crescer); as descrições por tipo precisam de tradução quando um tipo novo aparecer.
- **Como saber se deu errado:** item adicionado duas vezes num clique; aviso que não aparece ou contador que não muda; a página do kit em 404 para um kit à venda; uma tecla do teclado físico (ou um atalho) parando de funcionar na página do kit.
