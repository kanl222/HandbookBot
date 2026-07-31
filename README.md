# HandbookBot

Справочный бот для ГАУЗ «Областной аптечный склад» (ГАУЗ ОАС).  
Предоставляет информацию о льготных препаратах, аптечных пунктах, FAQ и контактах учреждения.

Поддерживает одновременную работу в **Telegram** и **MAX** (VK Teams).

---

## Содержание

- [Возможности](#возможности)
- [Архитектура](#архитектура)
- [Структура решения](#структура-решения)
- [Быстрый старт](#быстрый-старт)
  - [Получение токенов](#получение-токенов)
  - [Сертификаты Минцифры (MAX)](#сертификаты-минцифры-max)
- [Запуск через Docker](#запуск-через-docker)
- [Слой данных](#слой-данных)
- [Команды бота](#команды-бота)
- [Добавление новой платформы](#добавление-новой-платформы)

---

## Возможности

| Функция | Описание |
|---------|----------|
|  Список препаратов | Постраничный список льготных препаратов с ценой и наличием |
|  Поиск препарата | Двухшаговый диалоговый поиск по названию |
|  Аптечные пункты | Список пунктов ГАУЗ ОАС с адресами и телефонами |
|  Карта аптеки | Отправка геолокации выбранного пункта |
|  FAQ | Интерактивный список часто задаваемых вопросов |
|  Инструкция | Инструкция по пользованию ботом |
|  Контакты | Адрес, телефон, e-mail, сайт ГАУЗ ОАС |
|  Healthcheck | Эндпоинт `GET /health` для мониторинга |

---

## Архитектура

Проект построен по принципу **Ports & Adapters (Hexagonal Architecture)**:

```
┌─────────────────────────────────────────────────┐
│               HandbookBot (Host)                │
│     Program.cs — DI-корень, конфигурация        │
└────────┬────────────────────────────────────────┘
         │
         ├── HandbookBot.Core          (Ядро — бизнес-логика)
         │     ├── Interfaces/         (порты: IMessagingPlatform, IPharmacyRepository…)
         │     ├── Models/             (BotContext, IncomingMessage, BotKeyboard…)
         │     ├── Commands/           (StartCommand, PreparationsListCommand…)
         │     └── Services/           (CommandDispatcher, CommandFactory, InMemoryUserSessionStore)
         │
         ├── HandbookBot.Data          (Адаптер данных)
         │     ├── InMemory/           (заглушки с тестовыми данными)
         │     └── EfCore/             (EF Core реализации, BotDbContext)
         │
         ├── HandbookBot.Telegram      (Адаптер Telegram)
         │     ├── TelegramPlatformAdapter.cs
         │     └── TelegramPollingWorker.cs
         │
         ├── HandbookBot.Max           (Адаптер MAX / VK Teams)
         │     ├── MaxPlatformAdapter.cs
         │     └── MaxPollingWorker.cs
         │
         └── MAX.Bot                   (SDK MAX Bot API, git-submodule / локальная копия)
```

**Принцип изоляции**: команды бота (`Core`) не знают ни о Telegram, ни о MAX — они работают только с абстракцией `IMessagingPlatform`. Адаптеры платформ не знают о бизнес-логике.

**Жизненный цикл входящего сообщения:**

```
Telegram / MAX API
       │  (Long Polling)
       ▼
PollingWorker
       │  MapUpdate() → IncomingMessage
       ▼
CommandDispatcher
       │  Resolve() via Keyed DI
       ▼
IBotCommand.ExecuteAsync(BotContext, IncomingMessage)
       │
       ▼
IMessagingPlatform.SendTextAsync() → Telegram / MAX API
```

---

## Структура решения

```
HandbookBot2/
├── HandbookBot/                # Host-проект (точка входа)
│   ├── Program.cs
│   ├── CommandsRegistration.cs
│   ├── appsettings.json
│   └── appsettings.Development.json
├── HandbookBot.Core/           # Бизнес-логика (без зависимостей от платформ)
│   ├── Commands/
│   ├── Interfaces/
│   ├── Models/
│   └── Services/
├── HandbookBot.Data/           # Слой данных
│   ├── EfCore/                 # EF Core реализации
│   └── InMemory/               # InMemory заглушки
├── HandbookBot.Telegram/       # Адаптер Telegram
├── HandbookBot.Max/            # Адаптер MAX (VK Teams)
├── MAX.Bot/                    # SDK MAX Bot API
├── Dockerfile
├── .dockerignore
└── HandbookBot.slnx
```

---

## Быстрый старт

### Требования

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Токен Telegram-бота и/или MAX-бота (см. ниже)

### Установка

```bash
git clone <url>
cd HandbookBot2
```

### Получение токенов

#### Telegram

1. Откройте [@BotFather](https://t.me/BotFather) в Telegram
2. Отправьте команду `/newbot`
3. Укажите имя и username бота (username должен оканчиваться на `bot`)
4. BotFather выдаст токен вида `123456789:AAF...` — скопируйте его

> **Совет**: чтобы бот показывал кнопки-команды в меню Telegram, отправьте BotFather команду `/setcommands` и добавьте `start - Главное меню`.

#### MAX (VK Teams)

1. Перейдите в [Личный кабинет разработчика MAX](https://dev.max.ru)
2. Создайте нового бота и получите токен
3. Токен выглядит как длинная строка — скопируйте его

### Настройка токенов (User Secrets — рекомендуется)

```bash
cd HandbookBot
dotnet user-secrets set "Telegram:Token" "ВАШ_TELEGRAM_ТОКЕН"

# Для MAX (если нужен):
dotnet user-secrets set "Max:Token" "ВАШ_MAX_ТОКЕН"
dotnet user-secrets set "Max:Enabled" "true"
```

### Сертификаты Минцифры (MAX)

API MAX работает на `https://platform-api2.max.ru` и использует TLS-сертификаты **Национального удостоверяющего центра Минцифры** (Russian Trusted CA). Этого корневого сертификата нет в стандартном хранилище доверенных CA Windows и .NET, поэтому без его установки запросы будут завершаться ошибкой SSL/TLS.

Добавьте сертификаты Минцифры в доверенные на машине, где запускается бот. Инструкция и файлы сертификатов — на портале Госуслуг: [gosuslugi.ru/crt](https://www.gosuslugi.ru/crt).

<details>
<summary>Быстрая установка (Windows)</summary>

1. Скачайте файлы `russian_trusted_root_ca.cer` и `russian_trusted_sub_ca.cer` с [gosuslugi.ru/crt](https://www.gosuslugi.ru/crt)
2. Откройте PowerShell **от имени администратора** и выполните:

```powershell
# Корневой сертификат
Import-Certificate -FilePath .\russian_trusted_root_ca.cer -CertStoreLocation Cert:\LocalMachine\Root

# Промежуточный сертификат
Import-Certificate -FilePath .\russian_trusted_sub_ca.cer -CertStoreLocation Cert:\LocalMachine\CA
```

</details>

<details>
<summary>Быстрая установка (Linux / Docker)</summary>

```bash
# Скачайте сертификаты
curl -o russian_trusted_root_ca.cer https://gu-st.ru/content/lending/russian_trusted_root_ca_2022.cer
curl -o russian_trusted_sub_ca.cer  https://gu-st.ru/content/lending/russian_trusted_sub_ca_2022.cer

# Конвертируйте в PEM и установите
openssl x509 -inform DER -in russian_trusted_root_ca.cer -out /usr/local/share/ca-certificates/russian_trusted_root_ca.crt
openssl x509 -inform DER -in russian_trusted_sub_ca.cer  -out /usr/local/share/ca-certificates/russian_trusted_sub_ca.crt
update-ca-certificates
```

Для **Docker** добавьте эти команды в `Dockerfile` до запуска приложения или используйте volume с обновлённым хранилищем сертификатов.

</details>

### Запуск

```bash
dotnet run --project HandbookBot
```

После запуска:
- Бот принимает сообщения через Long Polling
- Healthcheck доступен на `http://localhost:PORT/health`

---


### Переменные среды

Все ключи конфигурации доступны через переменные среды (с разделителем `__`):

```bash
Telegram__Token=xxx
Max__Enabled=true
Max__Token=xxx
Data__UseDatabase=true
Data__ConnectionString="Host=..."
```

> **Важно**: никогда не записывайте токены в `appsettings.json`. Используйте User Secrets (разработка) или переменные среды / secrets-менеджер (продакшен).

---

## Запуск через Docker

```bash
# Сборка образа (из папки HandbookBot2)
docker build -t handbookbot .

# Запуск
docker run -d \
  -e Telegram__Token="ВАШ_ТОКЕН" \
  -e Max__Enabled=false \
  -p 8080:8080 \
  --name handbookbot \
  handbookbot
```

Healthcheck встроен в образ:
```
GET http://localhost:8080/health
→ {"status":"healthy","utc":"..."}
```

---

## Слой данных

По умолчанию (`Data:UseDatabase = false`) используются **InMemory-заглушки** с тестовыми данными — удобно для разработки и демо без БД.

При `Data:UseDatabase = true` подключается **EF Core** с провайдером **PostgreSQL**. 

Бот подключается к уже готовой существующей БД, поэтому выполнять EF Core миграции не требуется. Укажите правильную строку подключения в `Data:ConnectionString` для работы:

```json
"Data": {
  "UseDatabase": true,
  "ConnectionString": "Host=localhost;Database=handbookbot;Username=postgres;Password=your_password"
}
```

---

## Команды бота

| Команда | Ключ DI | Описание |
|---------|---------|----------|
| `/start` | `start` | Главное меню |
| Список препаратов | `preparations` | Постраничный список с ценами |
| Поиск препарата | `prepsearch` | Диалоговый поиск (2 шага) |
| Аптечные пункты | `pharmacies` | Список аптек |
| Карта аптеки | `pharmmap` | Геолокация по Id аптеки |
| FAQ | `faq` | Частые вопросы и ответы |
| Инструкция | `instruction` | Инструкция по работе с ботом |
| Контакты | `contacts` | Контакты ГАУЗ ОАС |

Маршрутизация работает в порядке приоритета:
1. **CallbackData** — нажатие inline-кнопки (`commandName:payload`)
2. **Активная сессия** — ожидание ввода текста (например, поиск)
3. **Текст команды** — `/start`, `start` и т.п.

---

## Добавление новой платформы

1. Создать проект `HandbookBot.MyPlatform`
2. Реализовать `IMessagingPlatform`:
   ```csharp
   public class MyPlatformAdapter : IMessagingPlatform
   {
       public event Func<IncomingMessage, Task>? OnMessageReceived;
       public Task SendTextAsync(string chatId, string text, BotKeyboard? keyboard, CancellationToken ct) { ... }
       public Task SendLocationAsync(string chatId, double lat, double lon, CancellationToken ct) { ... }
   }
   ```
3. Создать `BackgroundService` для polling/webhook
4. Добавить extension-метод `AddMyPlatform(IServiceCollection, IConfiguration)`
5. Подключить в `Program.cs` за конфигурационным флагом

---

## Технологии

| | |
|---|---|
| Runtime | .NET 10 |
| Web framework | ASP.NET Core (Minimal API) |
| Telegram SDK | [Telegram.Bot 22.x](https://github.com/TelegramBots/Telegram.Bot) |
| MAX SDK | SaaSoft.MAX.Bot (локальный проект) |
| ORM | Entity Framework Core 10 |
| Контейнеризация | Docker (Alpine Linux) |
| Логирование | Microsoft.Extensions.Logging |

---

## Лицензия

MIT
