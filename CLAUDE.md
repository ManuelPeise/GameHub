# CLAUDE.md – GameHub

Hinweise für Claude (und andere Mitwirkende) zur Arbeit in diesem Repository.
Antworte und dokumentiere auf **Deutsch**; Code, Bezeichner und Kommentare im Code auf **Englisch**.

## Projekt

GameHub ist eine Web-Spieleplattform. MVP: **Tic-Tac-Toe** und **Kniffel**, jeweils im Singleplayer
gegen eine regelbasierte KI (Einfach, Mittel, Schwer) und im Multiplayer, mit Benutzerkonten,
Administration und getrennten Umgebungen Development, Testing, Live.
Ausblick (nicht MVP): lernende KI, Quizspiel mit 3D-Darstellung.

- Konzept (maßgeblich für Architektur und Entscheidungen): https://claude.ai/artifact/6SecMcC4JmEeV8V9KAaYjR
- Anforderungen: `user-stories/` (nur lokal, von Git ignoriert) – Übersicht, Reihenfolge und Definition of Done in `user-stories/README.md`
- Einzelentwickler-Projekt; Repo: github.com/ManuelPeise/GameHub, Hauptzweig `Master`

## Repository-Struktur

```
sources/        gesamter Quellcode
  GameHubApp.slnx            Solution (XML-Format); alle Projekte liegen direkt in sources/
  Web.App/                   React + TypeScript + Vite (Web.App.esproj)
  Services.*/                Api, AppHost, ServiceDefaults, DbTools
  Logic.*/                   Core, TicTacToe, Kniffel, Shared, Sessions, Users, Administration
  Data.*/                    Database, DataAccessor
  Shared.Models/             DTOs für API/SignalR, AppSettings
  Tests.*/                   TicTacToe, Kniffel, Sessions, Architecture, Integration
docker/         Dockerfile, compose.testing.yaml, compose.live.yaml, *.env.example
user-stories/   eine Datei pro Story (US-<Nr>-<titel>.md), nur lokal – von Git ignoriert
```

Projektnamen folgen dem Schema `<Bereich>.<Name>`; der Bereich entspricht dem Projektmappenordner:
01 Web, 02 Services, 03 Logic, 04 Data, 05 Shared, 06 Tests, 07 Documentations.
Bereits vorhanden: Web.App, Logic.Shared, Data.Database, Data.DataAccessor, Shared.Models.
Die übrigen Projekte werden mit ihren User Stories angelegt (Zuordnung im Konzept, Abschnitt Projektstruktur).

| Projekt | Darf referenzieren |
| --- | --- |
| Logic.Core | nichts |
| Logic.TicTacToe, Logic.Kniffel | nur Logic.Core |
| Shared.Models | nur Konfigurations-Abstraktionen |
| Logic.Shared | Shared.Models |
| Logic.Sessions, Logic.Users, Logic.Administration | Logic.Core, Logic.Shared, Logic.TicTacToe, Logic.Kniffel, Shared.Models |
| Data.Database | Shared.Models |
| Data.DataAccessor | Data.Database, Logic.Shared, Shared.Models |
| Services.Api | Logic.*, Data.DataAccessor, Shared.Models, Services.ServiceDefaults |
| Services.DbTools | Logic.Users, Data.DataAccessor, Shared.Models |
| Services.AppHost | Services.Api, Services.DbTools, Web.App (nur Orchestrierung) |
| Web.App | nur die API über HTTP/SignalR |

## Technologie

.NET 10, ASP.NET Core, SignalR, EF Core mit PostgreSQL (Npgsql, JSON als `jsonb`),
ASP.NET Core Identity, eigener Datenbank-Logging-Provider (Microsoft.Extensions.Logging), Aspire (lokale Orchestrierung, Compose-Erzeugung),
React mit TypeScript (strict) und Vite, Playwright, Docker Compose.
Kein ML.NET, keine externen KI-APIs.

## Befehle

Alle .NET-Befehle aus `sources/`:

```
dotnet build GameHubApp.slnx
dotnet test GameHubApp.slnx
dotnet run --project Services.AppHost         # startet DB, Mailpit, API, Web.App
```

Frontend aus `sources/Web.App/`: `npm ci`, `npm run dev`, `npm run build`, `npm run lint`.
Details zum Frontend in `sources/Web.App/CLAUDE.md`.

## Architekturregeln (nicht verletzen)

1. **Ein Zugpfad:** Jede Spielaktion – Mensch oder KI, Singleplayer oder Multiplayer – läuft durch
   den `SubmitActionHandler`: laden, Zugrecht prüfen, `Validate`, `Apply`, speichern, Event senden.
2. **Spieldomäne ist rein:** `Logic.TicTacToe`, `Logic.Kniffel` (und künftige Spiele) kennen nur `Logic.Core`. Keine Abhängigkeit zu UI,
   Datenbank, Netzwerk oder Konfiguration. Kein generisches Regelmodell für alle Spiele.
3. **KI schlägt nur vor:** Strategien implementieren `IAiStrategy<TView, TAction>`, bekommen eine Sicht
   (nur Informationen, die auch ein Mensch hätte) und geben eine Aktion zurück. Sie ändern nie Zustand.
4. **Zufall nur über `IRandom`** mit Seed pro Partie; Würfel wirft immer die Domäne, nie die KI.
5. **Zugprotokoll ist unveränderlich:** `GameAction` wird nie geändert oder gelöscht. Zustand und
   GameAction in derselben Transaktion; Zufallsergebnisse und KI-Strategieversion mitspeichern.
6. **Optimistisches Locking** über `StateVersion` gegen doppelte Züge.
7. **Konfiguration nur über `IAppSettings`** (`Shared.Models`): typisierte Abschnitte, beim Start validiert.
   Nur `AppSettings` liest `IConfiguration`; alle anderen Klassen erhalten `IAppSettings`, nie
   `IConfiguration`. Keine Magic Numbers für Zeitlimits usw. im Code. Spielprojekte kennen keine Konfiguration.
8. **Keine Geheimnisse im Repo:** Passwörter und Verbindungsdaten nur über User Secrets, Aspire,
   Umgebungs- oder Release-Variablen (`App__Email__Password`). Nur `*.env.example` einchecken.
9. **Migrationen nie beim App-Start**, sondern als Migrationsbundle im Deployment.
10. **Personenbezogene Daten löschen = pseudonymisieren** (Status `Deleted`); Spielverläufe bleiben.
11. Abhängigkeitsregeln (Tabelle oben) werden durch `Tests.Architecture` geprüft.

## Konventionen

- **Zeilenenden:** LF überall (außer `*.cmd`, `*.bat`, `*.sln`), UTF-8 ohne BOM.
  Durchgesetzt über `.gitattributes` und `.editorconfig` – nie CRLF-Dateien erzeugen.
- **C#:** file-scoped Namespaces, Nullable aktiviert, Warnungen als Fehler, `sealed` wo sinnvoll,
  async durchgängig mit `CancellationToken`.
- **Namen:** Projekte `<Bereich>.<Name>` direkt unter `sources/`; Namespace = Projektname plus Ordner
  (z. B. `Logic.Sessions.Commands`); abstrakte Basisklassen mit Präfix `A` (z. B. `ALogicBase`),
  Interfaces mit `I`. Container `gamehub-app`, `gamehub-db`, `gamehub-migrate`; SignalR-Hub `GameSessionHub`.
- **TypeScript-Typen** für API und SignalR werden aus den DTOs in `Shared.Models` erzeugt, nicht von
  Hand gepflegt.
- **UI-Texte** auf Deutsch.

## Tests

- Regeln und KI ohne UI, Datenbank und Netzwerk testen; Zufall mit festem Seed.
- Pflicht je Spiel: alle Regeln, KI liefert nur gültige Aktionen (Property-Test), Replay ergibt exakt
  den gespeicherten Zustand. Tic-Tac-Toe Schwer verliert in erschöpfender Prüfung nie.
- KI-Verzögerung (`Ai.ActionDelay`) in Tests auf 0.
- Integrationstests mit `Aspire.Hosting.Testing` und echtem PostgreSQL.

## Arbeitsweise

- Vor der Umsetzung die betroffene User Story lesen; Akzeptanzkriterien sind die Abnahme.
- Branch je Story: `feature/US-<Nr>-<titel>`; Commit-Nachrichten auf Deutsch mit Story-Nummer,
  z. B. `US-019: Minimax-Strategie für Tic-Tac-Toe`.
- Nach Abschluss: Kriterien in der Story abhaken, Status in Story und `user-stories/README.md` setzen.
- Abweichungen vom Konzept nicht stillschweigend umsetzen, sondern nachfragen.
- Nicht ohne Auftrag committen, pushen oder Abhängigkeiten aktualisieren.
