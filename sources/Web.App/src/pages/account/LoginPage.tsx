import React from "react";
import { useNavigate } from "react-router";
import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Checkbox from "@mui/material/Checkbox";
import FormControlLabel from "@mui/material/FormControlLabel";
import Link from "@mui/material/Link";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import LockOutlined from "@mui/icons-material/LockOutlined";
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
import { useCurrentUser } from "../../app/CurrentUser";
import AuthCard from "./AuthCard";

type Step = "password" | "twoFactor" | "recoveryCode";

type LoginProblem = { title?: string; code?: LoginErrorCode } | null;

const LoginPage: React.FC = () => {
  const navigate = useNavigate();
  const { reload } = useCurrentUser();
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
      await reload();
      navigate(pagePaths.home);
    } catch (caught) {
      handleFailure(caught);
    } finally {
      setIsSubmitting(false);
    }
  };

  const submitPassword = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    void run(() => sendRequest<CurrentUserResponse, LoginRequest>("POST", { serviceUrl: accountUrls.login, body: form }));
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
        <Alert
          severity="error"
          action={
            errorCode === "email_not_confirmed" ? (
              <Button color="inherit" size="small" onClick={() => void resendConfirmation()}>
                Link erneut senden
              </Button>
            ) : undefined
          }
        >
          {error}
        </Alert>
      )}
      {info && <Alert severity="success">{info}</Alert>}
    </>
  );

  if (step !== "password") {
    const isRecovery = step === "recoveryCode";
    return (
      <AuthCard
        title="Zwei-Faktor-Anmeldung"
        icon={<LockOutlined />}
        subtitle={
          isRecovery
            ? "Gib einen deiner Wiederherstellungscodes ein. Jeder Code funktioniert nur einmal."
            : "Gib den sechsstelligen Code aus deiner Authenticator-App ein."
        }
      >
        <Stack component="form" spacing={2} onSubmit={submitCode} noValidate>
          <TextField
            label={isRecovery ? "Wiederherstellungscode" : "Code"}
            value={code}
            onChange={(event) => setCode(event.target.value)}
            autoComplete="one-time-code"
            autoFocus
            required
            slotProps={{ htmlInput: { inputMode: isRecovery ? "text" : "numeric" } }}
          />
          {messages}
          <Button type="submit" variant="contained" size="large" loading={isSubmitting}>
            Bestätigen
          </Button>
          <Button
            onClick={() => {
              setStep(isRecovery ? "twoFactor" : "recoveryCode");
              setCode("");
              setError(null);
            }}
          >
            {isRecovery ? "Code aus der Authenticator-App verwenden" : "Keinen Zugriff auf die App? Wiederherstellungscode"}
          </Button>
        </Stack>
      </AuthCard>
    );
  }

  return (
    <AuthCard
      title="Anmelden"
      icon={<LockOutlined />}
      footer={
        <>
          <Link href={pagePaths.forgotPassword}>Passwort vergessen?</Link>
          <Typography color="text.secondary">·</Typography>
          <Link href={pagePaths.register}>Neues Konto anlegen</Link>
        </>
      }
    >
      <Stack component="form" spacing={2} onSubmit={submitPassword} noValidate>
        <TextField
          label="E-Mail-Adresse"
          type="email"
          autoComplete="username"
          value={form.email}
          onChange={(event) => setForm({ ...form, email: event.target.value })}
          required
          autoFocus
        />
        <TextField
          label="Passwort"
          type="password"
          autoComplete="current-password"
          value={form.password}
          onChange={(event) => setForm({ ...form, password: event.target.value })}
          required
        />
        <FormControlLabel
          control={<Checkbox checked={form.rememberMe} onChange={(event) => setForm({ ...form, rememberMe: event.target.checked })} />}
          label="Angemeldet bleiben"
        />
        {messages}
        <Button type="submit" variant="contained" size="large" loading={isSubmitting}>
          Anmelden
        </Button>
      </Stack>
    </AuthCard>
  );
};

export default LoginPage;
