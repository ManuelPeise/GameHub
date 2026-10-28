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
  GameHubApp.slnx            Solution (XML-Format)
  src/                       .NET-Projekte
  web/gamehub-web/           React + TypeScript + Vite
  tests/                     Testprojekte
docker/         Dockerfile, compose.testing.yaml, compose.live.yaml, *.env.example
user-stories/   eine Datei pro Story (US-<Nr>-<titel>.md), nur lokal – von Git ignoriert
```

Projektmappenordner in Visual Studio: 01 Core, 02 Services, 03 Logic, 04 Data, 05 Shared,
06 Tests, 07 Documentations (Zuordnung siehe Konzept, Abschnitt Projektstruktur).

## Technologie

.NET 10, ASP.NET Core, SignalR, EF Core mit PostgreSQL (Npgsql, JSON als `jsonb`),
ASP.NET Core Identity, Serilog, Aspire (lokale Orchestrierung, Compose-Erzeugung),
React mit TypeScript (strict) und Vite, Playwright, Docker Compose.
Kein ML.NET, keine externen KI-APIs.

## Befehle

Alle .NET-Befehle aus `sources/`:

```
dotnet build GameHubApp.slnx
dotnet test GameHubApp.slnx
dotnet run --project src/GameHub.AppHost      # startet DB, Mailpit, API, Frontend
```

Frontend aus `sources/web/gamehub-web/`: `npm ci`, `npm run dev`, `npm run build`, `npm run lint`.

## Architekturregeln (nicht verletzen)

1. **Ein Zugpfad:** Jede Spielaktion – Mensch oder KI, Singleplayer oder Multiplayer – läuft durch
   den `SubmitActionHandler`: laden, Zugrecht prüfen, `Validate`, `Apply`, speichern, Event senden.
2. **Spieldomäne ist rein:** `GameHub.Games.*` kennen nur `GameHub.Core`. Keine Abhängigkeit zu UI,
   Datenbank, Netzwerk oder Konfiguration. Kein generisches Regelmodell für alle Spiele.
3. **KI schlägt nur vor:** Strategien implementieren `IAiStrategy<TView, TAction>`, bekommen eine Sicht
   (nur Informationen, die auch ein Mensch hätte) und geben eine Aktion zurück. Sie ändern nie Zustand.
4. **Zufall nur über `IRandom`** mit Seed pro Partie; Würfel wirft immer die Domäne, nie die KI.
5. **Zugprotokoll ist unveränderlich:** `GameAction` wird nie geändert oder gelöscht. Zustand und
   GameAction in derselben Transaktion; Zufallsergebnisse und KI-Strategieversion mitspeichern.
6. **Optimistisches Locking** über `StateVersion` gegen doppelte Züge.
7. **Konfiguration nur über `AppSettings`** (typisiert, `ValidateOnStart`). Keine Magic Numbers für
   Zeitlimits usw. im Code; Dienste erhalten nur ihren Abschnitt per `IOptions<T>`.
8. **Keine Geheimnisse im Repo:** Passwörter und Verbindungsdaten nur über User Secrets, Aspire,
   Umgebungs- oder Release-Variablen (`App__Email__Password`). Nur `*.env.example` einchecken.
9. **Migrationen nie beim App-Start**, sondern als Migrationsbundle im Deployment.
10. **Personenbezogene Daten löschen = pseudonymisieren** (Status `Deleted`); Spielverläufe bleiben.
11. Abhängigkeitsregeln zwischen Projekten werden durch `GameHub.Architecture.Tests` geprüft.

## Konventionen

- **Zeilenenden:** LF überall (außer `*.cmd`, `*.bat`, `*.sln`), UTF-8 ohne BOM.
  Durchgesetzt über `.gitattributes` und `.editorconfig` – nie CRLF-Dateien erzeugen.
- **C#:** file-scoped Namespaces, Nullable aktiviert, Warnungen als Fehler, `sealed` wo sinnvoll,
  async durchgängig mit `CancellationToken`.
- **Namen:** Projekte `GameHub.<Bereich>`; Container `gamehub-app`, `gamehub-db`, `gamehub-migrate`;
  SignalR-Hub `GameSessionHub`.
- **TypeScript-Typen** für API und SignalR werden aus `GameHub.Contracts` erzeugt, nicht von Hand
  gepflegt. `GameHub.Contracts` zielt auf .NET Standard 2.1.
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
