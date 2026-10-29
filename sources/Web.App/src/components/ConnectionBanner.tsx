import React from "react";
import { useHubConnectionStatus } from "../hooks/gameHub";

/** Shows the state of the real-time connection while it is not connected. */
const ConnectionBanner: React.FC = () => {
  const status = useHubConnectionStatus();

  if (status.state === "reconnecting") {
    return (
      <p className="account-error" role="status">
        Verbindung unterbrochen – Wiederverbindung (Versuch {status.attempt} von {status.maxAttempts}) …
      </p>
    );
  }

  if (status.state === "disconnected") {
    return (
      <p className="account-error" role="alert">
        Die Verbindung ist verloren.{" "}
        <button type="button" onClick={() => window.location.reload()}>
          Neu laden
        </button>
      </p>
    );
  }

  return null;
};

export default ConnectionBanner;
