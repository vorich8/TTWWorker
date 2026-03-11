# TTWWorker

JSON-раннер автоматизации TikTok через Яндекс.Браузер.

## Важное изменение

По вашему требованию **профили Яндекса больше не используются**.

Теперь при каждом запуске создаётся **новый временный профиль** (чистая сессия), браузер открывается в новом окне и после завершения временная папка удаляется.

## Запуск

```bash
dotnet restore
dotnet run --project TTWWorker -- scenario.json
```

## Пример `scenario.json`

```json
{
  "name": "TikTok: листание ленты в новом окне",
  "startUrl": "https://www.tiktok.com/foryou",
  "loginWaitSeconds": 5,
  "slowMoMs": 60,
  "browser": {
    "executablePath": "C:/Program Files (x86)/Yandex/YandexBrowser/Application/browser.exe",
    "launchMode": "cdp",
    "cdpPort": 9222,
    "keepBrowserOpen": false,
    "forceNewWindow": true,
    "additionalArgs": []
  },
  "steps": [
    { "action": "wait", "durationMs": 2000 },
    { "action": "keyPress", "key": "ArrowDown", "afterDelayMs": 2200, "repeat": 20 }
  ]
}
```

## browser-поля

- `executablePath` — путь к `browser.exe`
- `launchMode` — `cdp` (по умолчанию) или `persistent`
- `cdpPort` — CDP порт
- `forceNewWindow` — запуск отдельного окна
- `keepBrowserOpen` — не закрывать браузер после сценария
- `additionalArgs` — дополнительные аргументы запуска

## Поддерживаемые шаги

- `wait`
- `keyPress`
- `click`
- `navigate`
