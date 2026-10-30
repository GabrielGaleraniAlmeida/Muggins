(() => {
  const $ = (selector) => document.querySelector(selector);
  const page = document.body.dataset.page;
  const errorMessages = {
    NAO_AUTENTICADO: "Token inválido, ausente ou revogado. Conecte novamente.",
    JOGADOR_JA_ATIVO: "Este perfil está reservado em outro cliente. Libere-o lá ou prepare um perfil novo.",
    CONFLITO_DE_ESTADO: "A partida mudou. O estado foi atualizado; tente novamente.",
    FORA_DO_TURNO: "Ainda não é a sua vez.",
    PECA_AUSENTE: "A pedra escolhida não está na sua mão.",
    ENCAIXE_INVALIDO: "Essa pedra não encaixa nessa ponta.",
    PASSAGEM_NAO_PERMITIDA: "A API não permite passar enquanto houver uma jogada ou compra possível.",
    ESTADO_INVALIDO: "A ação não combina com a fase atual da partida."
  };

  async function callApi(path, { method = "GET", body } = {}) {
    const response = await fetch(path, {
      method,
      credentials: "same-origin",
      headers: body === undefined ? {} : { "Content-Type": "application/json" },
      body: body === undefined ? undefined : JSON.stringify(body)
    });
    const payload = response.status === 204 ? null : await response.json().catch(() => null);
    if (!response.ok) {
      const code = payload?.message || `HTTP ${response.status}`;
      throw new Error(errorMessages[code] || code);
    }
    return payload;
  }

  function setBusy(button, busy, label) {
    if (!button) return;
    if (busy) {
      button.dataset.originalText = button.textContent;
      button.dataset.originalDisabled = String(button.disabled);
      button.textContent = label || "Aguarde…";
      button.disabled = true;
    } else {
      button.textContent = button.dataset.originalText || button.textContent;
      button.disabled = button.dataset.originalDisabled === "true";
      delete button.dataset.originalText;
      delete button.dataset.originalDisabled;
    }
  }

  if (page === "offline" || page === "online") initializeLobby();
  if (page === "match") initializeMatch();

  function initializeLobby() {
    let latestLobby = null;
    const overlay = $("#auth-overlay");
    const emailForm = $("#email-form");
    const tokenForm = $("#token-form");
    const authMessage = $("#auth-message");

    $("#connect-open").addEventListener("click", () => {
      overlay.hidden = false;
      authMessage.textContent = "";
      emailForm.hidden = false;
      tokenForm.hidden = true;
      $("#login-email").focus();
    });
    $("#auth-close").addEventListener("click", () => { overlay.hidden = true; });
    overlay.addEventListener("click", (event) => {
      if (event.target === overlay) overlay.hidden = true;
    });

    emailForm.addEventListener("submit", async (event) => {
      event.preventDefault();
      const button = emailForm.querySelector("button[type=submit]");
      setBusy(button, true, "Enviando…");
      authMessage.textContent = "";
      try {
        await callApi("/app-api/auth/request-token", {
          method: "POST",
          body: { email: $("#login-email").value.trim() }
        });
        emailForm.hidden = true;
        tokenForm.hidden = false;
        authMessage.textContent = "Se o e-mail for válido, o token chegará na sua caixa de entrada.";
        $("#access-token").focus();
      } catch (error) {
        authMessage.textContent = error.message;
      } finally {
        setBusy(button, false);
      }
    });

    tokenForm.addEventListener("submit", async (event) => {
      event.preventDefault();
      const button = tokenForm.querySelector("button[type=submit]");
      setBusy(button, true, "Conectando…");
      authMessage.textContent = "";
      try {
        await callApi("/app-api/auth/connect", {
          method: "POST",
          body: { token: $("#access-token").value.trim() }
        });
        overlay.hidden = true;
        $("#access-token").value = "";
        await loadLobby();
      } catch (error) {
        authMessage.textContent = error.message;
      } finally {
        setBusy(button, false);
      }
    });

    $("#prepare-human")?.addEventListener("click", () => preparePlayer("human", "Você", $("#prepare-human")));
    $("#prepare-bot")?.addEventListener("click", () => preparePlayer("bot", "Muggins", $("#prepare-bot")));
    $("#start-match")?.addEventListener("click", startMatch);
    $("#create-online")?.addEventListener("click", createOnlineMatch);
    $("#join-online")?.addEventListener("click", joinOnlineMatch);
    $("#join-room-code")?.addEventListener("keydown", (event) => {
      if (event.key === "Enter") joinOnlineMatch();
    });
    $("#start-online")?.addEventListener("click", startOnlineMatch);
    $("#copy-room-code")?.addEventListener("click", copyRoomCode);
    $("#room-list")?.addEventListener("click", (event) => {
      const joinButton = event.target.closest("button[data-join-match]");
      if (!joinButton) return;
      $("#join-room-code").value = joinButton.dataset.joinMatch;
      joinOnlineMatch();
    });
    $("#disconnect")?.addEventListener("click", disconnect);
    loadLobby();
    window.setInterval(loadLobby, 3000);

    async function preparePlayer(role, name, button) {
      setBusy(button, true, "Preparando…");
      setLobbyMessage(`Preparando o perfil ${name}…`);
      try {
        await callApi("/app-api/players/prepare", { method: "POST", body: { role, name } });
      } catch (error) {
        setLobbyMessage(error.message);
      } finally {
        setBusy(button, false);
        await loadLobby();
      }
    }

    async function startMatch() {
      const button = $("#start-match");
      setBusy(button, true, "Abrindo mesa…");
      setLobbyMessage("Criando a partida e distribuindo as pedras pela API…");
      try {
        await callApi("/app-api/matches/vs-bot", {
          method: "POST",
          body: { targetScore: Number($("#target-score").value) }
        });
        window.location.href = "/partida.html";
      } catch (error) {
        setLobbyMessage(error.message);
        await loadLobby();
      } finally {
        setBusy(button, false);
      }
    }

    async function createOnlineMatch() {
      const button = $("#create-online");
      setBusy(button, true, "Criando sala…");
      setLobbyMessage("Abrindo uma sala para outro jogador…");
      try {
        await callApi("/app-api/matches/online/create", {
          method: "POST",
          body: { targetScore: Number($("#target-score").value) }
        });
        await loadLobby();
      } catch (error) {
        setLobbyMessage(error.message);
        await loadLobby();
      } finally {
        setBusy(button, false);
        await loadLobby();
      }
    }

    async function joinOnlineMatch() {
      const code = $("#join-room-code").value.trim();
      if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(code)) {
        setLobbyMessage("Cole o código completo da sala.");
        return;
      }

      const button = $("#join-online");
      setBusy(button, true, "Entrando…");
      setLobbyMessage("Entrando na sala pela API…");
      try {
        await callApi("/app-api/matches/online/join", {
          method: "POST",
          body: { matchId: code }
        });
        window.location.href = "/partida.html";
      } catch (error) {
        setLobbyMessage(error.message);
        await loadLobby();
      } finally {
        setBusy(button, false);
        await loadLobby();
      }
    }

    async function startOnlineMatch() {
      const button = $("#start-online");
      setBusy(button, true, "Iniciando…");
      setLobbyMessage("Iniciando a partida pela API…");
      try {
        await callApi("/app-api/matches/online/start", { method: "POST", body: {} });
        window.location.href = "/partida.html";
      } catch (error) {
        setLobbyMessage(error.message);
        await loadLobby();
      } finally {
        setBusy(button, false);
        await loadLobby();
      }
    }

    async function copyRoomCode() {
      const code = $("#online-room-code").textContent.trim();
      try {
        await navigator.clipboard.writeText(code);
        $("#copy-room-code").textContent = "Código copiado";
      } catch {
        window.prompt("Copie o código da sala:", code);
      }
    }

    async function disconnect() {
      if (!window.confirm("Desconectar também libera seus perfis ativos na API?")) return;
      const button = $("#disconnect");
      setBusy(button, true, "Liberando…");
      try {
        await callApi("/app-api/auth/disconnect", { method: "POST", body: {} });
        await loadLobby();
      } catch (error) {
        setLobbyMessage(error.message);
      } finally {
        setBusy(button, false);
        await loadLobby();
      }
    }

    async function loadLobby() {
      try {
        const lobby = await callApi("/app-api/lobby");
        renderLobby(lobby);
      } catch (error) {
        setLobbyMessage(error.message);
      }
    }

    function renderLobby(lobby) {
      latestLobby = lobby;
      const connected = lobby.connected;
      const humanReady = Boolean(lobby.humanPlayer?.ready);
      const botReady = Boolean(lobby.botPlayer?.ready);
      const hasMatch = Boolean(lobby.activeMatchId);
      const onlineMatch = lobby.matchMode === "Online" && hasMatch;
      const waitingMatches = lobby.waitingMatches || [];

      setText("#profile-name", lobby.humanPlayer?.name || "Você");
      setText("#profile-status", connected ? "Conta FIAP conectada" : "Conta não conectada");
      setText("#header-connection-label", connected ? "Conectado" : "Conta desconectada");
      $("#header-connection")?.classList.toggle("connected", connected);
      setText("#disclaimer", connected
        ? "Motor Muggins conectado à API"
        : "Conecte-se para começar a jogar");
      setText("#human-profile", lobby.humanPlayer
        ? `Você: ${lobby.humanPlayer.name}${humanReady ? " · pronto" : " · perfil sem chave local"}`
        : "Você: não preparado");
      setText("#bot-profile", lobby.botPlayer
        ? `Muggins: ${lobby.botPlayer.name}${botReady ? " · pronto" : " · perfil sem chave local"}`
        : "Muggins: não preparado");

      setHidden("#connect-open", connected);
      setHidden("#prepare-human", !connected || humanReady);
      setHidden("#prepare-bot", !connected || botReady);
      setHidden("#start-match", hasMatch);
      setDisabled("#start-match", !lobby.canStartMachineMatch || hasMatch);
      setHidden("#create-online", hasMatch);
      setDisabled("#create-online", !connected || !humanReady || hasMatch);
      setDisabled("#join-room-code", !connected || !humanReady || hasMatch);
      setDisabled("#join-online", !connected || !humanReady || hasMatch);
      setHidden("#online-room", !onlineMatch);
      setText("#online-room-code", lobby.activeMatchId || "");
      setText("#online-room-status", lobby.opponentName
        ? `${lobby.opponentName} entrou na sala. ${lobby.matchParticipants}/2 jogadores conectados.`
        : "Sala criada. Compartilhe este código e aguarde o outro jogador entrar.");
      setHidden("#start-online", !lobby.canStartOnlineMatch);
      setDisabled("#start-online", !lobby.canStartOnlineMatch);
      setHidden("#resume-match", !hasMatch);
      setText("#resume-match", onlineMatch ? "Abrir partida online" : "Continuar partida");
      setDisabled("#target-score", hasMatch);
      const targetScore = $("#target-score");
      if (hasMatch && targetScore) targetScore.value = String(lobby.targetScore);
      setHidden("#disconnect", !connected);

      const roomList = $("#room-list");
      if (roomList) {
        roomList.replaceChildren();
        for (const match of waitingMatches) {
          const waitingRow = document.createElement("div");
          waitingRow.className = "room-row";
          waitingRow.innerHTML = '<svg class="people-icon" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><circle cx="9" cy="8" r="3.2"/><circle cx="17" cy="9" r="2.5"/><path d="M2.7 18.6c.3-3.2 2.4-5.1 6.3-5.1s6 1.9 6.3 5.1v1H2.7zm12.1-4.4c3.8-.6 6.3 1.1 6.5 4.3h-4.7c-.1-1.7-.7-3.2-1.8-4.3"/></svg><span>1/2</span><span class="room-separator"></span><span class="room-target"></span><button class="small-action" type="button">Entrar</button>';
          waitingRow.querySelector(".room-target").textContent = `Online · ${match.pontuacaoAlvo} pts · ${match.partidaId.slice(0, 8)}`;
          const joinButton = waitingRow.querySelector("button");
          joinButton.dataset.joinMatch = match.partidaId;
          joinButton.disabled = !connected || !humanReady || hasMatch;
          joinButton.setAttribute("aria-label", `Entrar na sala ${match.partidaId}`);
          roomList.append(waitingRow);
        }
      }
      setHidden("#rooms-empty", waitingMatches.length > 0);

      if (hasMatch && onlineMatch && lobby.isMatchHost && !lobby.canStartOnlineMatch) {
        setLobbyMessage("Compartilhe o código da sala. O início será liberado quando o outro jogador entrar.");
      } else if (hasMatch && onlineMatch && lobby.canStartOnlineMatch) {
        setLobbyMessage("Os dois jogadores estão na sala. Inicie a partida quando estiverem prontos.");
      } else if (hasMatch) {
        setLobbyMessage("Há uma partida em andamento nesta sessão. Continue de onde parou.");
      } else if (!connected) {
        setLobbyMessage("Conecte sua conta FIAP para preparar seu perfil e abrir uma partida.");
      } else if (!humanReady) {
        setLobbyMessage(`Prepare seu perfil para jogar. ${waitingMatches.length} sala(s) aguardando outro jogador.`);
      } else if (page === "offline" && !botReady) {
        setLobbyMessage("Seu perfil está pronto. Prepare Muggins para iniciar uma partida contra a máquina.");
      } else {
        setLobbyMessage(`Perfil pronto. ${waitingMatches.length} sala(s) aguardando outro jogador.`);
      }
    }

    function setLobbyMessage(message) {
      const element = $("#lobby-status");
      if (element) element.textContent = message;
    }

    function setText(selector, value) {
      const element = $(selector);
      if (element) element.textContent = value;
    }

    function setHidden(selector, hidden) {
      const element = $(selector);
      if (element) element.hidden = hidden;
    }

    function setDisabled(selector, disabled) {
      const element = $(selector);
      if (element) element.disabled = disabled;
    }
  }

  function initializeMatch() {
    let selectedIndex = null;
    let currentMatch = null;
    let loading = false;
    let actionInProgress = false;
    let scoreNoticeTimer = null;
    const hand = $("#player-hand");
    const message = $("#game-message");
    const leftButton = $("#play-left");
    const rightButton = $("#play-right");
    const buyButton = $("#buy");
    const passButton = $("#pass");
    const finishButton = $("#finish-match");
    const restartButton = $("#restart-match");

    leftButton.addEventListener("click", () => play("left"));
    rightButton.addEventListener("click", () => play("right"));
    buyButton.addEventListener("click", () => submitAction("buy"));
    passButton.addEventListener("click", () => submitAction("pass"));
    finishButton.addEventListener("click", () => {
      if (currentMatch?.phase === "Finished") window.location.href = "/";
    });
    restartButton.addEventListener("click", restartMatch);
    window.addEventListener("resize", () => {
      if (currentMatch) renderBoard(currentMatch);
    });
    hand.addEventListener("click", (event) => {
      const tileButton = event.target.closest("button[data-index]");
      if (!tileButton || !currentMatch?.isHumanTurn) return;
      selectedIndex = Number(tileButton.dataset.index);
      renderMatch(currentMatch);
    });

    refreshMatch();
    window.setInterval(refreshMatch, 1600);

    async function refreshMatch() {
      if (loading || actionInProgress) return;
      loading = true;
      try {
        const updatedMatch = await callApi("/app-api/match");
        showScoreNotice(currentMatch, updatedMatch);
        currentMatch = updatedMatch;
        renderMatch(currentMatch);
        setMessage("");
      } catch (error) {
        setMessage(error.message);
        $("#sync-indicator").textContent = "!";
        $("#sync-label").textContent = "Sem sincronização";
        $("#sync-status").classList.add("error");
        disableActions();
      } finally {
        loading = false;
      }
    }

    async function play(side) {
      if (!currentMatch?.isHumanTurn || selectedIndex === null) return;
      const tile = currentMatch.hand[selectedIndex];
      if (!tile) return;
      await submitAction("play", { valueA: tile.valorA, valueB: tile.valorB, side });
    }

    async function submitAction(kind, details = {}) {
      if (actionInProgress) return;
      actionInProgress = true;
      disableActions();
      setMessage(kind === "play" ? "Enviando sua jogada…" : "Enviando ação…");
      try {
        const updatedMatch = await callApi("/app-api/match/action", {
          method: "POST",
          body: { kind, ...details }
        });
        showScoreNotice(currentMatch, updatedMatch);
        currentMatch = updatedMatch;
        selectedIndex = null;
        renderMatch(currentMatch);
        setMessage("");
      } catch (error) {
        setMessage(error.message);
        actionInProgress = false;
        await refreshMatch();
      } finally {
        actionInProgress = false;
        if (currentMatch) renderMatch(currentMatch);
      }
    }

    async function restartMatch() {
      if (actionInProgress || currentMatch?.phase !== "Finished") return;
      if (!window.confirm("Criar uma nova partida com a mesma pontuação-alvo?")) return;

      actionInProgress = true;
      setBusy(restartButton, true, "Abrindo…");
      setMessage("Criando uma nova partida…");
      let restartError = null;
      try {
        await callApi("/app-api/matches/vs-bot", {
          method: "POST",
          body: { targetScore: currentMatch.targetScore }
        });
        window.location.replace("/partida.html");
      } catch (error) {
        restartError = error.message;
      } finally {
        actionInProgress = false;
        setBusy(restartButton, false);
        if (currentMatch) renderMatch(currentMatch);
        if (restartError) setMessage(restartError);
      }
    }

    function renderMatch(state) {
      if (!state) return;
      if (selectedIndex !== null && selectedIndex >= state.hand.length) selectedIndex = null;
      const isFinished = state.phase === "Finished";
      $("#match-label").textContent = `Rodada ${state.round} · até ${state.targetScore} pontos`;
      $("#opponent-name").textContent = state.opponentName || state.botName || "Muggins";
      $("#opponent-score").textContent = state.opponentScore ?? state.botScore ?? 0;
      $("#opponent-count").textContent = state.opponentTileCount ?? state.botTileCount ?? 0;
      $("#human-name").textContent = state.humanName || "Você";
      $("#human-score").textContent = state.humanScore;
      $("#human-count").textContent = state.humanTileCount;
      const boardTiles = state.board || [];
      const singleOpeningDouble = boardTiles.length === 1 && boardTiles[0].isDouble;
      const leftEndCount = state.leftEnd === null
        ? 0
        : state.leftEnd * (boardTiles.length > 1 && boardTiles[0]?.isDouble ? 2 : 1);
      const rightEndCount = state.rightEnd === null
        ? 0
        : state.rightEnd * (boardTiles.length > 1 && boardTiles[boardTiles.length - 1]?.isDouble ? 2 : 1);
      $("#ends-total").textContent = String(state.endsTotal ?? leftEndCount + rightEndCount);
      $("#ends-values").textContent = singleOpeningDouble
        ? `${state.leftEnd} + ${state.rightEnd}`
        : `${leftEndCount} + ${rightEndCount}`;
      $("#turn-indicator").textContent = turnText(state);
      $("#sync-indicator").textContent = "✓";
      $("#sync-label").textContent = "Estado confirmado pela API";
      $("#sync-status").classList.remove("error");
      finishButton.disabled = !isFinished || actionInProgress;
      finishButton.title = isFinished
        ? "Voltar à sala após o encerramento confirmado pela API."
        : "A API atual não permite encerrar ou abandonar uma partida em andamento.";
      restartButton.disabled = !isFinished || actionInProgress;
      restartButton.title = isFinished
        ? "Criar uma nova partida com a mesma pontuação-alvo."
        : "Disponível quando a partida terminar pelas regras da API.";
      $("#match-controls-note").textContent = isFinished
        ? "Partida encerrada pela API"
        : "Encerramento depende das regras da API";

      const opponentHand = $("#opponent-hand");
      opponentHand.replaceChildren();
      const backCount = Math.min(7, state.opponentTileCount ?? state.botTileCount ?? 0);
      for (let index = 0; index < backCount; index += 1) {
        const back = document.createElement("span");
        back.className = "back-tile";
        opponentHand.append(back);
      }
      opponentHand.setAttribute("aria-label", `${state.opponentTileCount ?? state.botTileCount ?? 0} peças do oponente, viradas para baixo`);

      renderBoard(state);

      hand.replaceChildren();
      state.hand.forEach((piece, index) => {
        const button = createDomino(piece.valorA, piece.valorB, true, true);
        button.dataset.index = String(index);
        button.dataset.a = String(piece.valorA);
        button.dataset.b = String(piece.valorB);
        button.classList.toggle("selected", selectedIndex === index);
        button.setAttribute("aria-pressed", selectedIndex === index ? "true" : "false");
        hand.append(button);
      });

      const selected = selectedIndex === null ? null : state.hand[selectedIndex];
      $("#selected-caption").textContent = selected
        ? `Selecionada: ${selected.valorA} | ${selected.valorB}`
        : state.phase === "Opening" ? `Abertura obrigatória: ${tileText(state.requiredOpeningTile)}` : "Selecione uma pedra";

      const canSelect = state.isHumanTurn && ["Opening", "Play"].includes(state.phase);
      hand.querySelectorAll("button").forEach((button) => { button.disabled = !canSelect || actionInProgress; });
      const selectedTile = selected ? [selected.valorA, selected.valorB] : null;
      const opening = state.requiredOpeningTile;
      const isRequiredOpening = selectedTile && opening && sameTile(selectedTile, [opening.valorA, opening.valorB]);
      const leftFits = selectedTile && state.leftEnd !== null && selectedTile.includes(state.leftEnd);
      const rightFits = selectedTile && state.rightEnd !== null && selectedTile.includes(state.rightEnd);
      leftButton.disabled = actionInProgress || !state.isHumanTurn || !(state.phase === "Play" && leftFits || state.phase === "Opening" && isRequiredOpening);
      rightButton.disabled = actionInProgress || !state.isHumanTurn || state.phase !== "Play" || !rightFits;
      leftButton.textContent = state.phase === "Opening" ? "Abrir mesa" : "← Esquerda";
      buyButton.disabled = actionInProgress || !state.isHumanTurn || state.phase !== "Buy";
      passButton.disabled = actionInProgress || !state.isHumanTurn || state.phase !== "Pass";
      leftButton.classList.toggle("target-action", !leftButton.disabled);
      rightButton.classList.toggle("target-action", !rightButton.disabled);
      buyButton.classList.toggle("target-action", !buyButton.disabled);
      passButton.classList.toggle("target-action", !passButton.disabled);

      if (state.phase === "Finished") {
        const winner = state.winnerName || "Empate";
        setMessage(`Partida encerrada · ${winner}`);
      }
    }

    function renderBoard(state) {
      const board = $("#board-chain");
      const boardTiles = state.board || [];
      board.replaceChildren();
      board.setAttribute("aria-label", `${boardTiles.length} peça(s) no tabuleiro`);
      if (boardTiles.length === 0) {
        board.style.transform = "translate(-50%, -50%)";
        return;
      }

      const boardZone = board.closest(".board-zone");
      const boardWidth = board.clientWidth || (boardZone?.clientWidth || window.innerWidth) * 0.94;
      const boardHeight = boardZone?.clientHeight || window.innerHeight * 0.5;
      const boardZoneHeight = boardZone?.clientHeight || window.innerHeight * 0.5;
      const requestedOpeningIndex = Number.isInteger(state.openingTileIndex)
        ? state.openingTileIndex
        : Math.floor((boardTiles.length - 1) / 2);
      const openingIndex = Math.max(0, Math.min(boardTiles.length - 1, requestedOpeningIndex));
      const tileGap = 0;
      const availableHeight = Math.max(90, boardHeight * 0.95);
      const isDoubleTile = (piece) => piece.leftValue === piece.rightValue;
      let maxTileWidth = Math.min(148, boardWidth);
      const buildCenteredLayout = (candidateWidth) => {
        const candidateHeight = candidateWidth * 66 / 116;
        const lines = [];
        const turns = [];
        const horizontalDoubleIndices = new Set();
        const isVerticalDouble = (tileIndex) =>
          isDoubleTile(boardTiles[tileIndex]) && !horizontalDoubleIndices.has(tileIndex);
        const pieceWidth = (tileIndex) => isVerticalDouble(tileIndex) ? candidateHeight : candidateWidth;
        const pieceHeight = (tileIndex) => isVerticalDouble(tileIndex) ? candidateWidth : candidateHeight;
        // Keep the opening fixed at the table center and snake each side out independently.
        const rootWidth = pieceWidth(openingIndex);
        const rootHeight = pieceHeight(openingIndex);
        const rootLeft = boardWidth / 2 - rootWidth / 2;
        const rootTop = -rootHeight / 2;
        lines.push({
          indices: [openingIndex], left: rootLeft, anchor: rootLeft,
          width: rootWidth, height: rootHeight, direction: 1,
          top: rootTop, contentTop: rootTop
        });

        const buildArm = (sequence, initialDirection, verticalDirection) => {
          let index = 0;
          let direction = initialDirection;
          let anchor = initialDirection > 0 ? rootLeft + rootWidth : rootLeft;
          let contentTop = -candidateHeight / 2;

          while (index < sequence.length) {
            const available = direction > 0 ? boardWidth - anchor : anchor;
            if (available <= 0) return false;
            let remainingWidth = tileGap * (sequence.length - index - 1);
            for (let remainingIndex = index; remainingIndex < sequence.length; remainingIndex += 1) {
              remainingWidth += pieceWidth(sequence[remainingIndex]);
            }

            const indices = [];
            let lineWidth = 0;
            let lineHeight = candidateHeight;
            if (remainingWidth <= available) {
              while (index < sequence.length) {
                const tileIndex = sequence[index++];
                const width = pieceWidth(tileIndex);
                lineWidth += indices.length > 0 ? tileGap + width : width;
                lineHeight = Math.max(lineHeight, pieceHeight(tileIndex));
                indices.push(tileIndex);
              }
            } else {
              const turnOverhang = Math.max(candidateHeight / 2 - candidateWidth / 4, candidateWidth / 4);
              const lineBudget = available - turnOverhang;
              while (index < sequence.length) {
                const tileIndex = sequence[index];
                const width = pieceWidth(tileIndex);
                const nextWidth = indices.length > 0 ? tileGap + width : width;
                if (lineWidth + nextWidth > lineBudget) {
                  if (index === sequence.length - 1 && lineWidth + nextWidth <= available) {
                    index += 1;
                    lineWidth += nextWidth;
                    lineHeight = Math.max(lineHeight, pieceHeight(tileIndex));
                    indices.push(tileIndex);
                  }
                  break;
                }

                index += 1;
                lineWidth += nextWidth;
                lineHeight = Math.max(lineHeight, pieceHeight(tileIndex));
                indices.push(tileIndex);
              }
              if (indices.length === 0) return false;
            }

            const lineLeft = direction > 0 ? anchor : anchor - lineWidth;
            const lineTop = contentTop - (lineHeight - candidateHeight) / 2;
            lines.push({
              indices, left: lineLeft, anchor, width: lineWidth, height: lineHeight,
              direction, reverseValues: direction !== initialDirection,
              top: lineTop, contentTop
            });
            if (index >= sequence.length) break;

            const turnIndex = sequence[index];
            const terminalIndex = indices[indices.length - 1];
            const terminalOffset = isVerticalDouble(terminalIndex) ? candidateHeight / 2 : candidateWidth / 4;
            const turnCenter = direction > 0
              ? lineLeft + lineWidth - terminalOffset
              : lineLeft + terminalOffset;
            const turnLeft = turnCenter - candidateHeight / 2;
            const turnTop = verticalDirection > 0
              ? contentTop + candidateHeight
              : contentTop - candidateWidth;
            turns.push({ index: turnIndex, left: turnLeft, top: turnTop });
            index += 1;
            if (index >= sequence.length) break;

            const nextTileIndex = sequence[index];
            const nextPieceIsDouble = isDoubleTile(boardTiles[nextTileIndex]);
            if (nextPieceIsDouble) horizontalDoubleIndices.add(nextTileIndex);
            const nextPieceOffset = nextPieceIsDouble ? candidateWidth / 2 : candidateWidth / 4;
            anchor = direction > 0
              ? turnCenter + nextPieceOffset
              : turnCenter - nextPieceOffset;
            direction *= -1;
            contentTop = verticalDirection > 0
              ? turnTop + candidateWidth
              : turnTop - candidateHeight;
          }

          return true;
        };

        const leftSequence = [];
        for (let index = openingIndex - 1; index >= 0; index -= 1) leftSequence.push(index);
        const rightSequence = [];
        for (let index = openingIndex + 1; index < boardTiles.length; index += 1) rightSequence.push(index);
        if (!buildArm(leftSequence, -1, -1) || !buildArm(rightSequence, 1, 1)) return null;

        let minY = Math.min(...lines.map(line => line.top), ...turns.map(turn => turn.top));
        let maxY = Math.max(...lines.map(line => line.top + line.height),
          ...turns.map(turn => turn.top + candidateWidth));
        let minX = Math.min(...lines.map(line => line.left));
        let maxX = Math.max(...lines.map(line => line.left + line.width));
        for (const turn of turns) {
          minX = Math.min(minX, turn.left);
          maxX = Math.max(maxX, turn.left + candidateHeight);
        }
        const verticalOffset = -minY;
        for (const line of lines) {
          line.top += verticalOffset;
          line.contentTop += verticalOffset;
        }
        for (const turn of turns) turn.top += verticalOffset;
        minY += verticalOffset;
        maxY += verticalOffset;

        return {
          lines,
          turns,
          height: maxY - minY,
          tileHeight: candidateHeight,
          minX,
          maxX,
          openingCenterY: verticalOffset,
          horizontalDoubleIndices: [...horizontalDoubleIndices]
        };
      };

      let layout = null;
      const minTileWidth = Math.min(boardWidth < 760 ? 44 : boardWidth < 900 ? 54 : 88, maxTileWidth);
      let tileWidth = minTileWidth;
      for (let candidateWidth = maxTileWidth; candidateWidth >= minTileWidth; candidateWidth -= 0.5) {
        const candidateLayout = buildCenteredLayout(candidateWidth);
        if (!candidateLayout || candidateLayout.height > availableHeight ||
          candidateLayout.minX < 0 || candidateLayout.maxX > boardWidth ||
          candidateLayout.openingCenterY > boardZoneHeight / 2 ||
          candidateLayout.height - candidateLayout.openingCenterY > boardZoneHeight / 2) continue;

        // Use the largest pieces that fit so long chains wrap into readable rows.
        layout = candidateLayout;
        tileWidth = candidateWidth;
        break;
      }

      if (!layout) {
        layout = buildCenteredLayout(minTileWidth);
        tileWidth = minTileWidth;
      }
      const tileHeight = layout?.tileHeight || tileWidth * 66 / 116;
      const horizontalDoubleIndices = new Set(layout.horizontalDoubleIndices || []);
      const pieceWidth = (tileIndex) =>
        isDoubleTile(boardTiles[tileIndex]) && !horizontalDoubleIndices.has(tileIndex) ? tileHeight : tileWidth;
      const openingCenter = (() => {
        for (const turn of layout.turns) {
          if (turn.index === openingIndex) {
            return { x: turn.left + tileHeight / 2, y: turn.top + tileWidth / 2 };
          }
        }

        for (const line of layout.lines) {
          if (!line.indices.includes(openingIndex)) continue;
          let cursor = line.direction > 0 ? line.left : line.left + line.width;
          for (const index of line.indices) {
            const width = pieceWidth(index);
            const left = line.direction > 0 ? cursor : cursor - width;
            if (line.direction > 0) cursor += width;
            else cursor = left;
            if (index === openingIndex) return { x: left + width / 2, y: line.top + line.height / 2 };
          }
        }

        return { x: boardWidth / 2, y: layout.height / 2 };
      })();
      const targetOffsetX = boardWidth / 2 - openingCenter.x;
      const targetOffsetY = layout.height / 2 - openingCenter.y;
      // Keep the opening at the board-zone center; candidate sizing above keeps the snake inside it.
      const offsetX = targetOffsetX;
      const offsetY = targetOffsetY;
      board.style.transform = `translate(calc(-50% + ${offsetX}px), calc(-50% + ${offsetY}px))`;
      board.style.setProperty("--board-tile-width", `${tileWidth}px`);
      board.style.setProperty("--board-tile-height", `${tileHeight}px`);
      board.style.height = `${layout?.height || tileHeight}px`;
      board.style.setProperty("--board-pip-core", `${Math.max(1.9, tileWidth * 0.04)}px`);
      board.style.setProperty("--board-pip-halo", `${Math.max(2.5, tileWidth * 0.05)}px`);

      for (let rowIndex = 0; rowIndex < layout.lines.length; rowIndex += 1) {
        const line = layout.lines[rowIndex];
        const row = document.createElement("div");
        const reverseLayout = line.direction < 0;
        const reverseValues = line.reverseValues ?? reverseLayout;
        row.className = `board-row${reverseLayout ? " reverse" : ""}`;
        row.style.position = "absolute";
        row.style.left = "0";
        row.style.top = `${line.top}px`;
        row.style.zIndex = String(rowIndex * 2 + 1);
        row.setAttribute("role", "group");
        row.setAttribute("aria-label", `Linha ${rowIndex + 1} da sequência`);
        if (reverseLayout) row.style.paddingRight = `${boardWidth - line.anchor}px`;
        else row.style.paddingLeft = `${line.anchor}px`;

        for (const index of line.indices) {
          const piece = boardTiles[index];
          const firstValue = reverseValues ? piece.rightValue : piece.leftValue;
          const secondValue = reverseValues ? piece.leftValue : piece.rightValue;
          const isVerticalDouble = isDoubleTile(piece) && !horizontalDoubleIndices.has(index);
          const tile = createDomino(firstValue, secondValue, isVerticalDouble);
          const owner = piece.isBotTile ? (state.botName || "Adversário") : "Você";
          tile.setAttribute("aria-label", `${firstValue} e ${secondValue}, jogada por ${owner}`);
          tile.title = `Jogada de ${owner}`;
          tile.classList.add("board-tile");
          const badge = document.createElement("span");
          badge.className = `board-owner ${piece.isBotTile ? "bot" : "human"}`;
          badge.textContent = piece.isBotTile ? "OP" : "EU";
          badge.setAttribute("aria-hidden", "true");
          tile.append(badge);
          row.append(tile);
        }

        board.append(row);
      }

      for (let turnIndex = 0; turnIndex < layout.turns.length; turnIndex += 1) {
        const turn = layout.turns[turnIndex];
        const piece = boardTiles[turn.index];
        // PlacedTile values follow chain order: A connects from the row above, and B continues below.
        const tile = createDomino(piece.leftValue, piece.rightValue, true);
        const owner = piece.isBotTile ? (state.botName || "Adversário") : "Você";
        tile.setAttribute("aria-label", `${piece.leftValue} e ${piece.rightValue}, jogada por ${owner}; peça de virada`);
        tile.title = `Jogada de ${owner} · virada da sequência`;
        tile.classList.add("board-tile", "turn-connector");
        tile.style.left = `${turn.left}px`;
        tile.style.top = `${turn.top}px`;
        tile.style.zIndex = String(turnIndex * 2 + 2);
        const badge = document.createElement("span");
        badge.className = `board-owner ${piece.isBotTile ? "bot" : "human"}`;
        badge.textContent = piece.isBotTile ? "ADV" : "EU";
        badge.setAttribute("aria-hidden", "true");
        tile.append(badge);
        board.append(tile);
      }
    }

    function disableActions() {
      [leftButton, rightButton, buyButton, passButton].forEach((button) => { button.disabled = true; });
      hand.querySelectorAll("button").forEach((button) => { button.disabled = true; });
    }

    function setMessage(text) {
      message.textContent = text;
      message.hidden = !text;
    }

    function showScoreNotice(previous, updated) {
      if (!previous || !updated) return;
      const changes = [
        { name: updated.humanName || "Você", points: updated.humanScore - previous.humanScore },
        { name: updated.botName || "Adversário", points: updated.botScore - previous.botScore }
      ].filter(change => change.points > 0);
      if (changes.length === 0) return;

      const notice = $("#score-notice");
      $("#score-notice-player").textContent = changes.map(change => change.name).join(" · ");
      $("#score-notice-points").textContent = changes
        .map(change => `+${change.points} ${change.points === 1 ? "ponto" : "pontos"}`)
        .join(" · ");
      notice.hidden = false;
      notice.classList.remove("show");
      void notice.offsetWidth;
      notice.classList.add("show");
      window.clearTimeout(scoreNoticeTimer);
      scoreNoticeTimer = window.setTimeout(() => {
        notice.classList.remove("show");
        notice.hidden = true;
      }, 3600);
    }
  }

  function createDomino(valueA, valueB, isVertical, button = false) {
    const pipPositions = [
      [],
      [[2, 2]],
      [[1, 1], [3, 3]],
      [[1, 1], [2, 2], [3, 3]],
      [[1, 1], [1, 3], [3, 1], [3, 3]],
      [[1, 1], [1, 3], [2, 2], [3, 1], [3, 3]],
      [[1, 1], [1, 2], [2, 1], [2, 2], [3, 1], [3, 2]]
    ];
    const element = document.createElement(button ? "button" : "div");
    element.className = `domino ${isVertical ? "vertical" : "horizontal"}${button ? " hand-tile" : ""}`;
    if (button) element.type = "button";
    element.setAttribute("aria-label", `${valueA} e ${valueB}`);
    for (const value of [valueA, valueB]) {
      const half = document.createElement("span");
      half.className = "half";
      half.dataset.v = String(value);
      if (value === 6) half.classList.add("six-pips");
      const positions = value === 6 && !isVertical
        ? pipPositions[value].map(([row, column]) => [column, row])
        : pipPositions[value] || [];
      for (const [row, column] of positions) {
        const pip = document.createElement("span");
        pip.className = "pip";
        pip.style.gridRow = String(row);
        pip.style.gridColumn = String(column);
        pip.setAttribute("aria-hidden", "true");
        half.append(pip);
      }
      element.append(half);
    }
    return element;
  }

  function sameTile(first, second) {
    return (first[0] === second[0] && first[1] === second[1]) ||
      (first[0] === second[1] && first[1] === second[0]);
  }

  function tileText(tile) {
    return tile ? `${tile.valorA} | ${tile.valorB}` : "a pedra obrigatória";
  }

  function turnText(state) {
    if (state.phase === "Finished") return state.winnerName ? `${state.winnerName} venceu` : "Partida encerrada";
    if (state.isHumanTurn) {
      return ({ Opening: "Jogue a pedra de abertura", Play: "Sua vez", Buy: "Compre uma pedra", Pass: "Você deve passar" })[state.phase] || "Sua vez";
    }
    const playerName = state.turnPlayerName?.trim();
    if (!playerName) return "Aguardando a partida";

    const normalizedName = playerName.toLocaleLowerCase("pt-BR");
    if (["você", "voce", "oponente"].includes(normalizedName)) return "Oponente jogando";
    return `${playerName} está jogando`;
  }
})();
