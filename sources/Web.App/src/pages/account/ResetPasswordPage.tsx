import React from "react";
import { getValidationErrors, sendRequest } from "../../hooks/apiClient";
import { useStatefulApi } from "../../hooks/useStatefulApi";
import {
  accountUrls,
  pagePaths,
  type PasswordRulesResponse,
  type ResetPasswordRequest,
} from "../../api/account";
import "./AccountPages.css";

/** Target of the link in the password reset mail (?userId=...&token=...). */
const ResetPasswordPage: React.FC = () => {
  const query = new URLSearchParams(window.location.search);
  const userId = query.get("userId") ?? "";
  const token = query.get("token") ?? "";

  const passwordRules = useStatefulApi<PasswordRulesResponse>({ serviceUrl: accountUrls.passwordRules });
  const loadPasswordRules = passwordRules.get;

  const [newPassword, setNewPassword] = React.useState("");
  const [errors, setErrors] = React.useState<string[]>([]);
  const [done, setDone] = React.useState(false);
  const [isSubmitting, setIsSubmitting] = React.useState(false);

  React.useEffect(() => {
    void loadPasswordRules();
  }, [loadPasswordRules]);

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setIsSubmitting(true);
    setErrors([]);

    try {
      await sendRequest<null, ResetPasswordRequest>("POST", {
        serviceUrl: accountUrls.resetPassword,
        body: { userId, token, newPassword },
      });
      setDone(true);
    } catch (caught) {
      const fieldErrors = getValidationErrors(caught);
      const messages = [...(fieldErrors.token ?? []), ...(fieldErrors.newPassword ?? [])];
      setErrors(messages.length > 0 ? messages : ["Das Passwort konnte nicht geändert werden. Bitte versuche es erneut."]);
    } finally {
      setIsSubmitting(false);
    }
  };

  if (!userId || !token) {
    return (
      <main className="account-page">
        <h1>Neues Passwort</h1>
        <p className="account-error" role="alert">
          Der Link ist unvollständig. Bitte fordere einen neuen an.
        </p>
        <p className="account-links">
          <a href={pagePaths.forgotPassword}>Neuen Link anfordern</a>
        </p>
      </main>
    );
  }

  return (
    <main className="account-page">
      <h1>Neues Passwort</h1>
      {done ? (
        <>
          <p className="account-success" role="status">
            Dein Passwort wurde geändert. Du kannst dich jetzt anmelden.
          </p>
          <p className="account-links">
            <a href={pagePaths.login}>Zur Anmeldung</a>
          </p>
        </>
      ) : (
        <form className="account-form" onSubmit={submit} noValidate>
          <div className="account-field">
            <label htmlFor="newPassword">Neues Passwort</label>
            <input
              id="newPassword"
              type="password"
              autoComplete="new-password"
              value={newPassword}
              onChange={(event) => setNewPassword(event.target.value)}
              aria-invalid={errors.length > 0 ? true : undefined}
              aria-describedby="password-rules"
              required
            />
            {passwordRules.data && (
              <ul id="password-rules" className="account-hint">
                {passwordRules.data.rules.map((rule) => (
                  <li key={rule}>{rule}</li>
                ))}
              </ul>
            )}
          </div>
          {errors.map((message) => (
            <p key={message} className="account-error" role="alert">
              {message}
            </p>
          ))}
          <button type="submit" disabled={isSubmitting}>
            {isSubmitting ? "Wird gespeichert …" : "Passwort speichern"}
          </button>
        </form>
      )}
    </main>
  );
};

export default ResetPasswordPage;
