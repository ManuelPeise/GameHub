// DTO types are generated from Shared.Models (see generated/contracts.ts); do not define them here.
export type {
  AddAiSeatRequest,
  GameHistoryDetailDto,
  GameHistoryEntryDto,
  GameOutcome,
  HistoryActionDto,
  HistorySeatDto,
  CreateSessionRequest,
  GameMode,
  JoinSessionRequest,
  OpenLobbyDto,
  SeatConnectionStatus,
  SeatDto,
  SeatKind,
  SessionDto,
  SessionStatus,
  SubmitSessionActionRequest,
} from "./generated/contracts";

export const sessionUrls = {
  base: "/api/sessions",
  byId: (id: string) => `/api/sessions/${id}`,
  open: "/api/sessions/open",
  mine: "/api/sessions/mine",
  pause: (id: string) => `/api/sessions/${id}/pause`,
  resume: (id: string) => `/api/sessions/${id}/resume`,
  abandon: (id: string) => `/api/sessions/${id}/abandon`,
  history: "/api/history",
  historyDetail: (id: string) => `/api/history/${id}`,
  join: "/api/sessions/join",
  joinById: (id: string) => `/api/sessions/${id}/join`,
  aiSeats: (id: string) => `/api/sessions/${id}/ai-seats`,
  seat: (id: string, seatId: string) => `/api/sessions/${id}/seats/${seatId}`,
  start: (id: string) => `/api/sessions/${id}/start`,
  rematch: (id: string) => `/api/sessions/${id}/rematch`,
  actions: (id: string) => `/api/sessions/${id}/actions`,
} as const;

export const sessionPagePaths = {
  newSession: "/sessions/new",
  openLobbies: "/sessions/open",
  history: "/history",
  historyPrefix: "/history/",
  statistics: "/statistics",
  lobbyPrefix: "/lobby/",
  joinPrefix: "/join/",
} as const;

export const gameNames: Record<string, string> = {
  TicTacToe: "Tic-Tac-Toe",
  Kniffel: "Kniffel",
  Muehle: "Mühle",
};

/** Game types in the order they are offered. */
export const gameTypes = ["TicTacToe", "Kniffel", "Muehle"] as const;
export type GameType = (typeof gameTypes)[number];

/** Games with an AI opponent (Mühle follows with US-035). */
export const gamesWithAi: readonly string[] = ["TicTacToe", "Kniffel"];

/** Player count shown when creating a lobby. */
export const playerCountText: Record<string, string> = {
  TicTacToe: "Tic-Tac-Toe spielen 2 Personen",
  Kniffel: "Kniffel spielen 2 bis 4 Personen",
  Muehle: "Mühle spielen 2 Personen",
};

export const difficultyOptions = [
  { value: "Easy", label: "Einfach" },
  { value: "Medium", label: "Mittel" },
  { value: "Hard", label: "Schwer" },
] as const;

export const statusLabels: Record<string, string> = {
  Lobby: "Lobby",
  Running: "läuft",
  Paused: "pausiert",
  Finished: "beendet",
  Aborted: "abgebrochen",
};

export const outcomeLabels: Record<string, string> = {
  Win: "Gewonnen",
  Loss: "Verloren",
  Draw: "Unentschieden",
  Removed: "Entfernt",
};
