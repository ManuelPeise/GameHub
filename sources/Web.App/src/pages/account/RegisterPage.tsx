import React from "react";
import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Link from "@mui/material/Link";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import PersonAddAlt1Outlined from "@mui/icons-material/PersonAddAlt1Outlined";
import MarkEmailReadOutlined from "@mui/icons-material/MarkEmailReadOutlined";
import { ApiError, getValidationErrors, sendRequest } from "../../hooks/apiClient";
import { useStatefulApi } from "../../hooks/useStatefulApi";
import {
  accountUrls,
  pagePaths,
  type PasswordRulesResponse,
  type RegisterRequest,
  type RegisterResponse,
} from "../../api/account";
import AuthCard from "./AuthCard";
import PasswordRules from "./PasswordRules";

type FieldErrors = Partial<Record<keyof RegisterRequest, string[]>>;

const emptyRequest: RegisterRequest = { email: "", displayName: "", password: "" };

const RegisterPage: React.FC = () => {
  const passwordRules = useStatefulApi<PasswordRulesResponse>({ serviceUrl: accountUrls.passwordRules });
  const loadPasswordRules = passwordRules.get;

  const [form, setForm] = React.useState<RegisterRequest>(emptyRequest);
  const [fieldErrors, setFieldErrors] = React.useState<FieldErrors>({});
  const [generalError, setGeneralError] = React.useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = React.useState(false);
  const [successMessage, setSuccessMessage] = React.useState<string | null>(null);

  React.useEffect(() => {
    void loadPasswordRules();
  }, [loadPasswordRules]);

  const update = (field: keyof RegisterRequest) => (event: React.ChangeEvent<HTMLInputElement>) => {
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
      <AuthCard title="Fast geschafft" icon={<MarkEmailReadOutlined />}>
        <Stack spacing={2}>
          <Alert severity="success">{successMessage}</Alert>
          <Button href={pagePaths.login} variant="contained">
            Zur Anmeldung
          </Button>
        </Stack>
      </AuthCard>
    );
  }

  const errorText = (field: keyof RegisterRequest) => fieldErrors[field]?.join(" ");

  return (
    <AuthCard
      title="Konto anlegen"
      icon={<PersonAddAlt1Outlined />}
      footer={<Link href={pagePaths.login}>Ich habe schon ein Konto</Link>}
    >
      <Stack component="form" spacing={2} onSubmit={submit} noValidate>
        <TextField
          label="E-Mail-Adresse"
          type="email"
          autoComplete="email"
          value={form.email}
          onChange={update("email")}
          error={Boolean(fieldErrors.email)}
          helperText={errorText("email")}
          required
          autoFocus
        />
        <TextField
          label="Anzeigename"
          autoComplete="nickname"
          value={form.displayName}
          onChange={update("displayName")}
          error={Boolean(fieldErrors.displayName)}
          helperText={errorText("displayName") ?? "So sehen dich andere Spieler."}
          required
        />
        <TextField
          label="Passwort"
          type="password"
          autoComplete="new-password"
          value={form.password}
          onChange={update("password")}
          error={Boolean(fieldErrors.password)}
          helperText={errorText("password")}
          slotProps={{ htmlInput: { "aria-describedby": "password-rules" } }}
          required
        />
        {passwordRules.data && <PasswordRules id="password-rules" rules={passwordRules.data.rules} />}
        {generalError && <Alert severity="error">{generalError}</Alert>}
        <Button type="submit" variant="contained" size="large" loading={isSubmitting}>
          Konto anlegen
        </Button>
      </Stack>
    </AuthCard>
  );
};

export default RegisterPage;
