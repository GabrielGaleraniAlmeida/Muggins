# Muggins

Motor de decisão em C# para o projeto de Dominó Ponta de Quina. O cliente consulta a API oficial para obter o estado da partida; o motor escolhe uma ação local e envia a decisão à API.

## Estrutura

- `Muggins.Engine/`: regras, estratégia do bot, modelos e integração HTTP com a API.
- `Muggins.Engine.Tests/`: testes permanentes do motor e dos adaptadores da API.

## Requisitos

.NET 8 SDK.

## Executar os testes

Na raiz do repositório:

```sh
dotnet run --project Muggins.Engine.Tests/Muggins.Engine.Tests.csproj
```

O projeto de testes não depende de pacotes externos. Consulte [Muggins.Engine/README.md](Muggins.Engine/README.md) para detalhes do motor, da integração e das limitações conhecidas do formato do tabuleiro.
