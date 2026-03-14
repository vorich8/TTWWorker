# TTWWorker

Консольная автоматизация TikTok через **Яндекс.Браузер + Playwright**.

## Режимы работы

1. **Playwright persistent (по умолчанию, полный функционал)**
   - `useMainBrowserInputMode: false`
   - бот сам запускает `browser.exe` и управляет страницей через Playwright.

2. **Основной браузер (резервный fallback)**
   - `useMainBrowserInputMode: true`
   - вы вручную открываете TikTok в основном браузере,
   - бот отправляет в активное окно только клавиши (листание/лайк).

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
    "useMainBrowserInputMode": false,
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

1. Если нужен полный функционал автоматизации — оставьте `useMainBrowserInputMode: false`.
2. Режим `true` используйте только как fallback, если отдельное окно не стартует.
3. Во время работы команды: `like`, `stop`.
