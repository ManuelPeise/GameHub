import React from "react";
import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Chip from "@mui/material/Chip";
import List from "@mui/material/List";
import ListItemButton from "@mui/material/ListItemButton";
import ListItemText from "@mui/material/ListItemText";
import MenuItem from "@mui/material/MenuItem";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import ArticleOutlined from "@mui/icons-material/ArticleOutlined";
import type { AppLogEntryDto, AppLogPageDto } from "../../api/generated/contracts";
import { getErrorMessage, sendRequest } from "../../hooks/apiClient";
import Loading from "../../components/Loading";
import SectionCard from "../../components/SectionCard";
import { adminUrls, formatDateTime, prettyJson, toIso } from "./adminApi";
import LogDetailDialog from "./LogDetailDialog";

const levels = ["Trace", "Debug", "Information", "Warning", "Error", "Critical"] as const;

const levelColor = (level: string) =>
  level === "Critical" || level === "Error" ? "error" : level === "Warning" ? "warning" : level === "Information" ? "info" : "default";

/** Application log with filters (period, minimum level, text) and details (US-026). */
const AppLogPanel: React.FC = () => {
  const [level, setLevel] = React.useState("Warning");
  const [text, setText] = React.useState("");
  const [from, setFrom] = React.useState("");
  const [to, setTo] = React.useState("");
  const [entries, setEntries] = React.useState<AppLogEntryDto[] | null>(null);
  const [nextBeforeId, setNextBeforeId] = React.useState<number | null>(null);
  const [selected, setSelected] = React.useState<AppLogEntryDto | null>(null);
  const [error, setError] = React.useState<string | null>(null);

  const load = React.useCallback(
    async (beforeId: number | null) => {
      try {
        const params: Record<string, string | number> = { level, take: 50 };
        if (text.trim()) params.text = text.trim();
        if (from) params.from = toIso(from);
        if (to) params.to = toIso(to);
        if (beforeId !== null) params.beforeId = beforeId;
        const page = await sendRequest<AppLogPageDto>("GET", { serviceUrl: adminUrls.appLog, params });
        if (page) {
          setEntries((previous) => (beforeId === null ? page.items : [...(previous ?? []), ...page.items]));
          setNextBeforeId(page.nextBeforeId ?? null);
        }
      } catch (failure) {
        setError(getErrorMessage(failure, "Das Anwendungslog konnte nicht geladen werden."));
      }
    },
    [level, text, from, to],
  );

  React.useEffect(() => {
    const timer = setTimeout(() => void load(null), 300);
    return () => clearTimeout(timer);
  }, [load]);

  return (
    <SectionCard title="Anwendungslog" icon={<ArticleOutlined color="primary" />}>
      <Stack direction="row" spacing={1.5} useFlexGap sx={{ flexWrap: "wrap", "& > *": { flex: { xs: "1 1 100%", sm: "1 1 160px" } } }}>
        <TextField select size="small" label="Ab Level" value={level} onChange={(event) => setLevel(event.target.value)}>
          {levels.map((value) => (
            <MenuItem key={value} value={value}>
              {value}
            </MenuItem>
          ))}
        </TextField>
        <TextField size="small" label="Text" value={text} onChange={(event) => setText(event.target.value)} />
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
              <Chip size="small" color={levelColor(entry.level)} label={entry.level} sx={{ mt: 0.5, minWidth: 92 }} />
              <ListItemText
                primary={entry.message}
                secondary={`${formatDateTime(entry.timestamp)} · ${entry.category}${entry.exception ? " · Exception" : ""}`}
                slotProps={{ primary: { sx: { display: "-webkit-box", WebkitLineClamp: 2, WebkitBoxOrient: "vertical", overflow: "hidden", wordBreak: "break-word" } } }}
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
        title={selected ? `${selected.level} · ${formatDateTime(selected.timestamp)}` : ""}
        onClose={() => setSelected(null)}
        fields={
          selected && [
            { label: "Kategorie", value: selected.category },
            { label: "Event-ID", value: selected.eventId ?? undefined },
            { label: "Nachricht", value: selected.message, code: true },
            { label: "Exception", value: selected.exception, code: true },
            { label: "Eigenschaften", value: prettyJson(selected.properties), code: true },
          ]
        }
      />
    </SectionCard>
  );
};

export default AppLogPanel;
