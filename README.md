# TTWWorker

Консольная автоматизация TikTok через **Яндекс.Браузер + Playwright** (без Dolphin).

## Что изменено

- Убран режим Dolphin API (free-план не подходит для automation endpoint).
- Бот запускает Яндекс.Браузер напрямую через `browser.exe`.
- Можно запускать в двух режимах профиля:
  - `useSystemUserData: true` — использовать системный `User Data` + `profileName` (нужно, если расширение/VPN уже установлено в основном профиле).
  - `useSystemUserData: false` — использовать отдельный профиль в `profilesRoot`.
- В конфиг добавлен явный `userAgent` для стабильного входа/работы аккаунта.
- Для установки расширений отключены Playwright-флаги `--enable-automation` и `--disable-extensions`.
- Если системный профиль занят (основной браузер уже открыт), бот автоматически делает временную копию выбранного профиля и запускается из неё, чтобы не требовать закрытия браузера.

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

1. Если расширение/VPN работает только в основном профиле — включите `useSystemUserData: true`, `profileName: "Default"`.
2. Если хотите полностью отдельную сессию — поставьте `useSystemUserData: false`.
3. На старте бот создаёт отдельную вкладку и принудительно открывает TikTok (чтобы не оставаться на about:blank).
4. Во время работы команды в консоли: `like`, `stop`.
