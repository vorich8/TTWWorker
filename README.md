# TTWWorker

JSON-раннер автоматизации TikTok с запуском через **Яндекс.Браузер** и сохранённый профиль.

## Что изменено

Проект больше не зависит от скачанного Playwright Chromium для запуска сценария: теперь используется установленный в системе `browser.exe` Яндекс.Браузера, `userDataDir` и имя профиля (`Default`, `Profile 1` и т.д.) из `scenario.json`.

Это позволяет использовать уже сохранённую сессию (авторизацию) профиля.

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
    "executablePath": "C:/Users/Administrator/AppData/Local/Yandex/YandexBrowser/Application/browser.exe",
    "userDataDir": "C:/Users/Administrator/AppData/Local/Yandex/YandexBrowser/User Data",
    "profileDirectoryName": "Default"
  },
  "steps": [
    { "action": "wait", "durationMs": 2000 },
    { "action": "keyPress", "key": "ArrowDown", "afterDelayMs": 2200, "repeat": 20 }
  ]
}
```

## Поддерживаемые шаги

- `wait` — ожидание `durationMs`
- `keyPress` — нажатие клавиши `key`
- `click` — клик по CSS-селектору `selector`
- `navigate` — переход по URL `url`

Общие поля шага:
- `afterDelayMs`
- `repeat`

## Логи

Все основные сообщения раннера выводятся на русском языке (загрузка сценария, запуск браузера, выполнение шагов, ошибки валидации).
