import React from "react";
import { ApiError, sendRequest } from "../../hooks/apiClient";
import {
  accountUrls,
  pagePaths,
  type CurrentUserResponse,
  type EmailRequest,
  type LoginErrorCode,
  type LoginRequest,
  type MessageResponse,
} from "../../api/account";
import "./AccountPages.css";

const LoginPage: React.FC = () => {
  const [form, setForm] = React.useState<LoginRequest>({ email: "", password: "", rememberMe: false });
  const [error, setError] = React.useState<string | null>(null);
  const [errorCode, setErrorCode] = React.useState<LoginErrorCode | null>(null);
  const [info, setInfo] = React.useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = React.useState(false);

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setIsSubmitting(true);
    setError(null);
    setErrorCode(null);
    setInfo(null);

    try {
      await sendRequest<CurrentUserResponse, LoginRequest>("POST", {
        serviceUrl: accountUrls.login,
        body: form,
      });
      window.location.assign(pagePaths.home);
    } catch (caught) {
      if (caught instanceof ApiError && caught.status === 401) {
        const body = caught.body as { title?: string; code?: LoginErrorCode } | null;
        setError(body?.title ?? "Die Anmeldung ist fehlgeschlagen.");
        setErrorCode(body?.code ?? null);
      } else if (caught instanceof ApiError && caught.status === 429) {
        setError("Zu viele Anmeldeversuche. Bitte warte eine Minute.");
      } else {
        setError("Die Anmeldung ist fehlgeschlagen. Bitte versuche es erneut.");
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  const resendConfirmation = async () => {
    try {
      const response = await sendRequest<MessageResponse, EmailRequest>("POST", {
        serviceUrl: accountUrls.resendConfirmation,
        body: { email: form.email },
      });
      setInfo(response?.message ?? "Bitte prüfe dein E-Mail-Postfach.");
      setError(null);
      setErrorCode(null);
    } catch {
      setError("Der Bestätigungslink konnte nicht angefordert werden. Bitte versuche es später erneut.");
    }
  };

  return (
    <main className="account-page">
      <h1>Anmelden</h1>
      <form className="account-form" onSubmit={submit} noValidate>
        <div className="account-field">
          <label htmlFor="email">E-Mail-Adresse</label>
          <input
            id="email"
            type="email"
            autoComplete="username"
            value={form.email}
            onChange={(event) => setForm({ ...form, email: event.target.value })}
            required
          />
        </div>

        <div className="account-field">
          <label htmlFor="password">Passwort</label>
          <input
            id="password"
            type="password"
            autoComplete="current-password"
            value={form.password}
            onChange={(event) => setForm({ ...form, password: event.target.value })}
            required
          />
        </div>

        <label className="account-check">
          <input
            type="checkbox"
            checked={form.rememberMe}
            onChange={(event) => setForm({ ...form, rememberMe: event.target.checked })}
          />
          Angemeldet bleiben
        </label>

        {error && (
          <p className="account-error" role="alert">
            {error}
          </p>
        )}
        {errorCode === "email_not_confirmed" && (
          <button type="button" className="account-link-button" onClick={() => void resendConfirmation()}>
            Bestätigungslink erneut senden
          </button>
        )}
        {info && (
          <p className="account-success" role="status">
            {info}
          </p>
        )}

        <button type="submit" disabled={isSubmitting}>
          {isSubmitting ? "Wird angemeldet …" : "Anmelden"}
        </button>
      </form>

      <p className="account-links">
        <a href={pagePaths.forgotPassword}>Passwort vergessen?</a>
        <a href={pagePaths.register}>Neues Konto anlegen</a>
      </p>
    </main>
  );
};

export default LoginPage;
