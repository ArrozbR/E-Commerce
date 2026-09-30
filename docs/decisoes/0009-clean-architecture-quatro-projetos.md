# 0009. Clean Architecture em 4 projetos, com módulos por pastas

- **Status:** Aceito
- **Data:** 2026-09-29
- **Relacionado:** `docs/DEFINICOES.md` D22, §6; `.claude/rules/arquitetura.md`

## Contexto

O projeto anterior do autor (agenda-pessoal) é um único projeto organizado por pastas, com modelo anêmico: nada impede um controller de acessar o `DbContext` diretamente. Aqui precisamos de um domínio rico, de controllers finos, e de uma SPA futura que não exija reescrever regra de negócio. Tudo isso para uma pessoa sozinha.

## Decisão

- 4 projetos: **`Domain` ← `Application` ← `Infrastructure` / `Web`**, com as dependências apontando sempre para dentro. O `Domain` não tem pacotes NuGet.
- A `Application` define interfaces (portas); a `Infrastructure` as implementa (inversão de dependência).
- Os módulos (`Catalog`, `Cart`, `Orders`, `Payments`, `Identity`) são **pastas** dentro de cada camada, com as fronteiras verificadas por **testes de arquitetura**.

**Por que o domínio fica no centro:** as regras de negócio não podem mudar quando a tecnologia muda (e ela já mudou uma vez nesta definição: SQL Server → PostgreSQL), e precisam ser testáveis sem banco, sem rede e sem Stripe.

## Alternativas consideradas

- **Projeto único com pastas:** as fronteiras dependem só da disciplina, e o resultado seria "igual à agenda" para quem avalia.
- **Monolito modular "puro" (camadas por módulo):** 15+ projetos para uma pessoa, e problemas de comunicação entre módulos que a v1 não tem.

## Consequências

- **Positivas:** as fronteiras de camada são garantidas pelo compilador; o domínio é testável isoladamente; a SPA da v4 é só mais uma camada Web.
- **Negativas:** mais cerimônia (interfaces, mapeamentos). As fronteiras entre módulos dependem de testes, e não do compilador.
- **Como saber se deu errado:** a SPA exigir reescrever regra de negócio; os testes de arquitetura começarem a ser ignorados.
