import React from "react";
import Avatar from "@mui/material/Avatar";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import Box from "@mui/material/Box";

type AuthCardProps = React.PropsWithChildren<{
  title: string;
  icon: React.ReactNode;
  subtitle?: React.ReactNode;
  /** Links below the card (e.g. "Passwort vergessen?"). */
  footer?: React.ReactNode;
}>;

/** Centered card for the account pages (sign-in, registration, password). */
const AuthCard: React.FC<AuthCardProps> = ({ title, icon, subtitle, footer, children }) => {
  React.useEffect(() => {
    document.title = `${title} – GameHub`;
  }, [title]);

  return (
    <Box sx={{ maxWidth: 440, mx: "auto", mt: { xs: 1, sm: 4 } }}>
      <Card>
        <CardContent sx={{ p: { xs: 2.5, sm: 4 } }}>
          <Stack spacing={1} sx={{ alignItems: "center", mb: 3, textAlign: "center" }}>
            <Avatar sx={{ bgcolor: "primary.main", width: 48, height: 48 }}>{icon}</Avatar>
            <Typography variant="h2" component="h1">
              {title}
            </Typography>
            {subtitle && <Typography color="text.secondary">{subtitle}</Typography>}
          </Stack>
          {children}
        </CardContent>
      </Card>
      {footer && (
        <Stack direction="row" spacing={2} sx={{ justifyContent: "center", mt: 2, flexWrap: "wrap" }}>
          {footer}
        </Stack>
      )}
    </Box>
  );
};

export default AuthCard;
