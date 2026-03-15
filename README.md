# TTWWorker

Консольная автоматизация TikTok через **Яндекс.Браузер + Playwright**.

Теперь запуск всегда идёт в полностью автоматизированном режиме с **новым пустым профилем на каждый старт**:
- бот сам запускает `browser.exe`;
- создаёт свежий `userDataDir` в `profilesRoot/runtime-fresh/...`;
- открывает TikTok и выполняет автодействия через Playwright.

Основной профиль пользователя не используется.

## Запуск

```bash
dotnet restore
dotnet run --project TTWWorker -- scenario.json
```

## Пункт управления

- `1` — запустить автоматизацию
- `2` — настроить `scenario.json`
- `3` — показать текущий JSON
- `0` — выход

## Пример `scenario.json`

```json
{
  "startUrl": "https://www.tiktok.com/foryou",
  "browser": {
    "executablePath": "C:/Program Files (x86)/Yandex/YandexBrowser/Application/browser.exe",
    "profilesRoot": "managed-profiles",
    "profileName": "Profile 1",
    "userAgent": "Mozilla/5.0 (...) YaBrowser/..."
  },
  "automation": {
    "workDurationMinutes": 60,
    "scrollDelayMinSeconds": 3,
    "scrollDelayMaxSeconds": 12,
    "scrollKey": "ArrowDown",
    "likesPerPeriod": 2,
    "likePeriodMinutes": 10,
    "likeKey": "KeyL"
  },
  "automationConfigured": true
}
```

## Важно

1. На каждый запуск создаётся новый чистый профиль, поэтому расширения/логины из основного браузера не подхватываются.
2. Для установки/работы расширений отключены Playwright-флаги `--enable-automation` и `--disable-extensions`.
3. Во время работы команды: `like`, `stop`.
