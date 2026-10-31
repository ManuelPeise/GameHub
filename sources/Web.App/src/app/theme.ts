import { alpha, createTheme, darken } from "@mui/material/styles";
import "@fontsource-variable/nunito";
import LinkBehavior from "./LinkBehavior";

const primary = "#6c4cff";
const secondary = "#ff8a00";
const buttonColors = { primary, secondary, success: "#22b45a", error: "#ef4444", warning: "#f59e0b", info: "#0ea5e9" } as const;

/** Bright, child- and player-friendly theme: warm colours, round shapes, chunky "game" buttons. */
export const theme = createTheme({
  palette: {
    mode: "light",
    primary: { main: primary },
    secondary: { main: secondary, contrastText: "#ffffff" },
    success: { main: buttonColors.success, contrastText: "#ffffff" },
    info: { main: buttonColors.info },
    warning: { main: buttonColors.warning },
    error: { main: buttonColors.error },
    background: { default: "#fff6e9", paper: "#ffffff" },
    text: { primary: "#2b2250", secondary: "#6b6489" },
    divider: alpha("#2b2250", 0.1),
  },
  shape: { borderRadius: 18 },
  typography: {
    fontFamily: '"Nunito Variable", "Nunito", "Segoe UI", system-ui, -apple-system, Roboto, sans-serif',
    h1: { fontSize: "2.1rem", fontWeight: 900 },
    h2: { fontSize: "1.55rem", fontWeight: 900 },
    h3: { fontSize: "1.25rem", fontWeight: 800 },
    body1: { fontWeight: 500 },
    button: { textTransform: "none", fontWeight: 800, fontSize: "1rem" },
  },
  components: {
    MuiCssBaseline: {
      styleOverrides: {
        body: {
          backgroundImage: `radial-gradient(${alpha(primary, 0.07)} 2px, transparent 2px), radial-gradient(${alpha(secondary, 0.08)} 2px, transparent 2px)`,
          backgroundSize: "36px 36px",
          backgroundPosition: "0 0, 18px 18px",
          backgroundAttachment: "fixed",
        },
      },
    },
    MuiLink: { defaultProps: { component: LinkBehavior } },
    MuiButtonBase: { defaultProps: { LinkComponent: LinkBehavior } },
    MuiButton: {
      defaultProps: { disableElevation: true },
      styleOverrides: {
        root: { borderRadius: 999, paddingInline: 20, transition: "transform 80ms, box-shadow 80ms" },
        sizeLarge: { paddingBlock: 10, fontSize: "1.1rem" },
        outlined: { borderWidth: 2, "&:hover": { borderWidth: 2 } },
      },
      variants: Object.entries(buttonColors).map(([color, main]) => ({
        props: { variant: "contained" as const, color: color as keyof typeof buttonColors },
        style: {
          boxShadow: `0 4px 0 ${darken(main, 0.3)}`,
          "&:hover": { boxShadow: `0 4px 0 ${darken(main, 0.3)}` },
          "&:active": { transform: "translateY(3px)", boxShadow: `0 1px 0 ${darken(main, 0.3)}` },
        },
      })),
    },
    MuiCard: {
      defaultProps: { elevation: 0 },
      styleOverrides: { root: { border: `2px solid ${alpha("#2b2250", 0.08)}`, boxShadow: `0 6px 0 ${alpha("#2b2250", 0.06)}` } },
    },
    MuiPaper: { styleOverrides: { root: { backgroundImage: "none" } } },
    MuiChip: { styleOverrides: { root: { fontWeight: 800 } } },
    MuiTextField: { defaultProps: { fullWidth: true } },
    MuiOutlinedInput: { styleOverrides: { root: { borderRadius: 14, backgroundColor: "#ffffff" } } },
    MuiAppBar: { styleOverrides: { root: { backgroundImage: "none" } } },
    MuiTab: { styleOverrides: { root: { textTransform: "none", fontWeight: 800 } } },
  },
});
