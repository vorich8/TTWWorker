# TTWWorker

JSON-раннер автоматизации TikTok с запуском через **Яндекс.Браузер** и сохранённый профиль.

## Что изменено

Проект запускает установленный в системе Яндекс.Браузер в persistent-режиме Playwright и использует папку профилей (`User Data`) + имя профиля (`Default`, `Profile 1` и т.д.) из `scenario.json`.

Если путь в `browser.executablePath` не найден, раннер пробует типовые пути установки автоматически и выводит список проверенных путей в лог.

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

Все основные сообщения раннера выводятся на русском языке (загрузка сценария, найденные пути браузера, выполнение шагов, ошибки валидации).
