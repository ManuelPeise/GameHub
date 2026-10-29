import React from "react";
import { useSearchParams } from "react-router";
import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import KeyOutlined from "@mui/icons-material/KeyOutlined";
import { getValidationErrors, sendRequest } from "../../hooks/apiClient";
import { useStatefulApi } from "../../hooks/useStatefulApi";
import { accountUrls, pagePaths, type PasswordRulesResponse, type ResetPasswordRequest } from "../../api/account";
import AuthCard from "./AuthCard";
import PasswordRules from "./PasswordRules";

/** Target of the link in the password reset mail (?userId=...&token=...). */
const ResetPasswordPage: React.FC = () => {
  const [query] = useSearchParams();
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
      <AuthCard title="Neues Passwort" icon={<KeyOutlined />}>
        <Stack spacing={2}>
          <Alert severity="error">Der Link ist unvollständig. Bitte fordere einen neuen an.</Alert>
          <Button href={pagePaths.forgotPassword} variant="contained">
            Neuen Link anfordern
          </Button>
        </Stack>
      </AuthCard>
    );
  }

  return (
    <AuthCard title="Neues Passwort" icon={<KeyOutlined />}>
      {done ? (
        <Stack spacing={2}>
          <Alert severity="success">Dein Passwort wurde geändert. Du kannst dich jetzt anmelden.</Alert>
          <Button href={pagePaths.login} variant="contained">
            Zur Anmeldung
          </Button>
        </Stack>
      ) : (
        <Stack component="form" spacing={2} onSubmit={submit} noValidate>
          <TextField
            label="Neues Passwort"
            type="password"
            autoComplete="new-password"
            value={newPassword}
            onChange={(event) => setNewPassword(event.target.value)}
            error={errors.length > 0}
            slotProps={{ htmlInput: { "aria-describedby": "password-rules" } }}
            required
            autoFocus
          />
          {passwordRules.data && <PasswordRules id="password-rules" rules={passwordRules.data.rules} />}
          {errors.map((message) => (
            <Alert key={message} severity="error">
              {message}
            </Alert>
          ))}
          <Button type="submit" variant="contained" size="large" loading={isSubmitting}>
            Passwort speichern
          </Button>
        </Stack>
      )}
    </AuthCard>
  );
};

export default ResetPasswordPage;
