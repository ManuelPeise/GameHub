// DTO types are generated from Shared.Models (see generated/contracts.ts); do not define them here.
export type {
  AddAiSeatRequest,
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
  lobbyPrefix: "/lobby/",
  joinPrefix: "/join/",
} as const;

export const gameNames: Record<string, string> = {
  TicTacToe: "Tic-Tac-Toe",
  Kniffel: "Kniffel",
};

export const difficultyOptions = [
  { value: "Easy", label: "Einfach" },
  { value: "Medium", label: "Mittel" },
  { value: "Hard", label: "Schwer" },
] as const;
