import React from "react";
import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Checkbox from "@mui/material/Checkbox";
import FormControlLabel from "@mui/material/FormControlLabel";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import DeleteForeverOutlined from "@mui/icons-material/DeleteForeverOutlined";
import { ApiError, getValidationErrors, sendRequest } from "../../hooks/apiClient";
import { accountUrls, pagePaths, type DeleteAccountRequest } from "../../api/account";
import { useCurrentUser } from "../../app/CurrentUser";
import AuthCard from "./AuthCard";

/** The signed-in user deletes the own account (password and confirmation required). */
const DeleteAccountPage: React.FC = () => {
  const { reload } = useCurrentUser();
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
      await reload();
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
      <AuthCard title="Konto gelöscht" icon={<DeleteForeverOutlined />}>
        <Stack spacing={2}>
          <Alert severity="success">
            Dein Konto und deine persönlichen Daten wurden gelöscht. Danke, dass du GameHub genutzt hast.
          </Alert>
          <Button href={pagePaths.home} variant="contained">
            Zur Startseite
          </Button>
        </Stack>
      </AuthCard>
    );
  }

  return (
    <AuthCard title="Konto löschen" icon={<DeleteForeverOutlined />}>
      <Stack component="form" spacing={2} onSubmit={submit} noValidate>
        <Alert severity="warning">
          Dein Name, deine E-Mail-Adresse und deine Anmeldedaten werden endgültig gelöscht. In vergangenen Partien
          erscheinst du danach als „Gelöschter Spieler“. Das lässt sich nicht rückgängig machen.
        </Alert>
        <TextField
          label="Passwort zur Bestätigung"
          type="password"
          autoComplete="current-password"
          value={form.password}
          onChange={(event) => setForm({ ...form, password: event.target.value })}
          required
        />
        <FormControlLabel
          control={<Checkbox checked={form.confirmed} onChange={(event) => setForm({ ...form, confirmed: event.target.checked })} />}
          label={<Typography variant="body2">Ja, mein Konto soll endgültig gelöscht werden.</Typography>}
        />
        {errors.map((message) => (
          <Alert key={message} severity="error">
            {message}
          </Alert>
        ))}
        <Button type="submit" variant="contained" color="error" size="large" loading={isSubmitting} disabled={!form.confirmed}>
          Konto endgültig löschen
        </Button>
        <Button href={pagePaths.home}>Abbrechen</Button>
      </Stack>
    </AuthCard>
  );
};

export default DeleteAccountPage;
