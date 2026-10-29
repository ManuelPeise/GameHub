import React from "react";
import TurnTimer from "../../components/TurnTimer";
import { getErrorMessage, sendRequest, ApiError } from "../../hooks/apiClient";
import { useGameSessionEvents } from "../../hooks/gameHub";
import {
  sessionPagePaths,
  sessionUrls,
  type SessionDto,
  type SubmitSessionActionRequest,
} from "../../api/sessions";
import "./TicTacToeGame.css";

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

/** The Tic-Tac-Toe board with moves, "KI denkt …", result and a new round. */
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
    return <p className="account-error">Der Spielstand kann nicht angezeigt werden.</p>;
  }

  const mySeat = session.seats.find((seat) => seat.id === session.mySeatId);
  const currentSeat = session.seats.find((seat) => seat.id === session.currentSeatId);
  const isMyTurn =
    session.status === "Running" && state.status === "Running" && currentSeat !== undefined && currentSeat.id === mySeat?.id;
  const isFinished = session.status === "Finished" || state.status !== "Running";
  const winningLine = state.winningLine ?? [];

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

  const rematch = async () => {
    setError(null);
    try {
      const next = await sendRequest<SessionDto>("POST", { serviceUrl: sessionUrls.rematch(session.id) });
      if (next) {
        window.location.assign(`${sessionPagePaths.lobbyPrefix}${next.id}`);
      }
    } catch (failure) {
      setError(getErrorMessage(failure, "Die neue Runde konnte nicht gestartet werden."));
    }
  };

  const resultText = (() => {
    if (session.status === "Aborted") {
      return "Die Partie wurde abgebrochen.";
    }
    if (state.status === "Draw") {
      return "Unentschieden.";
    }
    const winner = session.seats.find((seat) => seat.order === state.winnerSeat);
    if (state.status === "Won" && winner) {
      return winner.id === mySeat?.id ? "Du hast gewonnen!" : `${winner.displayName} hat gewonnen.`;
    }
    // Finished without a winner on the board: the other player was removed.
    if (session.status === "Finished") {
      return mySeat?.connectionStatus === "Removed" ? "Du wurdest aus der Partie entfernt." : "Du hast gewonnen – dein Gegner wurde entfernt.";
    }
    return null;
  })();

  const statusText = (() => {
    if (session.status === "Paused") {
      return "Die Partie ist pausiert.";
    }
    if (isFinished) {
      return resultText;
    }
    if (aiThinkingSeat && aiThinkingSeat === currentSeat?.id) {
      return "KI denkt …";
    }
    if (isMyTurn) {
      return `Du bist am Zug (${mySeat?.symbol ?? ""}).`;
    }
    return currentSeat ? `${currentSeat.displayName} ist am Zug.` : null;
  })();

  return (
    <section className="ttt-game" aria-label="Tic-Tac-Toe">
      <p className="ttt-status" role="status" aria-live="polite">
        {statusText} {!isFinished && <TurnTimer deadline={session.turnDeadline} />}
      </p>

      <div className="ttt-board" role="grid" aria-label="Spielfeld">
        {[...state.board].map((cell, index) => {
          const isEmpty = cell === "-";
          return (
            <button
              key={index}
              type="button"
              role="gridcell"
              className={`ttt-cell${winningLine.includes(index) ? " ttt-cell--win" : ""}`}
              onClick={() => void place(index)}
              disabled={!isMyTurn || !isEmpty || isSubmitting}
              aria-label={`Feld ${cellNames[index]}, ${isEmpty ? "leer" : cell}`}
            >
              {isEmpty ? "" : cell}
            </button>
          );
        })}
      </div>

      {error && (
        <p className="account-error" role="alert">
          {error}
        </p>
      )}

      {(session.status === "Finished" || session.status === "Aborted") && (
        <div className="ttt-actions">
          {rematchId ? (
            <>
              <span>Eine neue Runde wurde gestartet.</span>
              <a href={`${sessionPagePaths.lobbyPrefix}${rematchId}`}>Zur neuen Runde</a>
            </>
          ) : (
            <button type="button" onClick={() => void rematch()}>
              Neue Runde
            </button>
          )}
          <a href={`${sessionPagePaths.historyPrefix}${session.id}`}>Verlauf ansehen</a>
        </div>
      )}
    </section>
  );
};

export default TicTacToeGame;
