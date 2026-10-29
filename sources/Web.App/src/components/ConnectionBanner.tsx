import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import LinearProgress from "@mui/material/LinearProgress";
import { useHubConnectionStatus } from "../hooks/gameHub";

/** Shows the state of the real-time connection while it is not connected. */
const ConnectionBanner = () => {
  const status = useHubConnectionStatus();

  if (status.state === "reconnecting") {
    return (
      <Alert severity="warning" role="status">
        Verbindung unterbrochen – Wiederverbindung (Versuch {status.attempt} von {status.maxAttempts}) …
        <LinearProgress color="warning" sx={{ mt: 1 }} />
      </Alert>
    );
  }

  if (status.state === "disconnected") {
    return (
      <Alert
        severity="error"
        role="alert"
        action={
          <Button color="inherit" size="small" onClick={() => window.location.reload()}>
            Neu laden
          </Button>
        }
      >
        Die Verbindung ist verloren.
      </Alert>
    );
  }

  return null;
};

export default ConnectionBanner;
