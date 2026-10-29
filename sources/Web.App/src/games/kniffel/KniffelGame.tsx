import React from "react";
import TurnTimer from "../../components/TurnTimer";
import { ApiError, getErrorMessage, sendRequest } from "../../hooks/apiClient";
import { useGameSessionEvents } from "../../hooks/gameHub";
import {
  sessionPagePaths,
  sessionUrls,
  type SessionDto,
  type SubmitSessionActionRequest,
} from "../../api/sessions";
import KniffelSheetTable from "./KniffelSheetTable";
import { categories, maxRolls, parseKniffelState, total } from "./kniffel";
import "./KniffelGame.css";

type KniffelGameProps = {
  session: SessionDto;
  onChanged: (session?: SessionDto) => void;
};

const pips = ["", "⚀", "⚁", "⚂", "⚃", "⚄", "⚅"];

/** Kniffel table: dice with hold selection, roll button, all score sheets, result and new round. */
const KniffelGame: React.FC<KniffelGameProps> = ({ session, onChanged }) => {
  const [isSubmitting, setIsSubmitting] = React.useState(false);
  const [aiThinkingSeat, setAiThinkingSeat] = React.useState<string | null>(null);
  const [error, setError] = React.useState<string | null>(null);
  const [rematchId, setRematchId] = React.useState<string | null>(null);
  // Hold selection belongs to one state version; after every action the server state is the default again.
  const [hold, setHold] = React.useState<{ version: number; mask: number } | null>(null);

  useGameSessionEvents(session.id, {
    onAiThinking: (message) => setAiThinkingSeat(message.seatId),
    onActionApplied: () => setAiThinkingSeat(null),
    onRematchCreated: (message) => setRematchId(message.newSessionId),
  });

  const state = parseKniffelState(session.stateJson);
  if (!state) {
    return <p className="account-error">Der Spielstand kann nicht angezeigt werden.</p>;
  }

  const seats = [...session.seats].sort((a, b) => a.order - b.order);
  const mySeat = seats.find((seat) => seat.id === session.mySeatId);
  const currentSeat = seats.find((seat) => seat.order === state.currentSeat);
  const isRunning = session.status === "Running" && state.status === "Running";
  const isMyTurn = isRunning && mySeat !== undefined && currentSeat?.id === mySeat.id;
  const heldMask = hold && hold.version === session.stateVersion ? hold.mask : state.heldMask;
  const canHold = isMyTurn && state.rollsUsed > 0 && state.rollsUsed < maxRolls;

  const send = async (actionType: string, payload: unknown) => {
    setIsSubmitting(true);
    setError(null);
    try {
      await sendRequest<unknown, SubmitSessionActionRequest>("POST", {
        serviceUrl: sessionUrls.actions(session.id),
        body: { expectedVersion: session.stateVersion, actionType, payload: JSON.stringify(payload) },
      });
      onChanged();
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 409) {
        onChanged();
      } else {
        setError(getErrorMessage(failure, "Die Aktion konnte nicht ausgeführt werden."));
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  const toggle = (index: number) => {
    if (canHold) {
      setHold({ version: session.stateVersion, mask: heldMask ^ (1 << index) });
    }
  };

  const roll = () => void send("Roll", { heldMask: state.rollsUsed === 0 ? 0 : heldMask });
  const score = (categoryIndex: number) => void send("Score", { category: categories[categoryIndex] });

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

  const isEnded = session.status === "Finished" || session.status === "Aborted";
  const ranking = seats
    .filter((seat) => !state.removed[seat.order])
    .map((seat) => ({ seat, points: total(state.sheets[seat.order]) }))
    .sort((a, b) => b.points - a.points);

  const statusText = (() => {
    if (session.status === "Paused") {
      return "Die Partie ist pausiert.";
    }
    if (session.status === "Aborted") {
      return "Die Partie wurde abgebrochen.";
    }
    if (isEnded) {
      const best = ranking[0]?.points;
      const winners = ranking.filter((entry) => entry.points === best);
      if (winners.length > 1) {
        return "Unentschieden.";
      }
      return winners[0]?.seat.id === mySeat?.id ? "Du hast gewonnen!" : `${winners[0]?.seat.displayName ?? "–"} hat gewonnen.`;
    }
    if (aiThinkingSeat && aiThinkingSeat === currentSeat?.id) {
      return `${currentSeat.displayName} denkt …`;
    }
    if (isMyTurn) {
      return state.rollsUsed === 0
        ? "Du bist am Zug: würfle."
        : state.rollsUsed < maxRolls
          ? "Halte Würfel und würfle erneut oder werte eine Kategorie."
          : "Wähle eine Kategorie.";
    }
    return currentSeat ? `${currentSeat.displayName} ist am Zug.` : null;
  })();

  return (
    <section className="kniffel-game" aria-label="Kniffel">
      <p className="kniffel-status" role="status" aria-live="polite">
        {statusText} {isRunning && <TurnTimer deadline={session.turnDeadline} />}
      </p>

      {isRunning && (
        <div className="kniffel-table">
          <div className="kniffel-dice" role="group" aria-label="Würfel">
            {state.dice.map((die, index) => {
              const isHeld = state.rollsUsed > 0 && (heldMask & (1 << index)) !== 0;
              return (
                <button
                  key={index}
                  type="button"
                  className={`kniffel-die${isHeld ? " kniffel-die--held" : ""}`}
                  onClick={() => toggle(index)}
                  disabled={!canHold || isSubmitting}
                  aria-pressed={isHeld}
                  aria-label={die === 0 ? "noch nicht gewürfelt" : `Würfel ${die}${isHeld ? ", gehalten" : ""}`}
                >
                  {die === 0 ? "·" : pips[die]}
                </button>
              );
            })}
          </div>
          <button
            type="button"
            onClick={roll}
            disabled={!isMyTurn || state.rollsUsed >= maxRolls || isSubmitting || (state.rollsUsed > 0 && heldMask === 31)}
          >
            Würfeln (noch {maxRolls - state.rollsUsed})
          </button>
        </div>
      )}

      <KniffelSheetTable
        state={state}
        seats={seats}
        mySeatId={session.mySeatId}
        onScore={isMyTurn && state.rollsUsed > 0 && !isSubmitting ? score : undefined}
      />

      {error && (
        <p className="account-error" role="alert">
          {error}
        </p>
      )}

      {isEnded && (
        <>
          <ol className="kniffel-ranking" aria-label="Endwertung">
            {ranking.map((entry) => (
              <li key={entry.seat.id}>
                {entry.seat.id === mySeat?.id ? "Du" : entry.seat.displayName}: {entry.points} Punkte
              </li>
            ))}
            {seats
              .filter((seat) => state.removed[seat.order])
              .map((seat) => (
                <li key={seat.id} className="kniffel-removed">
                  {seat.displayName}: aus der Wertung gestrichen
                </li>
              ))}
          </ol>
          <div className="kniffel-actions">
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
        </>
      )}
    </section>
  );
};

export default KniffelGame;
