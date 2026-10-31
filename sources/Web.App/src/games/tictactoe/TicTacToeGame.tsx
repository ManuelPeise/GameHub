import { alpha, type Theme } from "@mui/material/styles";
import React from "react";
import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import ButtonBase from "@mui/material/ButtonBase";
import Stack from "@mui/material/Stack";
import { ApiError, getErrorMessage, sendRequest } from "../../hooks/apiClient";
import { useGameSessionEvents } from "../../hooks/gameHub";
import { sessionUrls, type SessionDto, type SubmitSessionActionRequest } from "../../api/sessions";
import { GameEndActions, GameStatusBar } from "../GameParts";

type TicTacToeState = {
  board: string;
  nextSeat: number;
  status: "Running" | "Won" | "Draw";
  winnerSeat?: number | null;
  winningLine?: number[] | null;
};

type TicTacToeGameProps = {
  session: SessionDto;
  /** Called with the new session after a move or when the state has to be reloaded. */
  onChanged: (session?: SessionDto) => void;
};

const cellNames = ["oben links", "oben Mitte", "oben rechts", "Mitte links", "Mitte", "Mitte rechts", "unten links", "unten Mitte", "unten rechts"];

const parseState = (json: string): TicTacToeState | null => {
  try {
    const state = JSON.parse(json) as TicTacToeState;
    return typeof state.board === "string" && state.board.length === 9 ? state : null;
  } catch {
    return null;
  }
};

/** The Tic-Tac-Toe board (square, fills the phone width), status, result and a new round. */
const TicTacToeGame: React.FC<TicTacToeGameProps> = ({ session, onChanged }) => {
  const [isSubmitting, setIsSubmitting] = React.useState(false);
  const [aiThinkingSeat, setAiThinkingSeat] = React.useState<string | null>(null);
  const [error, setError] = React.useState<string | null>(null);
  const [rematchId, setRematchId] = React.useState<string | null>(null);

  useGameSessionEvents(session.id, {
    onAiThinking: (message) => setAiThinkingSeat(message.seatId),
    onActionApplied: () => setAiThinkingSeat(null),
    onRematchCreated: (message) => setRematchId(message.newSessionId),
  });

  const state = parseState(session.stateJson);
  if (!state) {
    return <Alert severity="error">Der Spielstand kann nicht angezeigt werden.</Alert>;
  }

  const mySeat = session.seats.find((seat) => seat.id === session.mySeatId);
  const currentSeat = session.seats.find((seat) => seat.id === session.currentSeatId);
  const isMyTurn = session.status === "Running" && state.status === "Running" && currentSeat !== undefined && currentSeat.id === mySeat?.id;
  const isEnded = session.status === "Finished" || session.status === "Aborted" || state.status !== "Running";
  const winningLine = state.winningLine ?? [];
  const winner = session.seats.find((seat) => seat.order === state.winnerSeat);

  const place = async (cell: number) => {
    if (!isMyTurn || isSubmitting || state.board[cell] !== "-") {
      return;
    }

    setIsSubmitting(true);
    setError(null);
    try {
      await sendRequest<unknown, SubmitSessionActionRequest>("POST", {
        serviceUrl: sessionUrls.actions(session.id),
        body: { expectedVersion: session.stateVersion, actionType: "PlaceMark", payload: JSON.stringify({ cell }) },
      });
      onChanged();
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 409) {
        // Someone (or the AI) was faster: load the current state.
        onChanged();
      } else {
        setError(getErrorMessage(failure, "Der Zug konnte nicht ausgeführt werden."));
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  const resultText = (() => {
    if (session.status === "Aborted") {
      return "Die Partie wurde abgebrochen.";
    }
    if (state.status === "Draw") {
      return "Unentschieden.";
    }
    if (state.status === "Won" && winner) {
      return winner.id === mySeat?.id ? "Du hast gewonnen!" : `${winner.displayName} hat gewonnen.`;
    }
    return mySeat?.connectionStatus === "Removed"
      ? "Du wurdest aus der Partie entfernt."
      : "Du hast gewonnen – dein Gegner wurde entfernt.";
  })();
  const isWin = (state.status === "Won" && winner?.id === mySeat?.id) || resultText.startsWith("Du hast gewonnen");

  const statusText = (() => {
    if (session.status === "Paused") {
      return "Die Partie ist pausiert.";
    }
    if (aiThinkingSeat && aiThinkingSeat === currentSeat?.id) {
      return "KI denkt …";
    }
    if (isMyTurn) {
      return `Du bist am Zug (${mySeat?.symbol ?? ""}).`;
    }
    return currentSeat ? `${currentSeat.displayName} ist am Zug.` : "";
  })();

  return (
    <Stack spacing={2} sx={{ width: "100%", maxWidth: 440, mx: "auto" }}>
      {!isEnded && (
        <GameStatusBar
          text={statusText}
          busy={Boolean(aiThinkingSeat && aiThinkingSeat === currentSeat?.id) || isSubmitting}
          highlight={isMyTurn}
          deadline={session.turnDeadline}
        />
      )}

      <Box
        role="grid"
        aria-label="Spielfeld"
        sx={{ display: "grid", gridTemplateColumns: "repeat(3, minmax(0, 1fr))", gridTemplateRows: "repeat(3, minmax(0, 1fr))", gap: { xs: 1, sm: 1.5 }, width: "100%", aspectRatio: "1" }}
      >
        {[...state.board].map((cell, index) => {
          const isEmpty = cell === "-";
          const isWinning = winningLine.includes(index);
          return (
            <ButtonBase
              key={index}
              role="gridcell"
              onClick={() => void place(index)}
              disabled={!isMyTurn || !isEmpty || isSubmitting}
              aria-label={`Feld ${cellNames[index]}, ${isEmpty ? "leer" : cell}`}
              sx={{
                minWidth: 0,
                minHeight: 0,
                overflow: "hidden",
                lineHeight: 1,
                borderRadius: 4,
                border: 3,
                boxShadow: "0 4px 0 rgba(43, 34, 80, 0.08)",
                borderColor: isWinning ? "secondary.main" : "divider",
                bgcolor: isWinning ? ((t: Theme) => alpha(t.palette.secondary.main, 0.18)) : "background.paper",
                fontSize: "clamp(2.5rem, 14vw, 4.5rem)",
                fontWeight: 900,
                color: cell === "X" ? "primary.main" : "secondary.main",
                transition: "border-color 120ms, background-color 120ms",
                "&:not(.Mui-disabled):hover": { borderColor: "primary.main", bgcolor: ((t: Theme) => alpha(t.palette.primary.main, 0.1)) },
                "&.Mui-focusVisible": { borderColor: "primary.main" },
              }}
            >
              {isEmpty ? "" : cell}
            </ButtonBase>
          );
        })}
      </Box>

      {error && <Alert severity="error">{error}</Alert>}

      {isEnded && <GameEndActions session={session} rematchId={rematchId} result={resultText} isWin={isWin} />}
    </Stack>
  );
};

export default TicTacToeGame;
