# TTWWorker

Упрощённая автоматизация TikTok через **пункт управления в консоли** с постоянным ресканом `scenario.json`.

## Что теперь делает программа

- Запускается как консольный пульт (`1/2/3/0`).
- При запуске автоматизации открывает **новое окно браузера** с временным профилем.
- В рабочем цикле делает **постоянный рескан JSON** перед каждым действием.
- Нажимает действие из JSON в **рандомное время от 3 до 12 секунд** (или ваш диапазон).
- При запущенной автоматизации принимает команды из консоли в любой момент:
  - `like` — поставить лайк (нажимается клавиша из `automation.likeKey`, по умолчанию `KeyL`)
  - `stop` — остановить автоматизацию

## Запуск

```bash
dotnet restore
dotnet run --project TTWWorker -- scenario.json
```

## Пункт управления

- `1` — запустить автоматизацию
- `2` — быстрая настройка простой автоматизации
- `3` — показать текущий JSON
- `0` — выход

## Пример `scenario.json`

```json
{
  "startUrl": "https://www.tiktok.com/foryou",
  "browserExecutablePath": "C:/Program Files (x86)/Yandex/YandexBrowser/Application/browser.exe",
  "automation": {
    "minDelaySeconds": 3,
    "maxDelaySeconds": 12,
    "actionType": "keyPress",
    "key": "ArrowDown",
    "selector": "",
    "likeKey": "KeyL"
  }
}
```

## Поля JSON

- `startUrl` — стартовый URL
- `browserExecutablePath` — путь к `browser.exe`
- `automation.minDelaySeconds` / `automation.maxDelaySeconds` — диапазон случайной задержки
- `automation.actionType` — `keyPress` или `click`
- `automation.key` — клавиша для `keyPress` (например `ArrowDown`)
- `automation.selector` — CSS-селектор кнопки для `click` (если используете кнопку справа ниже центра)
- `automation.likeKey` — клавиша для команды `like` (по умолчанию `KeyL`)
