# 0046. Ilustração dos kits pelo tipo do kit, nas três cores de um conjunto de keycaps

- **Status:** Aceito
- **Data:** 2026-11-05
- **Relacionado:** ADR 0045 (V3: ilustrações SVG com a paleta escolhida pelo admin)

## Contexto

A primeira ilustração do ADR 0045 desenhava o mesmo teclado para todos os kits, só trocando as cores; a segunda, só três teclas grandes. O autor não gostou de nenhuma das duas: o teclado inteiro estava poluído e todos os cartões pareciam iguais, e ele preferiu o teclado grande, com kits diferentes mostrando teclas diferentes.

Referências pesquisadas (páginas de lojas e fóruns de keycaps, como CannonKeys, Geon, Drop e Deskthority): um conjunto é vendido em **kits** com conteúdo diferente: o **base** (letras e modificadores, mostrado montado num teclado), as **novelties** (teclas com ícones no lugar da letra, quase sempre vendidas à parte), os **modificadores** (Ctrl, Shift, Alt, Tab...) e a fileira de **funções** (F1–F12). Uma "colorway" tem três papéis de cor: as **letras**, os **modificadores** em cor contrastante (muitas vezes a inversa das letras) e os **destaques** (Esc, Enter). As lojas mostram o teclado montado e, para cada kit, as teclas do kit soltas, lado a lado.

## Decisão

1. **O produto ganha o tipo do kit** (`KitKind`: Base, Novelties, Modificadores, Teclas de função), escolhido pelo admin junto com as cores ("Visual do kit"). Novo produto nasce como Base; os 6 kits do seed recebem o tipo certo (migration `AddProductKitKind`, padrão `Base`).
2. **Cada tipo tem o seu desenho:** Base é um teclado grande (letras, modificadores, Esc e Enter); Novelties são teclas grandes soltas com ícones (estrela, coração, lua, raio, onda, sol, retorno); Modificadores são teclas soltas de Esc, Tab, Caps, Backspace, Shift, Ctrl, Win, Enter, Alt, Fn, Menu; Teclas de função são Esc e F1–F12 (em grupos de cores alternadas) mais PrtSc, ScrLk, Pause, Home e End.
3. **As três cores seguem os papéis de um conjunto real:** letras na cor das teclas com a legenda na cor das legendas; modificadores com as duas cores **invertidas**; destaques (Esc, Enter e algumas novelties) na cor de destaque, com a legenda preta ou branca, a que tiver mais contraste com o destaque (calculado na hora, vale para qualquer cor do admin).
4. **Cores novas do seed** (migration `UpdateKitPalettes`), mais vivas e diferentes entre as linhas: Aurora (índigo, ciano-claro, verde), Maré (petróleo, branco-água, areia), Zéfiro (creme, marrom, vermelho); o kit complementar de cada linha usa as cores invertidas.
5. A montagem das teclas fica numa classe testável (`KitIllustration`), e a view só desenha o que ela calcula; o fundo do desenho acompanha o tema (variáveis `--surface` e `--line`).

## Alternativas consideradas

- **Tirar o tipo do kit do nome do produto** ("novelties", "modificadores"...): sem coluna nova, mas frágil (depende de como o admin escreve) e não funciona em inglês.
- **Fotos ou renders reais de outras marcas:** enganoso e com licença a conferir (ADR 0045).
- **Um desenho só para todos os kits:** foi o que o autor recusou.

## Consequências

- **Positivas:** cada kit tem cara própria e parecida com a de uma loja de keycaps de verdade; o admin controla tipo e cores sem mexer em código; o desenho é testado (fileiras fechando na mesma borda, sem teclas sobrepostas, teclas certas em cada tipo).
- **Negativas / custos:** mais uma coluna e duas migrations; os desenhos continuam ilustrações, não fotos; tipos novos (barra de espaço, numpad) exigem código novo.
- **Como saber se deu errado:** um cartão com teclas cortadas ou sobrepostas; legenda ilegível sobre alguma cor; o admin precisando de um tipo de kit que não existe.
