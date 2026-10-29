import React from "react";
import {
  gameNames,
  outcomeLabels,
  sessionPagePaths,
  sessionUrls,
  type GameHistoryDetailDto,
  type HistoryActionDto,
} from "../../api/sessions";
import { getErrorMessage, sendRequest } from "../../hooks/apiClient";
import "../account/AccountPages.css";
import "./HistoryPages.css";

type HistoryDetailPageProps = {
  sessionId: string;
};

const systemLabels: Record<string, string> = {
  Disconnected: "Verbindung unterbrochen",
  Reconnected: "wieder verbunden",
  PlayerRemoved: "aus der Partie entfernt",
  TurnTimeout: "Zugzeit abgelaufen",
  Paused: "Partie pausiert",
  Resumed: "Partie fortgesetzt",
};

const cellNames = ["oben links", "oben Mitte", "oben rechts", "Mitte links", "Mitte", "Mitte rechts", "unten links", "unten Mitte", "unten rechts"];

const parseJson = (json: string | null | undefined): Record<string, unknown> | null => {
  if (!json) {
    return null;
  }
  try {
    return JSON.parse(json) as Record<string, unknown>;
  } catch {
    return null;
  }
};

const describeAction = (action: HistoryActionDto): string => {
  if (systemLabels[action.actionType]) {
    const reason = parseJson(action.payloadJson)?.reason;
    return typeof reason === "string" ? `${systemLabels[action.actionType]} (${reason})` : systemLabels[action.actionType];
  }

  const cell = parseJson(action.payloadJson)?.cell;
  if (action.actionType === "PlaceMark" && typeof cell === "number") {
    return `setzt auf ${cellNames[cell] ?? `Feld ${cell}`}`;
  }

  const random = action.randomResultJson ? ` – Zufall: ${action.randomResultJson}` : "";
  return `${action.actionType}${action.payloadJson ? ` ${action.payloadJson}` : ""}${random}`;
};

/** Read-only view of a game state; Tic-Tac-Toe as board, other games as JSON until they have their own view. */
const StateView: React.FC<{ gameType: string; stateJson: string }> = ({ gameType, stateJson }) => {
  const state = parseJson(stateJson);
  const board = state?.board;
  if (gameType === "TicTacToe" && typeof board === "string") {
    const winningLine = state?.winningLine;
    const winning = Array.isArray(winningLine) ? (winningLine as number[]) : [];
    return (
      <div className="history-board" role="grid" aria-label="Spielfeld">
        {[...board].map((cell, index) => (
          <div
            key={index}
            role="gridcell"
            className={`history-cell${winning.includes(index) ? " history-cell--win" : ""}`}
            aria-label={`${cellNames[index]}: ${cell === "-" ? "leer" : cell}`}
          >
            {cell === "-" ? "" : cell}
          </div>
        ))}
      </div>
    );
  }

  return <pre className="history-json">{JSON.stringify(state, null, 2)}</pre>;
};

/** Move log of a finished game with a step-by-step replay (forward and back). */
const HistoryDetailPage: React.FC<HistoryDetailPageProps> = ({ sessionId }) => {
  const [detail, setDetail] = React.useState<GameHistoryDetailDto | null>(null);
  const [error, setError] = React.useState<string | null>(null);
  const [step, setStep] = React.useState(0);

  React.useEffect(() => {
    let cancelled = false;
    sendRequest<GameHistoryDetailDto>("GET", { serviceUrl: sessionUrls.historyDetail(sessionId) })
      .then((loaded) => {
        if (!cancelled && loaded) {
          setDetail(loaded);
          setStep(loaded.actions.length);
        }
      })
      .catch((failure: unknown) => {
        if (!cancelled) {
          setError(getErrorMessage(failure, "Die Partie konnte nicht geladen werden."));
        }
      });
    return () => {
      cancelled = true;
    };
  }, [sessionId]);

  if (!detail) {
    return (
      <main className="account-page">
        <h1>Partieverlauf</h1>
        <p>{error ?? "Lädt …"}</p>
      </main>
    );
  }

  const { entry, actions } = detail;
  const seatName = (seatId?: string | null) => entry.seats.find((seat) => seat.seatId === seatId)?.displayName ?? "System";
  const stateJson = step === 0 ? detail.initialStateJson : actions[step - 1].stateJson;

  return (
    <main className="account-page">
      <h1>{gameNames[entry.gameType] ?? entry.gameType}: Partieverlauf</h1>

      <section className="account-form">
        <h2>Ergebnis</h2>
        <ul>
          {entry.seats.map((seat) => (
            <li key={seat.seatId}>
              {seat.displayName}
              {seat.isMe && " (du)"}: {seat.outcome ? outcomeLabels[seat.outcome] : "–"}
              {seat.score !== null && seat.score !== undefined && `, ${seat.score} Punkte`}
            </li>
          ))}
        </ul>
      </section>

      <section className="account-form" aria-label="Nachspielen">
        <h2>
          Nachspielen – Schritt {step} von {actions.length}
        </h2>
        <StateView gameType={entry.gameType} stateJson={stateJson} />
        <div className="history-controls">
          <button type="button" onClick={() => setStep(0)} disabled={step === 0} aria-label="Zum Anfang">
            ⏮
          </button>
          <button type="button" onClick={() => setStep(step - 1)} disabled={step === 0} aria-label="Ein Schritt zurück">
            ◀
          </button>
          <input
            type="range"
            min={0}
            max={actions.length}
            value={step}
            onChange={(event) => setStep(Number(event.target.value))}
            aria-label="Schritt wählen"
          />
          <button type="button" onClick={() => setStep(step + 1)} disabled={step === actions.length} aria-label="Ein Schritt vor">
            ▶
          </button>
          <button type="button" onClick={() => setStep(actions.length)} disabled={step === actions.length} aria-label="Zum Ende">
            ⏭
          </button>
        </div>
      </section>

      <section className="account-form">
        <h2>Alle Aktionen</h2>
        <ol className="history-actions">
          {actions.map((action, index) => (
            <li key={action.sequence} className={index + 1 === step ? "history-action--current" : undefined}>
              <button type="button" className="history-action-link" onClick={() => setStep(index + 1)}>
                {new Date(action.createdAt).toLocaleTimeString("de-DE")} – {seatName(action.seatId)}: {describeAction(action)}
              </button>
            </li>
          ))}
        </ol>
      </section>

      <p className="account-links">
        <a href={sessionPagePaths.history}>Zum Spielverlauf</a>
      </p>
    </main>
  );
};

export default HistoryDetailPage;
