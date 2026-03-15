# TTWWorker

Консольная автоматизация TikTok через **Яндекс.Браузер + Playwright**.

Теперь проект работает только в полностью автоматизированном режиме (как AutoKBWW):
- бот сам запускает `browser.exe`;
- открывает TikTok;
- выполняет автодействия через Playwright.

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
    "profileName": "Default",
    "useSystemUserData": true,
    "systemUserDataDir": "C:/Users/Администратор/AppData/Local/Yandex/YandexBrowser/User Data",
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

1. Если системный профиль занят, бот автоматически запускается через временный клон профиля.
2. Для установки/работы расширений отключены Playwright-флаги `--enable-automation` и `--disable-extensions`.
3. Во время работы команды: `like`, `stop`.
