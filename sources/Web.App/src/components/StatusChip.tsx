import Chip from "@mui/material/Chip";
import type { SessionStatus } from "../api/sessions";

const variants: Record<SessionStatus, { label: string; color: "default" | "primary" | "success" | "warning" | "error" | "info" }> = {
  Lobby: { label: "Lobby", color: "info" },
  Running: { label: "läuft", color: "primary" },
  Paused: { label: "pausiert", color: "warning" },
  Finished: { label: "beendet", color: "success" },
  Aborted: { label: "abgebrochen", color: "default" },
};

/** Status of a session as colored chip. */
const StatusChip = ({ status }: { status: SessionStatus }) => (
  <Chip size="small" label={variants[status].label} color={variants[status].color} variant="outlined" />
);

export default StatusChip;
