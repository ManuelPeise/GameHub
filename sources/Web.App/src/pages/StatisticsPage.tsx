import React from "react";
import { pagePaths } from "../api/account";
import type {
  GameMode,
  LeaderboardDto,
  PlayerStatisticsDto,
  StatisticsRowDto,
} from "../api/generated/contracts";
import { difficultyOptions, gameNames } from "../api/sessions";
import { getErrorMessage, sendRequest } from "../hooks/apiClient";
import "./account/AccountPages.css";
import "./StatisticsPage.css";

const games = ["TicTacToe", "Kniffel"] as const;
type Period = "all" | "30d";

const difficultyLabel = (value?: string | null) =>
  difficultyOptions.find((option) => option.value === value)?.label ?? value ?? "";

const modeLabel = (mode: GameMode, difficulty?: string | null) =>
  mode === "Multiplayer" ? "Mehrspieler" : `gegen KI (${difficultyLabel(difficulty)})`;

const percent = (value: number) => `${Math.round(value * 100)} %`;

/** Personal statistics per game and the multiplayer leaderboard. */
const StatisticsPage: React.FC = () => {
  const [personal, setPersonal] = React.useState<PlayerStatisticsDto | null>(null);
  const [board, setBoard] = React.useState<LeaderboardDto | null>(null);
  const [game, setGame] = React.useState<(typeof games)[number]>("TicTacToe");
  const [period, setPeriod] = React.useState<Period>("all");
  const [error, setError] = React.useState<string | null>(null);

  React.useEffect(() => {
    sendRequest<PlayerStatisticsDto>("GET", { serviceUrl: "/api/statistics/me" })
      .then(setPersonal)
      .catch((failure: unknown) => setError(getErrorMessage(failure, "Die Statistik konnte nicht geladen werden.")));
  }, []);

  React.useEffect(() => {
    let cancelled = false;
    sendRequest<LeaderboardDto>("GET", { serviceUrl: "/api/statistics/leaderboard", params: { gameType: game, period } })
      .then((loaded) => {
        if (!cancelled) {
          setBoard(loaded);
        }
      })
      .catch((failure: unknown) => {
        if (!cancelled) {
          setError(getErrorMessage(failure, "Die Bestenliste konnte nicht geladen werden."));
        }
      });
    return () => {
      cancelled = true;
    };
  }, [game, period]);

  const rowsFor = (gameType: string): StatisticsRowDto[] => personal?.rows.filter((row) => row.gameType === gameType) ?? [];

  return (
    <main className="account-page statistics-page">
      <h1>Statistik</h1>

      {error && (
        <p className="account-error" role="alert">
          {error}
        </p>
      )}

      <section className="account-form">
        <h2>Meine Ergebnisse</h2>
        {personal === null && !error && <p>Lädt …</p>}
        {personal?.rows.length === 0 && <p>Noch keine beendeten Partien.</p>}
        {games
          .filter((gameType) => rowsFor(gameType).length > 0)
          .map((gameType) => (
            <table key={gameType} className="statistics-table">
              <caption>{gameNames[gameType]}</caption>
              <thead>
                <tr>
                  <th scope="col">Modus</th>
                  <th scope="col">Partien</th>
                  <th scope="col">Siege</th>
                  <th scope="col">Niederlagen</th>
                  <th scope="col">Unentschieden</th>
                  {gameType === "Kniffel" && (
                    <>
                      <th scope="col">Ø Punkte</th>
                      <th scope="col">Bestwert</th>
                    </>
                  )}
                </tr>
              </thead>
              <tbody>
                {rowsFor(gameType).map((row) => (
                  <tr key={`${row.mode}-${row.aiDifficulty ?? ""}`}>
                    <th scope="row">{modeLabel(row.mode, row.aiDifficulty)}</th>
                    <td>{row.games}</td>
                    <td>{row.wins}</td>
                    <td>{row.losses}</td>
                    <td>{row.draws}</td>
                    {gameType === "Kniffel" && (
                      <>
                        <td>{row.averageScore ?? "–"}</td>
                        <td>{row.bestScore ?? "–"}</td>
                      </>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          ))}
        <p className="statistics-note">Aus der Partie entfernt zählt als Niederlage.</p>
      </section>

      <section className="account-form">
        <h2>Bestenliste (Mehrspieler)</h2>
        <div className="statistics-filters">
          <label>
            Spiel{" "}
            <select value={game} onChange={(event) => setGame(event.target.value as (typeof games)[number])}>
              {games.map((gameType) => (
                <option key={gameType} value={gameType}>
                  {gameNames[gameType]}
                </option>
              ))}
            </select>
          </label>
          <label>
            Zeitraum{" "}
            <select value={period} onChange={(event) => setPeriod(event.target.value as Period)}>
              <option value="all">Gesamt</option>
              <option value="30d">Letzte 30 Tage</option>
            </select>
          </label>
        </div>

        {board && board.entries.length === 0 && <p>Noch keine Siege in diesem Zeitraum.</p>}
        {board && board.entries.length > 0 && (
          <table className="statistics-table">
            <caption>Meiste Siege, dann Siegquote</caption>
            <thead>
              <tr>
                <th scope="col">Platz</th>
                <th scope="col">Spieler</th>
                <th scope="col">Siege</th>
                <th scope="col">Partien</th>
                <th scope="col">Siegquote</th>
              </tr>
            </thead>
            <tbody>
              {board.entries.map((entry, index) => (
                <tr key={index} className={entry.isMe ? "statistics-me" : undefined}>
                  <td>{entry.rank}.</td>
                  <th scope="row">{entry.isMe ? `${entry.displayName} (du)` : entry.displayName}</th>
                  <td>{entry.wins}</td>
                  <td>{entry.games}</td>
                  <td>{percent(entry.winRate)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}

        {board && game === "Kniffel" && board.highScores.length > 0 && (
          <table className="statistics-table">
            <caption>Höchste Punktzahlen</caption>
            <thead>
              <tr>
                <th scope="col">Platz</th>
                <th scope="col">Spieler</th>
                <th scope="col">Punkte</th>
                <th scope="col">Datum</th>
              </tr>
            </thead>
            <tbody>
              {board.highScores.map((score, index) => (
                <tr key={index} className={score.isMe ? "statistics-me" : undefined}>
                  <td>{score.rank}.</td>
                  <th scope="row">{score.isMe ? `${score.displayName} (du)` : score.displayName}</th>
                  <td>{score.score}</td>
                  <td>{new Date(score.achievedAt).toLocaleDateString("de-DE")}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>

      <p className="account-links">
        <a href={pagePaths.home}>Zur Startseite</a>
      </p>
    </main>
  );
};

export default StatisticsPage;
