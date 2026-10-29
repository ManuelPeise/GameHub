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
  type RecoveryCodeLoginRequest,
  type TwoFactorLoginRequest,
} from "../../api/account";
import "./AccountPages.css";

type Step = "password" | "twoFactor" | "recoveryCode";

type LoginProblem = { title?: string; code?: LoginErrorCode } | null;

const LoginPage: React.FC = () => {
  const [step, setStep] = React.useState<Step>("password");
  const [form, setForm] = React.useState<LoginRequest>({ email: "", password: "", rememberMe: false });
  const [code, setCode] = React.useState("");
  const [error, setError] = React.useState<string | null>(null);
  const [errorCode, setErrorCode] = React.useState<LoginErrorCode | null>(null);
  const [info, setInfo] = React.useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = React.useState(false);

  const handleFailure = (caught: unknown) => {
    if (caught instanceof ApiError && caught.status === 401) {
      const body = caught.body as LoginProblem;
      if (body?.code === "two_factor_required") {
        setStep("twoFactor");
        setError(null);
        setErrorCode(null);
        return;
      }
      setError(body?.title ?? "Die Anmeldung ist fehlgeschlagen.");
      setErrorCode(body?.code ?? null);
    } else if (caught instanceof ApiError && caught.status === 429) {
      setError("Zu viele Anmeldeversuche. Bitte warte eine Minute.");
    } else {
      setError("Die Anmeldung ist fehlgeschlagen. Bitte versuche es erneut.");
    }
  };

  const run = async (request: () => Promise<unknown>) => {
    setIsSubmitting(true);
    setError(null);
    setErrorCode(null);
    setInfo(null);
    try {
      await request();
      window.location.assign(pagePaths.home);
    } catch (caught) {
      handleFailure(caught);
    } finally {
      setIsSubmitting(false);
    }
  };

  const submitPassword = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    void run(() =>
      sendRequest<CurrentUserResponse, LoginRequest>("POST", { serviceUrl: accountUrls.login, body: form }),
    );
  };

  const submitCode = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    void run(() =>
      step === "twoFactor"
        ? sendRequest<CurrentUserResponse, TwoFactorLoginRequest>("POST", {
            serviceUrl: accountUrls.loginTwoFactor,
            body: { code, rememberMe: form.rememberMe },
          })
        : sendRequest<CurrentUserResponse, RecoveryCodeLoginRequest>("POST", {
            serviceUrl: accountUrls.loginRecovery,
            body: { recoveryCode: code },
          }),
    );
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

  const messages = (
    <>
      {error && (
        <p className="account-error" role="alert">
          {error}
        </p>
      )}
      {info && (
        <p className="account-success" role="status">
          {info}
        </p>
      )}
    </>
  );

  if (step !== "password") {
    const isRecovery = step === "recoveryCode";
    return (
      <main className="account-page">
        <h1>Anmelden</h1>
        <form className="account-form" onSubmit={submitCode} noValidate>
          <p>
            {isRecovery
              ? "Gib einen deiner Wiederherstellungscodes ein. Jeder Code funktioniert nur einmal."
              : "Gib den sechsstelligen Code aus deiner Authenticator-App ein."}
          </p>
          <div className="account-field">
            <label htmlFor="code">{isRecovery ? "Wiederherstellungscode" : "Code"}</label>
            <input
              id="code"
              type="text"
              inputMode={isRecovery ? "text" : "numeric"}
              autoComplete="one-time-code"
              value={code}
              onChange={(event) => setCode(event.target.value)}
              autoFocus
              required
            />
          </div>
          {messages}
          <button type="submit" disabled={isSubmitting}>
            {isSubmitting ? "Wird geprüft …" : "Bestätigen"}
          </button>
          <button
            type="button"
            className="account-link-button"
            onClick={() => {
              setStep(isRecovery ? "twoFactor" : "recoveryCode");
              setCode("");
              setError(null);
            }}
          >
            {isRecovery ? "Code aus der Authenticator-App verwenden" : "Keinen Zugriff auf die App? Wiederherstellungscode verwenden"}
          </button>
        </form>
      </main>
    );
  }

  return (
    <main className="account-page">
      <h1>Anmelden</h1>
      <form className="account-form" onSubmit={submitPassword} noValidate>
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

        {messages}
        {errorCode === "email_not_confirmed" && (
          <button type="button" className="account-link-button" onClick={() => void resendConfirmation()}>
            Bestätigungslink erneut senden
          </button>
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
