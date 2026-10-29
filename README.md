# GameHub

Web-Spieleplattform mit Tic-Tac-Toe und Kniffel – im Singleplayer gegen eine KI und im Multiplayer.

Technik: .NET 10 (ASP.NET Core, SignalR, EF Core), PostgreSQL, React mit TypeScript und Vite,
Aspire für die lokale Entwicklung. Hinweise für die Mitarbeit stehen in [CLAUDE.md](CLAUDE.md).

## Voraussetzungen

| Werkzeug | Version | Wofür |
| --- | --- | --- |
| .NET SDK | 10.0 (siehe `sources/global.json`) | Backend, Aspire-AppHost |
| Node.js mit npm | 22 oder neuer | Web.App (Vite) |
| Docker Desktop (oder Podman) | aktuell, muss laufen | PostgreSQL- und Mailpit-Container |
| Visual Studio 2026 oder `dotnet` CLI | – | Starten und Debuggen |

Einmalig das HTTPS-Entwicklungszertifikat vertrauen: `dotnet dev-certs https --trust`.

## Starten

Alles startet gemeinsam über das Aspire-AppHost:

```
cd sources
dotnet run --project Services.AppHost
```

In Visual Studio `Services.AppHost` als Startprojekt wählen und F5 drücken.

Das Aspire-Dashboard öffnet sich im Browser. Dort sind alle Ressourcen mit Logs, Traces und Links:

| Ressource | Inhalt |
| --- | --- |
| `postgres` / `gamehub` | PostgreSQL mit der Datenbank `gamehub` |
| `mailpit` | fängt alle E-Mails ab; Web-Oberfläche über den Link im Dashboard |
| `api` | Services.Api, Health Check unter `/health` |
| `web` | Web.App (Vite); Aufrufe von `/api` und `/hubs` werden an die API weitergeleitet |

Verbindungsdaten und das Datenbank-Passwort setzt Aspire automatisch; das Passwort liegt in den
User Secrets von `Services.AppHost`, nicht im Repository.

## Entwicklungskonten

Beim Start legt die Ressource `migrate` in Development drei bestätigte Konten an, falls sie fehlen:

| Konto | Rolle |
| --- | --- |
| `admin@gamehub.local` | Administrator (und Spieler) |
| `spieler1@gamehub.local` | Spieler |
| `spieler2@gamehub.local` | Spieler |

Das gemeinsame Passwort erzeugt Aspire einmalig und speichert es in den User Secrets von
`Services.AppHost`; im Dashboard unter „Parameter“ (`seed-password`) einsehbar. Für den Admin-Bereich
ist bei der ersten Anmeldung die Einrichtung einer Authenticator-App nötig.

## Ersten Administrator anlegen (Testing, Live)

```
dotnet run --project Services.DbTools -- create-admin admin@example.org "Anzeigename"
```

Das Passwort kommt aus der Umgebungsvariable `GAMEHUB_ADMIN_PASSWORD` oder wird erzeugt und einmalig
ausgegeben. Die Verbindung zur Datenbank kommt aus `ConnectionStrings__gamehub`.

## Datenbank zurücksetzen

Die Daten liegen im Docker-Volume `gamehub-postgres-data` und bleiben zwischen den Starts erhalten.
Zum Zurücksetzen das AppHost beenden, dann den PostgreSQL-Container (im Namen steht `postgres`)
und das Volume löschen:

```
docker ps -a --filter "name=postgres"
docker rm -f <container-name>
docker volume rm gamehub-postgres-data
```

Beim nächsten Start wird eine leere Datenbank angelegt.

## Tests

```
cd sources
dotnet test GameHubApp.slnx
```

Frontend: in `sources/Web.App` `npm run build` und `npm run lint`.

TypeScript-Typen aus `Shared.Models` neu erzeugen (der Test in `Tests.Shared` schlägt fehl, wenn sie veraltet sind):

```
cd sources
$env:GAMEHUB_UPDATE_CONTRACTS = "1"; dotnet test Tests.Shared; Remove-Item Env:GAMEHUB_UPDATE_CONTRACTS
```

## CI (GitHub Actions)

`.github/workflows/ci.yml` läuft bei jedem Push und Pull Request mit drei Jobs:

| Job | Prüft |
| --- | --- |
| Zeilenenden | keine CRLF-Dateien im Repository (außer `*.cmd`, `*.bat`, `*.sln`) |
| .NET Build und Tests | Build aller .NET-Projekte (Release), alle `Tests.*` inkl. Architektur- und Vertragstests; Ergebnisse als Artefakt `test-results` |
| Frontend Lint, Typprüfung und Build | `npm ci`, `npm run lint`, `npm run build` (inkl. `tsc -b`) |

Damit ein Pull Request bei Fehlern nicht gemergt werden kann, einmalig in GitHub einrichten:
Settings → Branches → Branch protection rule für `Master` → „Require status checks to pass before merging“ →
die drei Checks `Zeilenenden`, `.NET Build und Tests` und `Frontend Lint, Typprüfung und Build` auswählen.
