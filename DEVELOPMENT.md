# Development Notes

## Architecture

The product is WPS-only and has two local components:

1. `plugin/`: WPS Spreadsheet JavaScript add-in.
2. `helper/`: Windows per-user helper service bound to `127.0.0.1:17653`.

The helper avoids browser CORS limitations and keeps the LLM API key out of WPS JavaScript. Credentials are serialized and encrypted with Windows DPAPI `CurrentUser`.

## Build

Run from Windows PowerShell:

```powershell
.\helper\build.ps1
.\package.ps1
```

The helper targets .NET Framework 4 and is compiled with the Windows inbox C# compiler. No npm or Visual Studio installation is required.

## Local API

- `GET /health`
- `GET /settings`
- `POST /settings`
- `POST /test`
- `GET /glossary`
- `POST /glossary`
- `POST /translate`

The server listens only on IPv4 loopback and never returns the stored secret.

## WPS Deployment

The installer copies the add-in to:

```text
%APPDATA%\kingsoft\wps\jsaddons\YNWpsTranslator_1.0.0
```

It merges a `jsplugin` entry into `publish.xml`, preserving unrelated entries.
