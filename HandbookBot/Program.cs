using BotEngine.Core;
using BotEngine.Core.Interfaces;
using BotEngine.Max;
using BotEngine.Telegram;
using HandbookBot;
using HandbookBot.Data;
using HandbookBot.Data.Caching;

// Загружаем переменные из .env файла в переменные окружения процесса.
// Это нужно сделать до создания builder.
DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Настройка логирования
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// -------------------- 1. Слой данных (Data Layer) --------------------
builder.Services.AddMemoryCache();
// Регистрирует DbContext (EF Core) и репозитории справочников
builder.Services.AddHandbookData(builder.Configuration);

// -------------------- 2. Ядро и Сервисы BotEngine --------------------
builder.Services.AddBotEngine();
builder.Services.AddRateLimiting();

// Хранилище сессий через распределенный кэш (IDistributedCache)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSingleton<IUserSessionStore, DistributedCacheUserSessionStore>();

// -------------------- 3. Команды Бота (Bot Commands) --------------------
// Регистрирует все IBotCommand в DI через Keyed Service по имени команды
builder.Services.AddBotCommands();

// -------------------- 4. Адаптер Telegram (Telegram Adapter) ──────
var telegramEnabled = builder.Configuration.GetValue<bool>("Telegram:Enabled");
if (telegramEnabled)
{
    var telegramToken = builder.Configuration["Telegram:Token"];
    builder.Services.AddTelegramPlatform(telegramToken!);
}

// -------------------- 5. Адаптер MAX (MAX Adapter) --------------------─
var maxEnabled = builder.Configuration.GetValue<bool>("Max:Enabled");
if (maxEnabled)
{
    builder.Services.AddMaxPlatform(builder.Configuration);
}

// -------------------- 6. Сборка и Запуск Веб-Приложения --------------------
var app = builder.Build();

// Эндпоинт проверки работоспособности (Healthcheck)
app.MapGet("/health", () => Results.Ok(new { status = "healthy", utc = DateTime.UtcNow }));

app.Run();
