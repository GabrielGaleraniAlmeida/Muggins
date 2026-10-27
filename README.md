# Muggins

Jogo de Dominó Ponta de Quina com motor autônomo em C#, cliente para a API oficial e uma interface web local baseada nas telas de sala e partida do protótipo.

## Estrutura

- `Muggins.Engine/`: regras, estratégia do bot, modelos e integração HTTP com a API.
- `Muggins.Engine.Tests/`: testes permanentes do motor e dos adaptadores da API.
- `Muggins.Web/`: servidor local e as páginas HTML/CSS/JavaScript conectadas ao motor e à API.

## Requisitos

.NET 8 SDK.

## Abrir o jogo

Com o .NET 8 SDK instalado, inicie o servidor local:

```sh
dotnet run --project Muggins.Web/Muggins.Web.csproj --launch-profile Muggins.Web
```

Abra `http://127.0.0.1:5080`. Informe o e-mail FIAP, cole o token recebido, prepare seu perfil e o perfil Muggins e escolha a pontuação da partida. A interface cria a partida pela API; você joga seus turnos e o motor escolhe as ações do perfil Muggins.

O endereço `127.0.0.1` só abre no mesmo computador onde esse comando está rodando. Se o projeto estiver em um container remoto, encaminhe a porta `5080` pelo ambiente e use o endereço encaminhado. Sem encaminhamento de porta, abra o projeto no computador que executará o servidor. A aba embutida do Codex não acessa automaticamente as portas do container.

O token da conta e as chaves de ativação não são enviados ao navegador. O servidor local os mantém criptografados em `.muggins-data/`; preserve essa pasta enquanto os perfis estiverem ativos na API. A opção “Desconectar e liberar perfis” desativa os dois jogadores depois que a partida termina.

Após sua ação, a tela mostra primeiro o estado e o placar confirmados pela API. O Muggins joga no turno seguinte; as peças na mesa exibem `EU` ou `M` para indicar quem as colocou.

O motor usa o histórico das jogadas enviadas por esta interface para manter as pontas do tabuleiro e confere a lista de pedras contra a API. Se outra ferramenta alterar a partida, ele interrompe a decisão em vez de presumir uma orientação.

Consulte [Muggins.Engine/README.md](Muggins.Engine/README.md) para detalhes das regras e da integração. O projeto de testes permanente fica em `Muggins.Engine.Tests/`.
