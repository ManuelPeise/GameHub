import { useSearchParams } from "react-router";
import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Stack from "@mui/material/Stack";
import MarkEmailReadOutlined from "@mui/icons-material/MarkEmailReadOutlined";
import { pagePaths } from "../../api/account";
import AuthCard from "./AuthCard";

/** Target of the confirmation link (via /api/account/confirm-email): shows the result. */
const EmailConfirmedPage = () => {
  const [query] = useSearchParams();
  const success = query.get("success") === "true";

  return (
    <AuthCard title="E-Mail-Bestätigung" icon={<MarkEmailReadOutlined />}>
      <Stack spacing={2}>
        {success ? (
          <Alert severity="success">Deine E-Mail-Adresse ist bestätigt. Dein Konto ist jetzt aktiv.</Alert>
        ) : (
          <Alert severity="error">Der Bestätigungslink ist ungültig oder abgelaufen.</Alert>
        )}
        <Button href={pagePaths.login} variant="contained">
          Zur Anmeldung
        </Button>
      </Stack>
    </AuthCard>
  );
};

export default EmailConfirmedPage;
