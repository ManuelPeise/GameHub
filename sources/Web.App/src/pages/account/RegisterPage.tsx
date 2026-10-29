import React from "react";
import { ApiError, getValidationErrors, sendRequest } from "../../hooks/apiClient";
import { useStatefulApi } from "../../hooks/useStatefulApi";
import {
  accountUrls,
  pagePaths,
  type PasswordRulesResponse,
  type RegisterRequest,
  type RegisterResponse,
} from "../../api/account";
import "./AccountPages.css";

type FieldErrors = Partial<Record<keyof RegisterRequest, string[]>>;

const emptyRequest: RegisterRequest = { email: "", displayName: "", password: "" };

const RegisterPage: React.FC = () => {
  const passwordRules = useStatefulApi<PasswordRulesResponse>({
    serviceUrl: accountUrls.passwordRules,
  });
  const loadPasswordRules = passwordRules.get;

  const [form, setForm] = React.useState<RegisterRequest>(emptyRequest);
  const [fieldErrors, setFieldErrors] = React.useState<FieldErrors>({});
  const [generalError, setGeneralError] = React.useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = React.useState(false);
  const [successMessage, setSuccessMessage] = React.useState<string | null>(null);

  React.useEffect(() => {
    void loadPasswordRules();
  }, [loadPasswordRules]);

  const update =
    (field: keyof RegisterRequest) => (event: React.ChangeEvent<HTMLInputElement>) => {
      const value = event.target.value;
      setForm((current) => ({ ...current, [field]: value }));
      setFieldErrors((current) => ({ ...current, [field]: undefined }));
    };

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setIsSubmitting(true);
    setGeneralError(null);
    setFieldErrors({});

    try {
      const response = await sendRequest<RegisterResponse, RegisterRequest>("POST", {
        serviceUrl: accountUrls.register,
        body: form,
      });
      setSuccessMessage(response?.message ?? "Bitte prüfe dein E-Mail-Postfach.");
    } catch (error) {
      const errors = getValidationErrors(error);
      if (Object.keys(errors).length > 0) {
        setFieldErrors(errors as FieldErrors);
      } else if (error instanceof ApiError && error.status === 429) {
        setGeneralError("Zu viele Registrierungen. Bitte versuche es später erneut.");
      } else {
        setGeneralError("Die Registrierung ist fehlgeschlagen. Bitte versuche es erneut.");
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  if (successMessage) {
    return (
      <main className="account-page">
        <h1>Registrierung</h1>
        <p className="account-success" role="status">
          {successMessage}
        </p>
        <p className="account-links">
          <a href={pagePaths.login}>Zur Anmeldung</a>
        </p>
      </main>
    );
  }

  const fieldError = (field: keyof RegisterRequest) =>
    fieldErrors[field]?.map((message) => (
      <p key={message} className="account-error">
        {message}
      </p>
    ));

  return (
    <main className="account-page">
      <h1>Registrierung</h1>
      <form className="account-form" onSubmit={submit} noValidate>
        <div className="account-field">
          <label htmlFor="email">E-Mail-Adresse</label>
          <input
            id="email"
            type="email"
            autoComplete="email"
            value={form.email}
            onChange={update("email")}
            aria-invalid={fieldErrors.email ? true : undefined}
            required
          />
          {fieldError("email")}
        </div>

        <div className="account-field">
          <label htmlFor="displayName">Anzeigename</label>
          <input
            id="displayName"
            type="text"
            autoComplete="nickname"
            value={form.displayName}
            onChange={update("displayName")}
            aria-invalid={fieldErrors.displayName ? true : undefined}
            required
          />
          {fieldError("displayName")}
        </div>

        <div className="account-field">
          <label htmlFor="password">Passwort</label>
          <input
            id="password"
            type="password"
            autoComplete="new-password"
            value={form.password}
            onChange={update("password")}
            aria-invalid={fieldErrors.password ? true : undefined}
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
          {fieldError("password")}
        </div>

        {generalError && (
          <p className="account-error" role="alert">
            {generalError}
          </p>
        )}

        <button type="submit" disabled={isSubmitting}>
          {isSubmitting ? "Wird gesendet …" : "Konto anlegen"}
        </button>
      </form>
      <p className="account-links">
        <a href={pagePaths.login}>Ich habe schon ein Konto</a>
      </p>
    </main>
  );
};

export default RegisterPage;
