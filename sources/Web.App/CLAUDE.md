# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Gilt für das Frontend in `sources/Web.App/`. Die `CLAUDE.md` in der Repo-Wurzel
bleibt maßgeblich für Architektur, Sprachregeln (Antworten/Doku Deutsch, Code/Bezeichner/Kommentare
Englisch) und die Gesamt-Arbeitsweise.

## Befehle (aus `sources/Web.App/`)

```
npm install        # einmalig / nach Änderungen an package.json – erzeugt package-lock.json
npm ci             # reproduzierbare Installation, sobald package-lock.json existiert
npm run dev        # Vite-Dev-Server auf Port 57764 (siehe vite.config.ts)
npm run build      # tsc -b (Type-Check über Projektreferenzen) + vite build nach dist/
npm run lint       # ESLint (flat config) über das gesamte Projekt
npm run preview    # gebautes dist/ lokal ausliefern
```

Einen einzelnen Test gibt es nicht: Dieses Projekt enthält derzeit **kein** Test-Setup.

## Aufbau

- **UI:** Material UI (`@mui/material`, Icons aus `@mui/icons-material`), dunkles Theme in `src/app/theme.ts`.
  Keine eigenen CSS-Dateien; Styling über `sx` und Theme. **Mobile first:** Layouts zuerst für das Handy
  (eine Spalte, große Touch-Flächen), ab `sm`/`md` erweitern.
- **Routing:** React Router (`react-router`), Routen in `src/pages/App.tsx`. Links und Buttons mit `href`
  navigieren über `src/app/LinkBehavior.tsx` ohne Neuladen; im Code `useNavigate()` statt `window.location`.
- **Layout:** `src/app/AppLayout.tsx` – App-Leiste mit Konto-Menü und Umgebungs-Chip, Seitenmenü
  (Desktop dauerhaft, Handy als Schublade) und untere Navigationsleiste auf dem Handy.
- **Angemeldeter Benutzer:** `useCurrentUser()` aus `src/app/CurrentUser.tsx` (nach Login/Logout `reload()`).
- **Bausteine:** `components/Page` (Seitenrahmen), `SectionCard`, `Loading`, `StatusChip`, `TurnTimer`,
  `ConnectionBanner`; Spiele unter `src/games/` (gemeinsam: `games/GameParts.tsx`).
- **API:** `hooks/apiClient.ts` (`sendRequest`, `getErrorMessage`); Typen aus `src/api/generated/contracts.ts`
  (aus `Shared.Models` erzeugt), Echtzeit über `hooks/gameHub.ts`.

## Technik- und Konfigurationshinweise

- **React 19 + TypeScript + Vite 8**, ES-Module (`"type": "module"`). Einstieg `index.html` → `src/main.tsx`.
- **TypeScript-Build läuft über Projektreferenzen** (`tsconfig.json` → `tsconfig.app.json` für `src/`,
  `tsconfig.node.json` für Config-Dateien). `tsc` emittiert nichts (`noEmit`); Vite bündelt. Beim Ändern
  von `compilerOptions` immer die passende Teil-Datei bearbeiten, nicht die Wurzel-`tsconfig.json`.
- **TypeScript-Version ist auf `~5.9` gepinnt**, weil `typescript-eslint` als Peer `typescript < 6.1`
  verlangt. `typescript@7` führt zu `ERESOLVE` – vor einem Upgrade zuerst typescript-eslint-Kompatibilität prüfen.
- **ESLint nutzt die flache Config** (`eslint.config.js`) mit `js`, `typescript-eslint` (nicht type-aware),
  `react-hooks` und `react-refresh`; `dist/` ist ignoriert.
- Die aus den DTOs in `Shared.Models` generierten API-/SignalR-Typen werden hier eingebunden, **nicht von Hand
  gepflegt** (siehe Wurzel-`CLAUDE.md`).

