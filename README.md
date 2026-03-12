# TTWWorker

Консольная автоматизация TikTok через Яндекс.Браузер с независимыми профилями и постоянными настройками за каждым профилем.

## Новый порядок работы

1. Выбираете профиль (`Profile 1`, `Profile 2`, ...).
2. Открывается TikTok сайт **без автодействий** (браузер запускается в ручном режиме, не через автоматизированный LaunchPersistentContext).
3. После открытия вводите/подтверждаете настройки автоматизации.
4. Настройки сохраняются **навсегда за этим профилем** (`automation-settings.json` внутри профиля).
5. Только после этого стартует автоматизация.

## Профили

- Профили хранятся в папке `managed-profiles` рядом с приложением.
- Можно создавать новые профили через пункт `4`.
- Каждый профиль независим от других (свои куки/сессия/настройки автоматизации).

## Команды во время работы

- Если VPN/расширения тормозят старт браузера, раннер делает повторный запуск CDP с увеличенным таймаутом и другим портом.
- `like` — поставить лайк сразу
- `stop` — остановить автоматизацию

## Запуск

```bash
dotnet restore
dotnet run --project TTWWorker -- scenario.json
```

## Пункт управления

- `1` — запустить профиль (выбор профиля -> открыть TikTok -> запрос настроек -> старт)
- `2` — настроить глобальные значения по умолчанию в `scenario.json`
- `3` — показать текущий JSON
- `4` — управление профилями
- `0` — выход

## Пример `scenario.json`

```json
{
  "startUrl": "https://www.tiktok.com/foryou",
  "browserExecutablePath": "C:/Program Files (x86)/Yandex/YandexBrowser/Application/browser.exe",
  "defaultAutomation": {
    "workDurationMinutes": 60,
    "scrollDelayMinSeconds": 3,
    "scrollDelayMaxSeconds": 12,
    "scrollActionType": "keyPress",
    "scrollKey": "ArrowDown",
    "scrollSelector": "",
    "likesPerPeriod": 2,
    "likePeriodMinutes": 10,
    "likeKey": "KeyL"
  }
}
```
