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

Integrationstests gegen PostgreSQL (`Tests.Data`) laufen nur, wenn `GAMEHUB_TEST_POSTGRES` gesetzt ist; sonst werden
sie übersprungen. Jeder Testlauf legt eine eigene Datenbank an, migriert sie und löscht sie danach:

```
$env:GAMEHUB_TEST_POSTGRES = "Host=localhost;Port=5432;Username=postgres;Password=<passwort>"
dotnet test Tests.Data
```

TypeScript-Typen aus `Shared.Models` neu erzeugen (der Test in `Tests.Shared` schlägt fehl, wenn sie veraltet sind):

```
cd sources
$env:GAMEHUB_UPDATE_CONTRACTS = "1"; dotnet test Tests.Shared; Remove-Item Env:GAMEHUB_UPDATE_CONTRACTS
```

## Docker: Testing und Live

Testing und Live laufen als getrennte Docker-Compose-Stacks (eigene Projektnamen `gamehub-testing` und
`gamehub-live`, eigene Netze, Volumes und Umgebungsdateien). Jeder Stack besteht aus:

| Dienst | Aufgabe |
| --- | --- |
| `gamehub-db` | PostgreSQL 17, nur im internen Netz erreichbar, Daten im Volume `gamehub-db-data` |
| `gamehub-migrate` | führt ausstehende Migrationen mit dem EF-Core-Migrationsbundle aus und beendet sich; in Live vorher automatisch ein Backup (`/backups/gamehub-PreMigration-<Zeit>.dump`); die App startet nur, wenn beides gelingt |
| `gamehub-app` | API mit eingebautem Frontend; Schlüssel (Anmeldungen) im Volume `gamehub-keys`, Backups in `gamehub-backups` |
| `gamehub-mail` | nur Testing: Mailpit, alle E-Mails landen im Web-UI (Standard: http://localhost:8026) |

Erstmalig einrichten (Beispiel Testing):

```
copy docker\testing.env.example docker\testing.env      # Werte anpassen, Datei bleibt lokal
docker login ghcr.io                                     # nur nötig, wenn das Paket privat ist (Token mit read:packages)
docker compose --env-file docker/testing.env -f docker/compose.testing.yaml up -d
```

Die App läuft dann unter http://localhost:8081 und zeigt das Banner „Testumgebung“. Ersten Administrator anlegen:

```
docker compose --env-file docker/testing.env -f docker/compose.testing.yaml run --rm --entrypoint dotnet gamehub-app dbtools/Services.DbTools.dll create-admin admin@example.org "Admin"
```

**Update** (neues Image nach einem Merge in `Master`, siehe CI): nur Migration und App werden ersetzt, die Datenbank läuft weiter.

```
docker compose --env-file docker/testing.env -f docker/compose.testing.yaml pull gamehub-migrate gamehub-app
docker compose --env-file docker/testing.env -f docker/compose.testing.yaml up -d
```

Ohne Registry lokal bauen: `docker compose --env-file docker/testing.env -f docker/compose.testing.yaml build`.
Live funktioniert gleich mit `docker/live.env` und `docker/compose.live.yaml` (SMTP-Anbieter in `live.env`).

## CI (GitHub Actions)

`.github/workflows/ci.yml` läuft bei jedem Push und Pull Request mit drei Jobs:

| Job | Prüft |
| --- | --- |
| Zeilenenden | keine CRLF-Dateien im Repository (außer `*.cmd`, `*.bat`, `*.sln`) |
| .NET Build und Tests | Build aller .NET-Projekte (Release), alle `Tests.*` inkl. Architektur- und Vertragstests; Ergebnisse als Artefakt `test-results` |
| Integrationstests (PostgreSQL) | `Tests.Data` gegen einen PostgreSQL-17-Dienst: Migrationen vollständig, Constraints, Repository |
| Frontend Lint, Typprüfung und Build | `npm ci`, `npm run lint`, `npm run build` (inkl. `tsc -b`) |
| Docker-Image | nur nach Push auf `Master` und grünen Checks: baut `ghcr.io/manuelpeise/gamehub-app` mit den Tags `latest` und `sha-<commit>` |

Damit ein Pull Request bei Fehlern nicht gemergt werden kann, einmalig in GitHub einrichten:
Settings → Branches → Branch protection rule für `Master` → „Require status checks to pass before merging“ →
die Checks `Zeilenenden`, `.NET Build und Tests`, `Integrationstests (PostgreSQL)` und `Frontend Lint, Typprüfung und Build` auswählen.
