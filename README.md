# TTWWorker

JSON-раннер автоматизации TikTok через Яндекс.Браузер.

## Почему был фикс

Проблема `Target page, context or browser has been closed` часто возникает на `LaunchPersistentContextAsync` из-за набора флагов запуска/блокировки профиля.

Теперь по умолчанию используется режим **CDP**: раннер запускает/подключается к браузеру через `--remote-debugging-port` и `ConnectOverCDPAsync`.

Дополнительно для поведения "как в AutoKBWW" добавлен отдельный изолированный запуск:
- старт в **новом окне** (`forceNewWindow: true`),
- запуск через **копию профиля** (`cloneProfileForCdp: true`),
- при занятом `cdpPort` автоматически выбирается следующий свободный порт.

Это предотвращает привязку к уже открытому основному браузеру.

## Запуск

```bash
dotnet restore
dotnet run --project TTWWorker -- scenario.json
```

## Формат `scenario.json`

```json
{
  "name": "TikTok: листание ленты через профиль Яндекс.Браузера",
  "startUrl": "https://www.tiktok.com/foryou",
  "loginWaitSeconds": 5,
  "slowMoMs": 60,
  "browser": {
    "executablePath": "%LOCALAPPDATA%/Yandex/YandexBrowser/Application/browser.exe",
    "userDataDir": "%LOCALAPPDATA%/Yandex/YandexBrowser/User Data",
    "profileDirectoryName": "Default",
    "launchMode": "cdp",
    "cdpPort": 9222,
    "keepBrowserOpen": false,
    "forceNewWindow": true,
    "cloneProfileForCdp": true,
    "useProfileClone": true,
    "additionalArgs": []
  },
  "steps": [
    { "action": "wait", "durationMs": 2000 },
    { "action": "keyPress", "key": "ArrowDown", "afterDelayMs": 2200, "repeat": 20 }
  ]
}
```

## browser-поля

- `launchMode`: `cdp` (по умолчанию) или `persistent`
- `cdpPort`: базовый порт CDP (например `9222`)
- `forceNewWindow`: принудительно запускать отдельное окно браузера для CDP
- `cloneProfileForCdp`: создавать временную копию профиля для CDP (изоляция от основного браузера)
- `keepBrowserOpen`: не закрывать процесс браузера после сценария
- `useProfileClone`: fallback копия профиля для `persistent`-режима
- `additionalArgs`: дополнительные аргументы запуска браузера

## Поддерживаемые шаги

- `wait`
- `keyPress`
- `click`
- `navigate`

## Логи

Логи и ошибки — на русском, включая диагностику путей браузера, портов CDP и режима запуска.
