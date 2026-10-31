import React from "react";
import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Chip from "@mui/material/Chip";
import List from "@mui/material/List";
import ListItemButton from "@mui/material/ListItemButton";
import ListItemText from "@mui/material/ListItemText";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import VerifiedUserOutlined from "@mui/icons-material/VerifiedUserOutlined";
import type { AuditEntryDto, AuditPageDto } from "../../api/generated/contracts";
import { getErrorMessage, sendRequest } from "../../hooks/apiClient";
import Loading from "../../components/Loading";
import SectionCard from "../../components/SectionCard";
import { adminUrls, formatDateTime, prettyJson, toIso } from "./adminApi";
import LogDetailDialog from "./LogDetailDialog";

/** Audit log (read only) with filters for user, action and period (US-026). */
const AuditPanel: React.FC = () => {
  const [user, setUser] = React.useState("");
  const [action, setAction] = React.useState("");
  const [from, setFrom] = React.useState("");
  const [to, setTo] = React.useState("");
  const [entries, setEntries] = React.useState<AuditEntryDto[] | null>(null);
  const [nextBeforeId, setNextBeforeId] = React.useState<number | null>(null);
  const [selected, setSelected] = React.useState<AuditEntryDto | null>(null);
  const [error, setError] = React.useState<string | null>(null);

  const load = React.useCallback(
    async (beforeId: number | null) => {
      try {
        const params: Record<string, string | number> = { take: 50 };
        if (user.trim()) params.user = user.trim();
        if (action.trim()) params.action = action.trim();
        if (from) params.from = toIso(from);
        if (to) params.to = toIso(to);
        if (beforeId !== null) params.beforeId = beforeId;
        const page = await sendRequest<AuditPageDto>("GET", { serviceUrl: adminUrls.auditLog, params });
        if (page) {
          setEntries((previous) => (beforeId === null ? page.items : [...(previous ?? []), ...page.items]));
          setNextBeforeId(page.nextBeforeId ?? null);
        }
      } catch (failure) {
        setError(getErrorMessage(failure, "Das Audit-Log konnte nicht geladen werden."));
      }
    },
    [user, action, from, to],
  );

  React.useEffect(() => {
    const timer = setTimeout(() => void load(null), 300);
    return () => clearTimeout(timer);
  }, [load]);

  return (
    <SectionCard title="Audit-Log" icon={<VerifiedUserOutlined color="primary" />}>
      <Typography variant="body2" color="text.secondary">
        Einträge sind unveränderlich und enthalten keine personenbezogenen Daten; Namen sind die aktuellen Anzeigenamen.
      </Typography>
      <Stack direction="row" spacing={1.5} useFlexGap sx={{ flexWrap: "wrap", "& > *": { flex: { xs: "1 1 100%", sm: "1 1 160px" } } }}>
        <TextField size="small" label="Benutzer (Name/E-Mail)" value={user} onChange={(event) => setUser(event.target.value)} />
        <TextField size="small" label="Aktion (z. B. Login)" value={action} onChange={(event) => setAction(event.target.value)} />
        <TextField size="small" type="datetime-local" label="Von" value={from} onChange={(event) => setFrom(event.target.value)} slotProps={{ inputLabel: { shrink: true } }} />
        <TextField size="small" type="datetime-local" label="Bis" value={to} onChange={(event) => setTo(event.target.value)} slotProps={{ inputLabel: { shrink: true } }} />
      </Stack>

      {error && (
        <Alert severity="error" onClose={() => setError(null)}>
          {error}
        </Alert>
      )}
      {entries === null && !error && <Loading />}
      {entries?.length === 0 && <Typography color="text.secondary">Keine Einträge für diesen Filter.</Typography>}
      {entries && entries.length > 0 && (
        <List dense disablePadding>
          {entries.map((entry) => (
            <ListItemButton key={entry.id} divider onClick={() => setSelected(entry)} sx={{ gap: 1.5, alignItems: "flex-start", px: 1 }}>
              <Chip
                size="small"
                color={entry.result === "Success" ? "success" : "error"}
                variant="outlined"
                label={entry.result === "Success" ? "OK" : "Fehler"}
                sx={{ mt: 0.5, minWidth: 64 }}
              />
              <ListItemText
                primary={entry.action}
                secondary={`${formatDateTime(entry.createdAt)} · ${entry.userName ?? "anonym"}${entry.targetName ? ` → ${entry.targetName}` : ""}`}
                slotProps={{ primary: { sx: { wordBreak: "break-word" } } }}
              />
            </ListItemButton>
          ))}
        </List>
      )}
      {nextBeforeId !== null && (
        <Button onClick={() => void load(nextBeforeId)} sx={{ alignSelf: "center" }}>
          Ältere laden
        </Button>
      )}

      <LogDetailDialog
        title={selected ? selected.action : ""}
        onClose={() => setSelected(null)}
        fields={
          selected && [
            { label: "Zeitpunkt", value: formatDateTime(selected.createdAt) },
            { label: "Ergebnis", value: selected.result },
            { label: "Benutzer", value: selected.userName ? `${selected.userName} (${selected.userId})` : selected.userId },
            { label: "Ziel", value: selected.targetName ? `${selected.targetName} (${selected.target})` : selected.target },
            { label: "Details", value: prettyJson(selected.details), code: true },
          ]
        }
      />
    </SectionCard>
  );
};

export default AuditPanel;
