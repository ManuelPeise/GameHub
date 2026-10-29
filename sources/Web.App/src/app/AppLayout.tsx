import React from "react";
import { Outlet, useLocation, useNavigate } from "react-router";
import AppBar from "@mui/material/AppBar";
import BottomNavigation from "@mui/material/BottomNavigation";
import BottomNavigationAction from "@mui/material/BottomNavigationAction";
import Paper from "@mui/material/Paper";
import Avatar from "@mui/material/Avatar";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Chip from "@mui/material/Chip";
import Divider from "@mui/material/Divider";
import Drawer from "@mui/material/Drawer";
import IconButton from "@mui/material/IconButton";
import List from "@mui/material/List";
import ListItemButton from "@mui/material/ListItemButton";
import ListItemIcon from "@mui/material/ListItemIcon";
import ListItemText from "@mui/material/ListItemText";
import ListSubheader from "@mui/material/ListSubheader";
import Menu from "@mui/material/Menu";
import MenuItem from "@mui/material/MenuItem";
import Toolbar from "@mui/material/Toolbar";
import Tooltip from "@mui/material/Tooltip";
import Typography from "@mui/material/Typography";
import useMediaQuery from "@mui/material/useMediaQuery";
import { useTheme } from "@mui/material/styles";
import AddCircleOutline from "@mui/icons-material/AddCircleOutline";
import AdminPanelSettingsOutlined from "@mui/icons-material/AdminPanelSettingsOutlined";
import Casino from "@mui/icons-material/Casino";
import DeleteForeverOutlined from "@mui/icons-material/DeleteForeverOutlined";
import GroupsOutlined from "@mui/icons-material/GroupsOutlined";
import HistoryOutlined from "@mui/icons-material/HistoryOutlined";
import HomeOutlined from "@mui/icons-material/HomeOutlined";
import LeaderboardOutlined from "@mui/icons-material/LeaderboardOutlined";
import Login from "@mui/icons-material/Login";
import Logout from "@mui/icons-material/Logout";
import MenuIcon from "@mui/icons-material/Menu";
import PersonAddAlt1Outlined from "@mui/icons-material/PersonAddAlt1Outlined";
import { pagePaths, roleNames } from "../api/account";
import { sessionPagePaths } from "../api/sessions";
import { sendRequest } from "../hooks/apiClient";
import { useCurrentUser } from "./CurrentUser";

const drawerWidth = 248;
const bottomNavigationHeight = 64;

type NavItem = { label: string; path: string; icon: React.ReactNode; exact?: boolean };

const playItems: NavItem[] = [
  { label: "Startseite", path: pagePaths.home, icon: <HomeOutlined />, exact: true },
  { label: "Neue Partie", path: sessionPagePaths.newSession, icon: <AddCircleOutline /> },
  { label: "Offene Partien", path: sessionPagePaths.openLobbies, icon: <GroupsOutlined /> },
];

const progressItems: NavItem[] = [
  { label: "Spielverlauf", path: sessionPagePaths.history, icon: <HistoryOutlined /> },
  { label: "Statistik", path: sessionPagePaths.statistics, icon: <LeaderboardOutlined /> },
];

type ApiInfo = { environment: string; showBanner: boolean };

const environmentLabels: Record<string, string> = { Development: "Entwicklung", Testing: "Testumgebung" };

/** Frame of all pages: app bar with account menu, navigation (permanent on desktop, drawer on mobile). */
const AppLayout: React.FC = () => {
  const theme = useTheme();
  const isDesktop = useMediaQuery(theme.breakpoints.up("md"));
  const { user, logout } = useCurrentUser();
  const navigate = useNavigate();
  const location = useLocation();
  const [mobileOpen, setMobileOpen] = React.useState(false);
  const [accountAnchor, setAccountAnchor] = React.useState<HTMLElement | null>(null);
  const [info, setInfo] = React.useState<ApiInfo | null>(null);

  React.useEffect(() => {
    sendRequest<ApiInfo>("GET", { serviceUrl: "/api/info" })
      .then(setInfo)
      .catch(() => undefined);
  }, []);

  const isAdmin = user?.roles.includes(roleNames.administrator) ?? false;
  const isActive = (item: NavItem) =>
    item.exact ? location.pathname === item.path : location.pathname.startsWith(item.path);

  const signOut = async () => {
    setAccountAnchor(null);
    await logout();
    navigate(pagePaths.login);
  };

  const navList = (title: string, items: NavItem[]) => (
    <List subheader={<ListSubheader sx={{ bgcolor: "transparent" }}>{title}</ListSubheader>} dense>
      {items.map((item) => (
        <ListItemButton
          key={item.path}
          href={item.path}
          selected={isActive(item)}
          onClick={() => setMobileOpen(false)}
          sx={{ mx: 1, borderRadius: 2 }}
        >
          <ListItemIcon sx={{ minWidth: 40 }}>{item.icon}</ListItemIcon>
          <ListItemText primary={item.label} />
        </ListItemButton>
      ))}
    </List>
  );

  const navigation = (
    <Box sx={{ overflowY: "auto" }}>
      <Toolbar />
      {user ? (
        <>
          {navList("Spielen", playItems)}
          {navList("Fortschritt", progressItems)}
          {isAdmin && navList("Verwaltung", [{ label: "Administration", path: pagePaths.admin, icon: <AdminPanelSettingsOutlined /> }])}
        </>
      ) : (
        navList("Willkommen", [
          { label: "Startseite", path: pagePaths.home, icon: <HomeOutlined />, exact: true },
          { label: "Anmelden", path: pagePaths.login, icon: <Login /> },
          { label: "Registrieren", path: pagePaths.register, icon: <PersonAddAlt1Outlined /> },
        ])
      )}
    </Box>
  );

  return (
    <Box sx={{ display: "flex", minHeight: "100vh" }}>
      <AppBar
        position="fixed"
        color="inherit"
        elevation={0}
        sx={{ zIndex: (t) => t.zIndex.drawer + 1, borderBottom: 1, borderColor: "divider", bgcolor: "background.paper" }}
      >
        <Toolbar sx={{ gap: 1, px: { xs: 1, sm: 2 } }}>
          {!isDesktop && (
            <IconButton edge="start" aria-label="Menü öffnen" onClick={() => setMobileOpen(true)}>
              <MenuIcon />
            </IconButton>
          )}
          <Button href={pagePaths.home} color="inherit" startIcon={<Casino color="primary" />} sx={{ fontSize: "1.15rem" }}>
            GameHub
          </Button>
          {info?.showBanner && (
            <Chip
              size="small"
              color="warning"
              label={isDesktop ? `${environmentLabels[info.environment] ?? info.environment} – keine echten Daten` : environmentLabels[info.environment] ?? info.environment}
            />
          )}
          <Box sx={{ flexGrow: 1 }} />
          {user === null && (
            <>
              <Button href={pagePaths.login} startIcon={<Login />}>
                Anmelden
              </Button>
              {isDesktop && (
                <Button href={pagePaths.register} variant="contained">
                  Registrieren
                </Button>
              )}
            </>
          )}
          {user && (
            <>
              <Tooltip title="Konto">
                <IconButton onClick={(event) => setAccountAnchor(event.currentTarget)} aria-label="Konto-Menü">
                  <Avatar sx={{ width: 34, height: 34, bgcolor: "primary.main", fontSize: "0.95rem" }}>
                    {user.displayName.slice(0, 1).toUpperCase()}
                  </Avatar>
                </IconButton>
              </Tooltip>
              <Menu anchorEl={accountAnchor} open={accountAnchor !== null} onClose={() => setAccountAnchor(null)}>
                <Box sx={{ px: 2, py: 1 }}>
                  <Typography sx={{ fontWeight: 600 }}>{user.displayName}</Typography>
                  <Typography variant="body2" color="text.secondary">
                    {user.email}
                  </Typography>
                </Box>
                <Divider />
                <MenuItem
                  onClick={() => {
                    setAccountAnchor(null);
                    navigate(pagePaths.deleteAccount);
                  }}
                >
                  <ListItemIcon>
                    <DeleteForeverOutlined fontSize="small" />
                  </ListItemIcon>
                  Konto löschen
                </MenuItem>
                <MenuItem onClick={() => void signOut()}>
                  <ListItemIcon>
                    <Logout fontSize="small" />
                  </ListItemIcon>
                  Abmelden
                </MenuItem>
              </Menu>
            </>
          )}
        </Toolbar>
      </AppBar>

      <Box component="nav" sx={{ width: { md: drawerWidth }, flexShrink: { md: 0 } }} aria-label="Hauptnavigation">
        <Drawer
          variant={isDesktop ? "permanent" : "temporary"}
          open={isDesktop || mobileOpen}
          onClose={() => setMobileOpen(false)}
          ModalProps={{ keepMounted: true }}
          sx={{ "& .MuiDrawer-paper": { width: drawerWidth, boxSizing: "border-box", borderRight: 1, borderColor: "divider" } }}
        >
          {navigation}
        </Drawer>
      </Box>

      <Box
        component="main"
        sx={{
          flexGrow: 1,
          minWidth: 0,
          px: { xs: 1.5, sm: 3, md: 4 },
          pt: { xs: 2, sm: 3, md: 4 },
          // Mobile: room for the bottom navigation (and the home indicator of phones).
          pb: { xs: user ? `calc(${bottomNavigationHeight + 16}px + env(safe-area-inset-bottom))` : 2, md: 4 },
        }}
      >
        <Toolbar />
        <Outlet />
      </Box>

      {!isDesktop && user && (
        <Paper
          elevation={8}
          sx={{ position: "fixed", bottom: 0, left: 0, right: 0, zIndex: (t) => t.zIndex.appBar, pb: "env(safe-area-inset-bottom)" }}
        >
          <BottomNavigation
            showLabels
            value={[...playItems, ...progressItems].findIndex((item) => isActive(item))}
            sx={{ height: bottomNavigationHeight, bgcolor: "background.paper" }}
          >
            {[...playItems, ...progressItems].map((item) => (
              <BottomNavigationAction
                key={item.path}
                label={item.label.replace("Offene Partien", "Lobbys").replace("Startseite", "Start").replace("Spielverlauf", "Verlauf")}
                icon={item.icon}
                href={item.path}
                sx={{ minWidth: 0, px: 0.5 }}
              />
            ))}
          </BottomNavigation>
        </Paper>
      )}
    </Box>
  );
};

export default AppLayout;
