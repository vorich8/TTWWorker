# TTWWorker

Консольная автоматизация TikTok через **Dolphin Anty** + Playwright.

Теперь проект полностью опирается на Dolphin:
- запуск/остановка профиля делается через локальный API Dolphin;
- подключение к браузеру делается по CDP endpoint, который возвращает сам Dolphin;
- не нужно вручную поднимать CDP-порт и не нужно закрывать основной браузер.

## Что умеет

- Запуск профиля Dolphin из консоли.
- Открытие TikTok и пауза для ручной проверки (VPN, аккаунт, лента).
- Автоматическое листание с рандомной задержкой.
- Плановые лайки в случайные моменты внутри периода.
- Команды во время работы:
  - `like` — лайк сразу;
  - `stop` — остановить автоматизацию.
- Постоянный рескан `scenario.json` во время цикла (можно править на лету).

## Запуск

```bash
dotnet restore
dotnet run --project TTWWorker -- scenario.json
```

## Пункт управления

- `1` — запустить автоматизацию профиля Dolphin
- `2` — настроить `scenario.json`
- `3` — показать текущий JSON
- `0` — выход

## Настройка `scenario.json`

```json
{
  "startUrl": "https://www.tiktok.com/foryou",
  "dolphin": {
    "apiBaseUrl": "http://127.0.0.1:3001",
    "profileId": ""
  },
  "automation": {
    "workDurationMinutes": 60,
    "scrollDelayMinSeconds": 3,
    "scrollDelayMaxSeconds": 12,
    "scrollKey": "ArrowDown",
    "likesPerPeriod": 2,
    "likePeriodMinutes": 10,
    "likeKey": "KeyL"
  }
}
```

### Важно

1. В `dolphin.profileId` укажите ID профиля из Dolphin Anty.
2. Локальный API Dolphin должен быть доступен по `apiBaseUrl`.
3. Если API отвечает нестандартным JSON, подскажи пример ответа — быстро подгоню парсер под твою версию.
