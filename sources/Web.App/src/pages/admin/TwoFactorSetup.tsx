import React from "react";
import { QRCodeSVG } from "qrcode.react";
import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Grid from "@mui/material/Grid";
import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import Step from "@mui/material/Step";
import StepContent from "@mui/material/StepContent";
import StepLabel from "@mui/material/StepLabel";
import Stepper from "@mui/material/Stepper";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import SecurityOutlined from "@mui/icons-material/SecurityOutlined";
import { getValidationErrors, sendRequest } from "../../hooks/apiClient";
import { useStatefulApi } from "../../hooks/useStatefulApi";
import {
  accountUrls,
  type EnableTwoFactorRequest,
  type RecoveryCodesResponse,
  type TwoFactorSetupResponse,
} from "../../api/account";
import SectionCard from "../../components/SectionCard";

type Props = {
  /** Called after the user has saved the recovery codes. */
  onCompleted: () => void;
};

/** Setting up an authenticator app (required for administrators). */
const TwoFactorSetup: React.FC<Props> = ({ onCompleted }) => {
  const setup = useStatefulApi<TwoFactorSetupResponse>({ serviceUrl: accountUrls.twoFactor });
  const loadSetup = setup.get;

  const [code, setCode] = React.useState("");
  const [errors, setErrors] = React.useState<string[]>([]);
  const [recoveryCodes, setRecoveryCodes] = React.useState<string[] | null>(null);
  const [isSubmitting, setIsSubmitting] = React.useState(false);

  React.useEffect(() => {
    void loadSetup();
  }, [loadSetup]);

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setIsSubmitting(true);
    setErrors([]);

    try {
      const response = await sendRequest<RecoveryCodesResponse, EnableTwoFactorRequest>("POST", {
        serviceUrl: accountUrls.enableTwoFactor,
        body: { code },
      });
      setRecoveryCodes(response?.recoveryCodes ?? []);
    } catch (caught) {
      const fieldErrors = getValidationErrors(caught);
      setErrors(fieldErrors.code ?? ["Die Einrichtung ist fehlgeschlagen. Bitte versuche es erneut."]);
    } finally {
      setIsSubmitting(false);
    }
  };

  if (recoveryCodes) {
    return (
      <SectionCard title="Wiederherstellungscodes" icon={<SecurityOutlined color="success" />}>
        <Alert severity="success">
          Die Zwei-Faktor-Anmeldung ist aktiv. Bewahre diese Codes sicher auf: Mit jedem Code kannst du dich einmal
          anmelden, falls du keinen Zugriff auf die Authenticator-App hast. Sie werden nur jetzt angezeigt.
        </Alert>
        <Grid container spacing={1}>
          {recoveryCodes.map((recoveryCode) => (
            <Grid key={recoveryCode} size={{ xs: 6, sm: 4 }}>
              <Paper variant="outlined" sx={{ p: 1, textAlign: "center", fontFamily: "monospace" }}>
                {recoveryCode}
              </Paper>
            </Grid>
          ))}
        </Grid>
        <Button variant="contained" onClick={onCompleted} sx={{ alignSelf: "flex-start" }}>
          Ich habe die Codes gespeichert
        </Button>
      </SectionCard>
    );
  }

  return (
    <SectionCard title="Zwei-Faktor-Anmeldung einrichten" icon={<SecurityOutlined color="primary" />}>
      <Typography color="text.secondary">
        Für den Admin-Bereich ist eine Authenticator-App (z. B. Microsoft oder Google Authenticator) erforderlich.
      </Typography>
      <Stepper orientation="vertical" nonLinear activeStep={-1}>
        <Step expanded>
          <StepLabel>QR-Code mit der App scannen</StepLabel>
          <StepContent>
            {setup.data && (
              <Stack spacing={1}>
                <Box sx={{ bgcolor: "#fff", p: 1, borderRadius: 2, width: "fit-content" }}>
                  <QRCodeSVG value={setup.data.authenticatorUri} size={176} marginSize={1} title="QR-Code für die Authenticator-App" />
                </Box>
                <Typography variant="body2" color="text.secondary">
                  Falls das Scannen nicht klappt, gib diesen Schlüssel von Hand ein:
                </Typography>
                <Typography sx={{ fontFamily: "monospace", letterSpacing: 1 }}>
                  {setup.data.sharedKey.match(/.{1,4}/g)?.join(" ")}
                </Typography>
              </Stack>
            )}
          </StepContent>
        </Step>
        <Step expanded>
          <StepLabel>Code aus der App eingeben</StepLabel>
          <StepContent>
            <Stack component="form" spacing={2} onSubmit={submit} noValidate sx={{ maxWidth: 320 }}>
              <TextField
                label="Sechsstelliger Code"
                value={code}
                onChange={(event) => setCode(event.target.value)}
                autoComplete="one-time-code"
                slotProps={{ htmlInput: { inputMode: "numeric" } }}
                required
              />
              {errors.map((message) => (
                <Alert key={message} severity="error">
                  {message}
                </Alert>
              ))}
              <Button type="submit" variant="contained" loading={isSubmitting} disabled={!setup.data}>
                Aktivieren
              </Button>
            </Stack>
          </StepContent>
        </Step>
      </Stepper>
    </SectionCard>
  );
};

export default TwoFactorSetup;
