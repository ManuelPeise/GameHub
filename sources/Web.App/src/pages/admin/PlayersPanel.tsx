import React from "react";
import Alert from "@mui/material/Alert";
import Avatar from "@mui/material/Avatar";
import Button from "@mui/material/Button";
import Checkbox from "@mui/material/Checkbox";
import Chip from "@mui/material/Chip";
import Dialog from "@mui/material/Dialog";
import DialogActions from "@mui/material/DialogActions";
import DialogContent from "@mui/material/DialogContent";
import DialogContentText from "@mui/material/DialogContentText";
import DialogTitle from "@mui/material/DialogTitle";
import FormControlLabel from "@mui/material/FormControlLabel";
import IconButton from "@mui/material/IconButton";
import InputAdornment from "@mui/material/InputAdornment";
import List from "@mui/material/List";
import ListItem from "@mui/material/ListItem";
import ListItemAvatar from "@mui/material/ListItemAvatar";
import ListItemIcon from "@mui/material/ListItemIcon";
import ListItemText from "@mui/material/ListItemText";
import Menu from "@mui/material/Menu";
import MenuItem from "@mui/material/MenuItem";
import Stack from "@mui/material/Stack";
import Switch from "@mui/material/Switch";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import DeleteForeverOutlined from "@mui/icons-material/DeleteForeverOutlined";
import LockOpenOutlined from "@mui/icons-material/LockOpenOutlined";
import LockOutlined from "@mui/icons-material/LockOutlined";
import MoreVert from "@mui/icons-material/MoreVert";
import PeopleOutline from "@mui/icons-material/PeopleOutline";
import Search from "@mui/icons-material/Search";
import type { AdminUserDto, AdminUserPageDto, AdminDeleteUserRequest } from "../../api/generated/contracts";
import { roleNames } from "../../api/account";
import { getErrorMessage, sendRequest } from "../../hooks/apiClient";
import Loading from "../../components/Loading";
import SectionCard from "../../components/SectionCard";
import { adminUrls } from "./adminApi";

const pageSize = 25;


const formatDate = (value?: string | null) =>
  value ? new Date(value).toLocaleString("de-DE", { dateStyle: "medium", timeStyle: "short" }) : "noch nie";

const statusChip = (user: AdminUserDto) =>
  user.status === "Locked" ? (
    <Chip size="small" color="warning" icon={<LockOutlined />} label="gesperrt" />
  ) : user.status === "Deleted" ? (
    <Chip size="small" label="gelöscht" />
  ) : !user.emailConfirmed ? (
    <Chip size="small" color="info" variant="outlined" label="nicht bestätigt" />
  ) : null;

type PendingAction = { kind: "lock" | "unlock" | "delete"; user: AdminUserDto };

/** Player administration: search, lock, unlock, delete (US-025). */
const PlayersPanel: React.FC = () => {
  const [query, setQuery] = React.useState("");
  const [includeDeleted, setIncludeDeleted] = React.useState(false);
  const [page, setPage] = React.useState<AdminUserPageDto | null>(null);
  const [error, setError] = React.useState<string | null>(null);
  const [info, setInfo] = React.useState<string | null>(null);
  const [menu, setMenu] = React.useState<{ anchor: HTMLElement; user: AdminUserDto } | null>(null);
  const [pending, setPending] = React.useState<PendingAction | null>(null);
  const [confirmed, setConfirmed] = React.useState(false);
  const [isWorking, setIsWorking] = React.useState(false);

  const load = React.useCallback(
    async (skip: number) => {
      try {
        const loaded = await sendRequest<AdminUserPageDto>("GET", {
          serviceUrl: adminUrls.users,
          params: { query, includeDeleted, skip, take: pageSize },
        });
        if (loaded) {
          setPage((previous) => (skip === 0 || !previous ? loaded : { items: [...previous.items, ...loaded.items], total: loaded.total }));
        }
      } catch (failure) {
        setError(getErrorMessage(failure, "Die Spielerliste konnte nicht geladen werden."));
      }
    },
    [query, includeDeleted],
  );

  // Search while typing, with a short delay.
  React.useEffect(() => {
    const timer = setTimeout(() => void load(0), 300);
    return () => clearTimeout(timer);
  }, [load]);

  const open = (kind: PendingAction["kind"], user: AdminUserDto) => {
    setMenu(null);
    setConfirmed(false);
    setPending({ kind, user });
  };

  const execute = async () => {
    if (!pending) {
      return;
    }
    setIsWorking(true);
    setError(null);
    const { kind, user } = pending;
    try {
      if (kind === "delete") {
        await sendRequest<null, AdminDeleteUserRequest>("POST", { serviceUrl: adminUrls.remove(user.id), body: { confirmed: true } });
        setInfo(`${user.displayName} wurde gelöscht.`);
      } else {
        await sendRequest<null>("POST", { serviceUrl: kind === "lock" ? adminUrls.lock(user.id) : adminUrls.unlock(user.id) });
        setInfo(kind === "lock" ? `${user.displayName} ist gesperrt und wird abgemeldet.` : `${user.displayName} ist entsperrt.`);
      }
      setPending(null);
      await load(0);
    } catch (failure) {
      setError(getErrorMessage(failure, "Die Aktion ist fehlgeschlagen."));
      setPending(null);
    } finally {
      setIsWorking(false);
    }
  };

  return (
    <SectionCard title={`Spieler${page ? ` (${page.total})` : ""}`} icon={<PeopleOutline color="primary" />}>
      <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5} sx={{ alignItems: { sm: "center" } }}>
        <TextField
          size="small"
          placeholder="Name oder E-Mail suchen"
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          slotProps={{
            input: {
              startAdornment: (
                <InputAdornment position="start">
                  <Search />
                </InputAdornment>
              ),
            },
          }}
        />
        <FormControlLabel
          control={<Switch checked={includeDeleted} onChange={(event) => setIncludeDeleted(event.target.checked)} />}
          label="Gelöschte anzeigen"
          sx={{ flexShrink: 0 }}
        />
      </Stack>

      {error && (
        <Alert severity="error" onClose={() => setError(null)}>
          {error}
        </Alert>
      )}
      {info && (
        <Alert severity="success" onClose={() => setInfo(null)}>
          {info}
        </Alert>
      )}

      {page === null && !error && <Loading />}
      {page?.items.length === 0 && <Typography color="text.secondary">Keine Spieler gefunden.</Typography>}
      {page && page.items.length > 0 && (
        <List disablePadding>
          {page.items.map((user) => (
            <ListItem
              key={user.id}
              divider
              disableGutters
              secondaryAction={
                user.status !== "Deleted" &&
                !user.isMe && (
                  <IconButton edge="end" aria-label={`Aktionen für ${user.displayName}`} onClick={(event) => setMenu({ anchor: event.currentTarget, user })}>
                    <MoreVert />
                  </IconButton>
                )
              }
              sx={{ alignItems: "flex-start", opacity: user.status === "Deleted" ? 0.6 : 1 }}
            >
              <ListItemAvatar sx={{ mt: 0.5 }}>
                <Avatar sx={{ bgcolor: user.roles.includes(roleNames.administrator) ? "secondary.dark" : "primary.dark" }}>
                  {user.displayName.slice(0, 1).toUpperCase()}
                </Avatar>
              </ListItemAvatar>
              <ListItemText
                primary={
                  <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap", rowGap: 0.5 }}>
                    <span>
                      {user.displayName}
                      {user.isMe && " (du)"}
                    </span>
                    {user.roles.includes(roleNames.administrator) && <Chip size="small" color="secondary" variant="outlined" label="Admin" />}
                    {statusChip(user)}
                  </Stack>
                }
                secondary={
                  <>
                    <Typography component="span" variant="body2" sx={{ display: "block", wordBreak: "break-all" }}>
                      {user.email}
                    </Typography>
                    <Typography component="span" variant="caption" color="text.secondary">
                      Letzte Anmeldung: {formatDate(user.lastLoginAt)} · seit {new Date(user.createdAt).toLocaleDateString("de-DE")}
                    </Typography>
                  </>
                }
                sx={{ pr: 5 }}
              />
            </ListItem>
          ))}
        </List>
      )}
      {page && page.items.length < page.total && (
        <Button onClick={() => void load(page.items.length)} sx={{ alignSelf: "center" }}>
          Weitere laden
        </Button>
      )}

      <Menu anchorEl={menu?.anchor} open={menu !== null} onClose={() => setMenu(null)}>
        {menu?.user.status === "Locked" ? (
          <MenuItem onClick={() => open("unlock", menu.user)}>
            <ListItemIcon>
              <LockOpenOutlined fontSize="small" />
            </ListItemIcon>
            Entsperren
          </MenuItem>
        ) : (
          <MenuItem onClick={() => menu && open("lock", menu.user)}>
            <ListItemIcon>
              <LockOutlined fontSize="small" />
            </ListItemIcon>
            Sperren
          </MenuItem>
        )}
        <MenuItem onClick={() => menu && open("delete", menu.user)} sx={{ color: "error.main" }}>
          <ListItemIcon>
            <DeleteForeverOutlined fontSize="small" color="error" />
          </ListItemIcon>
          Löschen
        </MenuItem>
      </Menu>

      <Dialog open={pending !== null} onClose={() => !isWorking && setPending(null)} fullWidth maxWidth="xs">
        {pending && (
          <>
            <DialogTitle>
              {pending.kind === "delete"
                ? `${pending.user.displayName} löschen?`
                : pending.kind === "lock"
                  ? `${pending.user.displayName} sperren?`
                  : `${pending.user.displayName} entsperren?`}
            </DialogTitle>
            <DialogContent>
              <DialogContentText>
                {pending.kind === "delete"
                  ? "Name, E-Mail-Adresse und Anmeldedaten werden endgültig gelöscht. In Partien erscheint der Spieler als „Gelöschter Spieler“; laufende Partien werden für ihn beendet."
                  : pending.kind === "lock"
                    ? "Der Spieler kann sich nicht mehr anmelden und wird innerhalb einer Minute abgemeldet."
                    : "Der Spieler kann sich wieder anmelden."}
              </DialogContentText>
              {pending.kind === "delete" && (
                <FormControlLabel
                  sx={{ mt: 1 }}
                  control={<Checkbox checked={confirmed} onChange={(event) => setConfirmed(event.target.checked)} />}
                  label="Ja, endgültig löschen"
                />
              )}
            </DialogContent>
            <DialogActions>
              <Button onClick={() => setPending(null)} disabled={isWorking}>
                Abbrechen
              </Button>
              <Button
                variant="contained"
                color={pending.kind === "unlock" ? "primary" : "error"}
                loading={isWorking}
                disabled={pending.kind === "delete" && !confirmed}
                onClick={() => void execute()}
              >
                {pending.kind === "delete" ? "Löschen" : pending.kind === "lock" ? "Sperren" : "Entsperren"}
              </Button>
            </DialogActions>
          </>
        )}
      </Dialog>
    </SectionCard>
  );
};

export default PlayersPanel;
