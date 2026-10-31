/** URLs of the admin endpoints (/api/admin, policy Administrator). */
export const adminUrls = {
  status: "/api/admin/status",
  users: "/api/admin/users",
  lock: (id: string) => `/api/admin/users/${id}/lock`,
  unlock: (id: string) => `/api/admin/users/${id}/unlock`,
  remove: (id: string) => `/api/admin/users/${id}/delete`,
  appLog: "/api/admin/logs/app",
  auditLog: "/api/admin/logs/audit",
  games: "/api/admin/games",
  game: (id: string) => `/api/admin/games/${id}`,
} as const;

/** Page path of a game in the admin area (replay of any game). */
export const adminGamePath = (id: string) => `/admin/games/${id}`;

export const formatDateTime = (value?: string | null) =>
  value ? new Date(value).toLocaleString("de-DE", { dateStyle: "medium", timeStyle: "medium" }) : "–";

/** Converts the value of an <input type="datetime-local"> to ISO with time zone (empty = no filter). */
export const toIso = (local: string) => (local ? new Date(local).toISOString() : "");

/** Pretty-prints a JSON string (log properties, audit details); falls back to the raw text. */
export const prettyJson = (json?: string | null) => {
  if (!json) {
    return "";
  }
  try {
    return JSON.stringify(JSON.parse(json), null, 2);
  } catch {
    return json;
  }
};
