# PCWächter Minimal Agent

## Start

```bash
dotnet run -- "https://api.pcwächter.de"
```

Mit API-Key (empfohlen):

```bash
dotnet run -- "https://api.pcwächter.de" "<AGENT_API_KEY>"
```

Alternativ über Environment:

```bash
set PCWAECHTER_AGENT_API_KEY=<AGENT_API_KEY>
dotnet run -- "https://api.pcwächter.de"
```

## Verhalten

- persistiert `device_install_id` unter `%ProgramData%/PCWaechter/device_install_id.txt`
- ruft nacheinander auf:
  - `POST /agent/register`
  - `POST /agent/heartbeat`
  - `POST /agent/inventory`
- sendet bei gesetztem Key den Header `X-Agent-Api-Key`
