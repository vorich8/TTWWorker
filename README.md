# TTWWorker

Консольная автоматизация TikTok через **Яндекс.Браузер + Playwright**.

## Что сделано

- Полностью автоматизированный запуск браузера.
- На каждый старт создаётся новый чистый профиль (`profilesRoot/runtime-fresh/...`).
- **Настройки действий хранятся по профилям** в `profilesRoot/profile-settings/<profile>.json`.
- Лайк выполняется рандомно: **через кнопку лайка или через клавишу `L`** (каждый раз случайно).
- Листание выполняется **только кликом по кнопке-стрелке** (через `scrollSelector`).

## Запуск

```bash
dotnet restore
dotnet run --project TTWWorker -- scenario.json
```

## Пункт управления

- `1` — запустить автоматизацию
- `2` — настроить `scenario.json` + действия для выбранного профиля
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
    "scrollSelector": "button[data-e2e='arrow-right']",
    "likesPerPeriod": 2,
    "likePeriodMinutes": 10,
    "likeKey": "KeyL",
    "likeButtonSelector": "button[data-e2e='like-icon']",
    "allowLikeButton": true,
    "allowLikeKey": true
  },
  "automationConfigured": true
}
```

## Важно

1. Для скролла укажите корректный `scrollSelector` (кнопка-стрелка справа ниже середины).
2. Для лайка можно включить оба режима (`allowLikeButton` и `allowLikeKey`) — бот будет случайно выбирать между ними.
3. Команды во время работы: `like`, `stop`.
