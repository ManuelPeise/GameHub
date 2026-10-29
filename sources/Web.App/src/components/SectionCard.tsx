import React from "react";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";

type SectionCardProps = React.PropsWithChildren<{
  title?: React.ReactNode;
  icon?: React.ReactNode;
  action?: React.ReactNode;
}>;

/** Card with an optional heading (icon, title, action on the right). */
const SectionCard: React.FC<SectionCardProps> = ({ title, icon, action, children }) => (
  <Card>
    <CardContent sx={{ p: { xs: 2, sm: 3 } }}>
      {(title || action) && (
        <Stack direction="row" spacing={1.5} sx={{ mb: 2, alignItems: "center" }}>
          {icon}
          <Typography variant="h3" sx={{ flexGrow: 1 }}>
            {title}
          </Typography>
          {action}
        </Stack>
      )}
      <Stack spacing={2}>{children}</Stack>
    </CardContent>
  </Card>
);

export default SectionCard;
