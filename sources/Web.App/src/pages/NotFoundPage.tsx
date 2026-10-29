import Button from "@mui/material/Button";
import Page from "../components/Page";
import SectionCard from "../components/SectionCard";
import Typography from "@mui/material/Typography";
import { pagePaths } from "../api/account";

const NotFoundPage = () => (
  <Page title="Seite nicht gefunden" width="sm">
    <SectionCard>
      <Typography>Diese Seite gibt es nicht (mehr).</Typography>
      <Button href={pagePaths.home} variant="contained" sx={{ alignSelf: "flex-start" }}>
        Zur Startseite
      </Button>
    </SectionCard>
  </Page>
);

export default NotFoundPage;
