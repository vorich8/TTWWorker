# TTWWorker

Консольная автоматизация TikTok через **Яндекс.Браузер + Playwright** (без Dolphin).

## Что изменено

- Убран режим Dolphin API (free-план не подходит для automation endpoint).
- Бот запускает Яндекс.Браузер напрямую через `browser.exe`.
- Используется отдельная папка профиля (`profilesRoot/profileName`), поэтому не нужно закрывать другие открытые браузеры.
- В конфиг добавлен явный `userAgent` для стабильного входа/работы аккаунта.
- Для установки расширений (например VPN) отключены Playwright-флаги `--enable-automation` и `--disable-extensions`, которые мешали установке в автоматизированном режиме.

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

1. Укажите корректный путь к `browser.exe` Яндекс.Браузера.
2. Настройте `userAgent` под ваш рабочий аккаунт/сессию.
3. На старте бот открывает TikTok, вы проверяете VPN/аккаунт и нажимаете Enter.
4. Во время работы команды в консоли: `like`, `stop`.
