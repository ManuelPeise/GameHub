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

## Stand des Projekts

Mit `create-vite` (Template `react-ts`) erzeugt und in die Solution eingebunden (`Web.App.esproj`,
Projektmappenordner 01 Web). Aufbau von `src/`:

- `main.tsx` – Einstieg, rendert `pages/App.tsx`
- `pages/` – Seiten (derzeit nur `App.tsx` als Platzhalter)
- `hooks/` – API-Zugriff: `apiClient.ts` (`sendRequest`), `useStatefulApi` (mit Lade-/Fehlerzustand),
  `useStatelessApi` (ohne eigenen Zustand), Typen in `hooks/types/api.ts`

Noch keine Routen, kein State-Management, keine SignalR-Anbindung, kein Dev-Proxy zur API und kein
Test-Setup. Geplante Ordner (siehe Konzept): `src/api/generated/`, `src/games/tictactoe/`,
`src/games/kniffel/`, `src/admin/`, `e2e/` (Playwright).

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

