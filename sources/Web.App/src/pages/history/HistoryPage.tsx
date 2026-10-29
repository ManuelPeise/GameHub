import React from "react";
import { pagePaths } from "../../api/account";
import {
  gameNames,
  outcomeLabels,
  sessionPagePaths,
  sessionUrls,
  statusLabels,
  type GameHistoryEntryDto,
} from "../../api/sessions";
import { getErrorMessage, sendRequest } from "../../hooks/apiClient";
import "../account/AccountPages.css";

const pageSize = 20;

const formatDate = (value: string) =>
  new Date(value).toLocaleString("de-DE", {
    dateStyle: "medium",
    timeStyle: "short",
  });

/** List of the own finished and aborted games. */
const HistoryPage: React.FC = () => {
  const [entries, setEntries] = React.useState<GameHistoryEntryDto[] | null>(
    null,
  );
  const [hasMore, setHasMore] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);

  const load = React.useCallback(async (skip: number) => {
    try {
      const page =
        (await sendRequest<GameHistoryEntryDto[]>("GET", {
          serviceUrl: sessionUrls.history,
          params: { skip, take: pageSize },
        })) ?? [];
      setEntries((previous) =>
        skip === 0 ? page : [...(previous ?? []), ...page],
      );
      setHasMore(page.length === pageSize);
    } catch (failure) {
      setError(
        getErrorMessage(
          failure,
          "Der Spielverlauf konnte nicht geladen werden.",
        ),
      );
    }
  }, []);

  React.useEffect(() => {
    const onLoad = async () => {
      await load(0);
    };
    void onLoad();
  }, [load]);

  return (
    <main className="account-page">
      <h1>Spielverlauf</h1>

      {error && (
        <p className="account-error" role="alert">
          {error}
        </p>
      )}
      {entries === null && !error && <p>Lädt …</p>}
      {entries?.length === 0 && <p>Du hast noch keine beendeten Partien.</p>}

      {entries && entries.length > 0 && (
        <ul className="account-form">
          {entries.map((entry) => {
            const opponents = entry.seats
              .filter((seat) => !seat.isMe)
              .map((seat) => seat.displayName)
              .join(", ");
            const result =
              entry.status === "Aborted"
                ? statusLabels.Aborted
                : entry.myOutcome
                  ? outcomeLabels[entry.myOutcome]
                  : "–";
            return (
              <li key={entry.sessionId}>
                <a href={`${sessionPagePaths.historyPrefix}${entry.sessionId}`}>
                  {gameNames[entry.gameType] ?? entry.gameType}
                </a>{" "}
                (
                {entry.mode === "Singleplayer" ? "Singleplayer" : "Multiplayer"}
                ) gegen {opponents || "–"}: <strong>{result}</strong>
                {" – "}
                {formatDate(entry.endedAt)}
              </li>
            );
          })}
        </ul>
      )}

      {hasMore && entries && (
        <button type="button" onClick={() => void load(entries.length)}>
          Weitere laden
        </button>
      )}

      <p className="account-links">
        <a href={pagePaths.home}>Zur Startseite</a>
      </p>
    </main>
  );
};

export default HistoryPage;
