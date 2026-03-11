# TTWWorker

JSON-driven TikTok automation worker.

## What changed

Project now runs automation steps from `scenario.json` and executes them in a separate visible browser window using Playwright.

## Run

```bash
dotnet restore
cd TTWWorker
pwsh bin/Debug/net10.0/playwright.ps1 install chromium
# or after publish/build use the generated playwright script in output folder

dotnet run -- scenario.json
```

You can pass a custom file:

```bash
dotnet run -- my-scenario.json
```

## JSON format

```json
{
  "name": "TikTok: open and scroll videos",
  "startUrl": "https://www.tiktok.com/foryou",
  "loginWaitSeconds": 30,
  "slowMoMs": 80,
  "steps": [
    { "action": "wait", "durationMs": 2000 },
    { "action": "keyPress", "key": "ArrowDown", "afterDelayMs": 2500, "repeat": 20 }
  ]
}
```

Supported actions:
- `wait`: waits for `durationMs`
- `keyPress`: sends keyboard key from `key`
- `click`: clicks element by `selector`
- `navigate`: opens `url`

Shared optional fields:
- `afterDelayMs`
- `repeat`

