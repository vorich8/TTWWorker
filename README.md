# TTWWorker

Консольная автоматизация TikTok через **Яндекс.Браузер + Playwright**.

## Важное обновление

Добавлен режим **основного браузера** (по умолчанию):
- `browser.useMainBrowserInputMode: true`
- бот НЕ запускает новый браузерный процесс;
- вы вручную открываете TikTok в основном браузере;
- бот отправляет нажатия клавиш (листание/лайк) в активное окно.

Это сделано как fallback, когда запуск отдельного автоматизированного окна нестабилен.

## Режимы работы

1. **Основной браузер (рекомендуется сейчас)**
   - `useMainBrowserInputMode: true`
   - работает прямо в уже открытом браузере.

2. **Playwright persistent**
   - `useMainBrowserInputMode: false`
   - бот запускает `browser.exe` сам (с системным User Data или отдельным профилем).

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
    "useMainBrowserInputMode": true,
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

1. Для режима основного браузера держите окно TikTok активным (в фокусе).
2. Во время работы доступны команды: `like`, `stop`.
3. Если нужна старая схема отдельного окна — отключите `useMainBrowserInputMode`.
