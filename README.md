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
