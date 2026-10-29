import React from "react";
import "./AccountPages.css";

/** Target of the confirmation link (via /api/account/confirm-email): shows the result. */
const EmailConfirmedPage: React.FC = () => {
  const success = new URLSearchParams(window.location.search).get("success") === "true";

  return (
    <main className="account-page">
      <h1>E-Mail-Bestätigung</h1>
      {success ? (
        <p className="account-success" role="status">
          Deine E-Mail-Adresse ist bestätigt. Dein Konto ist jetzt aktiv.
        </p>
      ) : (
        <p className="account-error" role="alert">
          Der Bestätigungslink ist ungültig oder abgelaufen.
        </p>
      )}
    </main>
  );
};

export default EmailConfirmedPage;
