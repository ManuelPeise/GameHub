import React from "react";
import { ApiError, sendRequest } from "../../hooks/apiClient";
import { accountUrls, pagePaths, type EmailRequest, type MessageResponse } from "../../api/account";
import "./AccountPages.css";

const ForgotPasswordPage: React.FC = () => {
  const [email, setEmail] = React.useState("");
  const [message, setMessage] = React.useState<string | null>(null);
  const [error, setError] = React.useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = React.useState(false);

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setIsSubmitting(true);
    setError(null);

    try {
      const response = await sendRequest<MessageResponse, EmailRequest>("POST", {
        serviceUrl: accountUrls.forgotPassword,
        body: { email },
      });
      setMessage(response?.message ?? "Bitte prüfe dein E-Mail-Postfach.");
    } catch (caught) {
      setError(
        caught instanceof ApiError && caught.status === 429
          ? "Zu viele Anfragen. Bitte warte eine Minute."
          : "Die Anfrage ist fehlgeschlagen. Bitte versuche es erneut.",
      );
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <main className="account-page">
      <h1>Passwort vergessen</h1>
      {message ? (
        <p className="account-success" role="status">
          {message}
        </p>
      ) : (
        <form className="account-form" onSubmit={submit} noValidate>
          <p>Gib deine E-Mail-Adresse ein. Wir schicken dir einen Link, mit dem du ein neues Passwort festlegen kannst.</p>
          <div className="account-field">
            <label htmlFor="email">E-Mail-Adresse</label>
            <input
              id="email"
              type="email"
              autoComplete="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              required
            />
          </div>
          {error && (
            <p className="account-error" role="alert">
              {error}
            </p>
          )}
          <button type="submit" disabled={isSubmitting}>
            {isSubmitting ? "Wird gesendet …" : "Link anfordern"}
          </button>
        </form>
      )}
      <p className="account-links">
        <a href={pagePaths.login}>Zur Anmeldung</a>
      </p>
    </main>
  );
};

export default ForgotPasswordPage;
