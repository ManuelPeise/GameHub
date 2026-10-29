export type GameMode = "Singleplayer" | "Multiplayer";
export type SessionStatus = "Lobby" | "Running" | "Paused" | "Finished" | "Aborted";
export type SeatKind = "Human" | "Ai";
export type SeatConnectionStatus = "Connected" | "Disconnected" | "Removed";

export type CreateSessionRequest = {
  gameType: string;
  mode: GameMode;
  difficulty?: string | null;
  preferredSeat?: number | null;
};

export type JoinSessionRequest = {
  inviteCode: string;
};

export type SubmitSessionActionRequest = {
  expectedVersion?: number | null;
  actionType: string;
  payload?: string | null;
};

export type SeatDto = {
  id: string;
  order: number;
  kind: SeatKind;
  displayName: string;
  symbol?: string | null;
  userId?: string | null;
  connectionStatus: SeatConnectionStatus;
};

export type SessionDto = {
  id: string;
  gameType: string;
  mode: GameMode;
  status: SessionStatus;
  stateVersion: number;
  stateJson: string;
  seats: SeatDto[];
  inviteCode?: string | null;
  mySeatId?: string | null;
};

export const sessionUrls = {
  base: "/api/sessions",
  byId: (id: string) => `/api/sessions/${id}`,
  join: "/api/sessions/join",
  start: (id: string) => `/api/sessions/${id}/start`,
  rematch: (id: string) => `/api/sessions/${id}/rematch`,
  actions: (id: string) => `/api/sessions/${id}/actions`,
} as const;

export const sessionPagePaths = {
  newSession: "/sessions/new",
  lobbyPrefix: "/lobby/",
  joinPrefix: "/join/",
} as const;
