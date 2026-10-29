import React from "react";
import { ApiError, getValidationErrors, sendRequest } from "../../hooks/apiClient";
import { accountUrls, pagePaths, type DeleteAccountRequest } from "../../api/account";
import "./AccountPages.css";

/** The signed-in user deletes the own account (password and confirmation required). */
const DeleteAccountPage: React.FC = () => {
  const [form, setForm] = React.useState<DeleteAccountRequest>({ password: "", confirmed: false });
  const [errors, setErrors] = React.useState<string[]>([]);
  const [deleted, setDeleted] = React.useState(false);
  const [isSubmitting, setIsSubmitting] = React.useState(false);

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setIsSubmitting(true);
    setErrors([]);

    try {
      await sendRequest<null, DeleteAccountRequest>("POST", { serviceUrl: accountUrls.deleteAccount, body: form });
      setDeleted(true);
    } catch (caught) {
      const fieldErrors = getValidationErrors(caught);
      const messages = [...(fieldErrors.confirmed ?? []), ...(fieldErrors.password ?? [])];
      if (messages.length > 0) {
        setErrors(messages);
      } else if (caught instanceof ApiError && caught.status === 401) {
        setErrors(["Bitte melde dich zuerst an."]);
      } else {
        setErrors(["Das Konto konnte nicht gelöscht werden. Bitte versuche es erneut."]);
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  if (deleted) {
    return (
      <main className="account-page">
        <h1>Konto gelöscht</h1>
        <p className="account-success" role="status">
          Dein Konto und deine persönlichen Daten wurden gelöscht. Danke, dass du GameHub genutzt hast.
        </p>
        <p className="account-links">
          <a href={pagePaths.home}>Zur Startseite</a>
        </p>
      </main>
    );
  }

  return (
    <main className="account-page">
      <h1>Konto löschen</h1>
      <form className="account-form" onSubmit={submit} noValidate>
        <p>
          Dein Name, deine E-Mail-Adresse und deine Anmeldedaten werden endgültig gelöscht. In vergangenen Partien
          erscheinst du danach als „Gelöschter Spieler“. Das lässt sich nicht rückgängig machen.
        </p>
        <div className="account-field">
          <label htmlFor="password">Passwort zur Bestätigung</label>
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
            checked={form.confirmed}
            onChange={(event) => setForm({ ...form, confirmed: event.target.checked })}
          />
          Ja, mein Konto soll endgültig gelöscht werden.
        </label>
        {errors.map((message) => (
          <p key={message} className="account-error" role="alert">
            {message}
          </p>
        ))}
        <button type="submit" className="account-danger" disabled={isSubmitting || !form.confirmed}>
          {isSubmitting ? "Wird gelöscht …" : "Konto endgültig löschen"}
        </button>
      </form>
      <p className="account-links">
        <a href={pagePaths.home}>Abbrechen</a>
      </p>
    </main>
  );
};

export default DeleteAccountPage;
