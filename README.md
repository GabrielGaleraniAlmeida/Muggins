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

Abra `http://127.0.0.1:5080`. Informe o e-mail FIAP e o token recebido, depois prepare seu perfil. Para jogar contra o Muggins, prepare também o perfil Muggins e escolha **Jogar contra Muggins**. Para jogar com outra pessoa, escolha **Criar sala para outra pessoa** e envie o código da sala; o outro jogador conecta a própria conta, prepara o próprio perfil e entra pelo código. Quem criou a sala inicia quando os dois estiverem conectados.

O endereço `127.0.0.1` só abre no mesmo computador onde esse comando está rodando. Se o projeto estiver em um container remoto, encaminhe a porta `5080` pelo ambiente e use o endereço encaminhado. Sem encaminhamento de porta, abra o projeto no computador que executará o servidor. A aba embutida do Codex não acessa automaticamente as portas do container.

Para uma partida online entre computadores, cada jogador executa o cliente no próprio computador e usa sua própria conta FIAP. Compartilhe o código da sala, não o token. A lista de salas disponíveis também mostra partidas aguardando um segundo jogador.

O token da conta e as chaves de ativação não são enviados ao navegador. O servidor local os mantém criptografados em `.muggins-data/`; preserve essa pasta enquanto os perfis estiverem ativos na API. A opção “Desconectar e liberar perfis” desativa os dois jogadores depois que a partida termina.

Após cada ação, a tela mostra o estado e o placar confirmados pela API. No modo contra o computador, o Muggins joga no turno seguinte; na sala online, cada jogador vê a atualização depois que o adversário joga. As peças identificam quem as colocou.

O motor usa o histórico das jogadas enviadas por esta interface para manter as pontas do tabuleiro. Na partida online, cada cliente sincroniza as peças novas com o estado da API e encaixa as jogadas nas pontas conhecidas.

Consulte [Muggins.Engine/README.md](Muggins.Engine/README.md) para detalhes das regras e da integração. O projeto de testes permanente fica em `Muggins.Engine.Tests/`.
