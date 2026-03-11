# TTWWorker

Консольная автоматизация TikTok с управлением в реальном времени, ресканом JSON и независимыми профилями браузера.

## Главное

- Перед запуском выбирается профиль (`Profile 1`, `Profile 2`, ...).
- Можно создавать новые профили — каждый профиль это отдельная независимая сессия браузера.
- Автоматизация постоянно перечитывает `scenario.json` в цикле.
- Во время работы доступны команды:
  - `like` — поставить лайк сразу
  - `stop` — остановить работу

## Запуск

```bash
dotnet restore
dotnet run --project TTWWorker -- scenario.json
```

## Пункт управления

- `1` — запуск автоматизации: сначала выбор профиля, потом настройка параметров, затем старт
- `2` — настройка автоматизации через консоль
- `3` — показать текущий JSON
- `4` — управление профилями (создание/удаление)
- `0` — выход

## Настройки автоматизации (как в старой логике)

- `workDurationMinutes` — время работы в минутах
- `scrollDelayMinSeconds` / `scrollDelayMaxSeconds` — задержка между листаниями
- `likesPerPeriod` — количество лайков за период
- `likePeriodMinutes` — длина периода лайков
- лайки в периоде распределяются случайно по времени

## Пример `scenario.json`

```json
{
  "startUrl": "https://www.tiktok.com/foryou",
  "browserExecutablePath": "C:/Program Files (x86)/Yandex/YandexBrowser/Application/browser.exe",
  "automation": {
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
