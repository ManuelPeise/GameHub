import React from "react";
import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Link from "@mui/material/Link";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import KeyOutlined from "@mui/icons-material/KeyOutlined";
import { ApiError, sendRequest } from "../../hooks/apiClient";
import { accountUrls, pagePaths, type EmailRequest, type MessageResponse } from "../../api/account";
import AuthCard from "./AuthCard";

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
    <AuthCard
      title="Passwort vergessen"
      icon={<KeyOutlined />}
      subtitle={message ? undefined : "Wir schicken dir einen Link, mit dem du ein neues Passwort festlegen kannst."}
      footer={<Link href={pagePaths.login}>Zur Anmeldung</Link>}
    >
      {message ? (
        <Alert severity="success">{message}</Alert>
      ) : (
        <Stack component="form" spacing={2} onSubmit={submit} noValidate>
          <TextField
            label="E-Mail-Adresse"
            type="email"
            autoComplete="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            required
            autoFocus
          />
          {error && <Alert severity="error">{error}</Alert>}
          <Button type="submit" variant="contained" size="large" loading={isSubmitting}>
            Link anfordern
          </Button>
        </Stack>
      )}
    </AuthCard>
  );
};

export default ForgotPasswordPage;
