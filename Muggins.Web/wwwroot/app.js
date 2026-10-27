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

  if (page === "lobby") initializeLobby();
  if (page === "match") initializeMatch();

  function initializeLobby() {
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

    $("#prepare-human").addEventListener("click", () => preparePlayer("human", "Você", $("#prepare-human")));
    $("#prepare-bot").addEventListener("click", () => preparePlayer("bot", "Muggins", $("#prepare-bot")));
    $("#start-match").addEventListener("click", startMatch);
    $("#disconnect").addEventListener("click", disconnect);
    loadLobby();

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

    async function disconnect() {
      if (!window.confirm("Desconectar também libera seus dois perfis na API?")) return;
      const button = $("#disconnect");
      setBusy(button, true, "Liberando…");
      try {
        await callApi("/app-api/auth/disconnect", { method: "POST", body: {} });
        await loadLobby();
      } catch (error) {
        setLobbyMessage(error.message);
      } finally {
        setBusy(button, false);
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
      const connected = lobby.connected;
      const humanReady = Boolean(lobby.humanPlayer?.ready);
      const botReady = Boolean(lobby.botPlayer?.ready);
      const hasMatch = Boolean(lobby.activeMatchId);
      $("#profile-name").textContent = lobby.humanPlayer?.name || "Você";
      $("#profile-status").textContent = connected ? "Conta FIAP conectada" : "Conta não conectada";
      $("#disclaimer").textContent = connected
        ? "Motor Muggins conectado à API"
        : "Prévia visual da mesa · conecte-se para jogar";
      $("#human-profile").textContent = lobby.humanPlayer
        ? `Você: ${lobby.humanPlayer.name}${humanReady ? " · pronto" : " · perfil sem chave local"}`
        : "Você: não preparado";
      $("#bot-profile").textContent = lobby.botPlayer
        ? `Muggins: ${lobby.botPlayer.name}${botReady ? " · pronto" : " · perfil sem chave local"}`
        : "Muggins: não preparado";

      $("#connect-open").hidden = connected;
      $("#prepare-human").hidden = !connected || humanReady;
      $("#prepare-bot").hidden = !connected || botReady;
      $("#start-match").hidden = hasMatch;
      $("#start-match").disabled = !lobby.canStartMachineMatch || hasMatch;
      $("#resume-match").hidden = !hasMatch;
      $("#target-score").disabled = hasMatch;
      if (hasMatch) $("#target-score").value = String(lobby.targetScore);
      $("#disconnect").hidden = !connected;

      const roomList = $("#room-list");
      roomList.replaceChildren();
      const row = document.createElement("div");
      row.className = "room-row selected";
      row.setAttribute("aria-current", "true");
      row.innerHTML = '<svg class="people-icon" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><circle cx="9" cy="8" r="3.2"/><circle cx="17" cy="9" r="2.5"/><path d="M2.7 18.6c.3-3.2 2.4-5.1 6.3-5.1s6 1.9 6.3 5.1v1H2.7zm12.1-4.4c3.8-.6 6.3 1.1 6.5 4.3h-4.7c-.1-1.7-.7-3.2-1.8-4.3"/></svg><span>1/2</span><span class="room-separator"></span><span class="room-target"></span><span class="chevron">›</span>';
      row.querySelector(".room-target").textContent = `Contra Muggins · ${lobby.targetScore} pts`;
      roomList.append(row);

      if (hasMatch) {
        setLobbyMessage("Há uma partida em andamento nesta sessão. Continue de onde parou.");
      } else if (!connected) {
        setLobbyMessage("Conecte sua conta FIAP para preparar os perfis e abrir uma partida.");
      } else if (!lobby.canStartMachineMatch) {
        const missing = [!humanReady ? "seu perfil" : null, !botReady ? "perfil do Muggins" : null].filter(Boolean).join(" e ");
        setLobbyMessage(`Prepare ${missing} para começar. ${lobby.waitingMatches.length} partida(s) aguardando na API.`);
      } else {
        setLobbyMessage(`Perfis prontos. ${lobby.waitingMatches.length} partida(s) de outros jogadores aguardando na API.`);
      }
    }

    function setLobbyMessage(message) {
      $("#lobby-status").textContent = message;
    }
  }

  function initializeMatch() {
    let selectedIndex = null;
    let currentMatch = null;
    let loading = false;
    let actionInProgress = false;
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
        currentMatch = await callApi("/app-api/match");
        renderMatch(currentMatch);
        setMessage("");
      } catch (error) {
        setMessage(error.message);
        $("#sync-indicator").textContent = "!";
        $("#sync-label").textContent = "Sem sincronização";
        $("#sync-indicator").parentElement.style.background = "conic-gradient(#ff8e8e 0deg 360deg)";
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
        currentMatch = await callApi("/app-api/match/action", {
          method: "POST",
          body: { kind, ...details }
        });
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
      $("#opponent-label").textContent = `${state.botName || "Muggins"} · ${state.botScore} pts`;
      $("#opponent-count").textContent = state.botTileCount;
      $("#human-name").textContent = state.humanName || "Você";
      $("#human-score").textContent = state.humanScore;
      $("#human-count").textContent = state.humanTileCount;
      $("#turn-indicator").textContent = turnText(state);
      $("#sync-indicator").textContent = "●";
      $("#sync-label").textContent = "Estado confirmado pela API";
      $("#sync-indicator").parentElement.style.background = "conic-gradient(#66d99b 0deg 360deg)";
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
      const backCount = Math.min(7, state.botTileCount);
      for (let index = 0; index < backCount; index += 1) {
        const back = document.createElement("span");
        back.className = "back-tile";
        opponentHand.append(back);
      }
      opponentHand.setAttribute("aria-label", `${state.botTileCount} peças do oponente, viradas para baixo`);

      renderBoard(state);

      hand.replaceChildren();
      state.hand.forEach((piece, index) => {
        const button = createDomino(piece.valorA, piece.valorB, piece.valorA === piece.valorB, true);
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
      if (boardTiles.length === 0) return;

      const boardWidth = board.clientWidth || $("#board-zone").clientWidth * 0.94;
      const boardHeight = $("#board-zone").clientHeight;
      const tileGap = 4;
      const rowGap = 8;
      const availableHeight = Math.max(90, boardHeight * 0.84);
      const maxColumns = Math.max(1, Math.min(
        boardTiles.length,
        Math.floor((boardWidth + tileGap) / (38 + tileGap))
      ));
      let columns = 1;
      let rows = boardTiles.length;
      let tileWidth = 38;
      for (let candidateColumns = 1; candidateColumns <= maxColumns; candidateColumns += 1) {
        const candidateRows = Math.ceil(boardTiles.length / candidateColumns);
        const widthByColumns = (boardWidth - tileGap * (candidateColumns - 1)) / candidateColumns;
        let rowHeightFactor = 0;
        for (let rowIndex = 0; rowIndex < candidateRows; rowIndex += 1) {
          const rowStart = rowIndex * candidateColumns;
          const rowEnd = Math.min(rowStart + candidateColumns, boardTiles.length);
          const hasVerticalTile = boardTiles.slice(rowStart, rowEnd).some((piece) => piece.isDouble);
          rowHeightFactor += hasVerticalTile ? 1 : 66 / 116;
        }
        const widthByRows = (availableHeight - rowGap * (candidateRows - 1)) / rowHeightFactor;
        const candidateWidth = Math.min(116, widthByColumns, widthByRows);
        if (candidateWidth > tileWidth) {
          columns = candidateColumns;
          rows = candidateRows;
          tileWidth = candidateWidth;
        }
      }

      tileWidth = Math.max(38, tileWidth);
      const tileHeight = tileWidth * 66 / 116;
      board.style.setProperty("--board-tile-width", `${tileWidth}px`);
      board.style.setProperty("--board-tile-height", `${tileHeight}px`);
      board.style.setProperty("--board-pip-core", `${Math.max(1.7, tileWidth * 0.036)}px`);
      board.style.setProperty("--board-pip-halo", `${Math.max(2.3, tileWidth * 0.045)}px`);

      for (let rowIndex = 0; rowIndex < rows; rowIndex += 1) {
        const row = document.createElement("div");
        const reverse = rowIndex % 2 === 1;
        row.className = `board-row${reverse ? " reverse" : ""}`;
        row.setAttribute("role", "group");
        row.setAttribute("aria-label", `Linha ${rowIndex + 1} da sequência`);

        const start = rowIndex * columns;
        const end = Math.min(start + columns, boardTiles.length);
        for (let index = start; index < end; index += 1) {
          const piece = boardTiles[index];
          const firstValue = reverse ? piece.rightValue : piece.leftValue;
          const secondValue = reverse ? piece.leftValue : piece.rightValue;
          const tile = createDomino(firstValue, secondValue, piece.isDouble);
          const owner = piece.isBotTile ? "Muggins" : "Você";
          tile.setAttribute("aria-label", `${firstValue} e ${secondValue}, jogada por ${owner}`);
          tile.title = `Jogada de ${owner}`;
          tile.classList.add("board-tile");
          const badge = document.createElement("span");
          badge.className = `board-owner ${piece.isBotTile ? "bot" : "human"}`;
          badge.textContent = piece.isBotTile ? "M" : "EU";
          badge.setAttribute("aria-hidden", "true");
          tile.append(badge);
          row.append(tile);
        }

        board.append(row);
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
  }

  function createDomino(valueA, valueB, isDouble, button = false) {
    const element = document.createElement(button ? "button" : "div");
    element.className = `domino ${isDouble ? "vertical" : "horizontal"}${button ? " hand-tile" : ""}`;
    if (button) element.type = "button";
    element.setAttribute("aria-label", `${valueA} e ${valueB}`);
    for (const value of [valueA, valueB]) {
      const half = document.createElement("span");
      half.className = "half";
      half.dataset.v = String(value);
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
    return state.turnPlayerName ? `Vez de ${state.turnPlayerName}` : "Aguardando a partida";
  }
})();
