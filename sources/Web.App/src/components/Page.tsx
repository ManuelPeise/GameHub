import React from "react";
import Box from "@mui/material/Box";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";

type PageProps = React.PropsWithChildren<{
  title: React.ReactNode;
  subtitle?: React.ReactNode;
  /** Buttons on the right of the title. */
  actions?: React.ReactNode;
  /** Narrow pages (forms) use "sm". */
  width?: "sm" | "md" | "lg";
}>;

const maxWidths = { sm: 520, md: 900, lg: 1200 } as const;

/** Common page frame: title, optional subtitle and actions, content. */
const Page: React.FC<PageProps> = ({ title, subtitle, actions, width = "md", children }) => {
  React.useEffect(() => {
    if (typeof title === "string") {
      document.title = `${title} – GameHub`;
    }
  }, [title]);

  return (
    <Box sx={{ maxWidth: maxWidths[width], mx: "auto", width: "100%" }}>
      <Stack
        direction={{ xs: "column", sm: "row" }}
        spacing={2}
        sx={{ mb: { xs: 2, sm: 3 }, alignItems: { sm: "center" }, justifyContent: "space-between" }}
      >
        <Box>
          <Typography variant="h1" sx={{ fontSize: { xs: "1.6rem", sm: "2rem" } }}>
            {title}
          </Typography>
          {subtitle && (
            <Typography color="text.secondary" sx={{ mt: 0.5 }}>
              {subtitle}
            </Typography>
          )}
        </Box>
        {actions && (
          <Stack direction="row" spacing={1} sx={{ flexShrink: 0, "& > *": { flexGrow: { xs: 1, sm: 0 } } }}>
            {actions}
          </Stack>
        )}
      </Stack>
      <Stack spacing={{ xs: 2, sm: 3 }}>{children}</Stack>
    </Box>
  );
};

export default Page;
