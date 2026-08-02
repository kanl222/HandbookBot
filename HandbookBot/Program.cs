using HandbookBot;
using HandbookBot.Core.Commands;
using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Services;
using HandbookBot.Data;
using HandbookBot.Max;
using HandbookBot.Telegram;

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

// -------------------- 2. Ядро и Сервисы (Core Layer) --------------------
// Хранилище сессий пользователей (InMemory или Redis в будущем)
builder.Services.AddSingleton<IUserSessionStore, InMemoryUserSessionStore>();

// Диспетчер команд и фабрика команд регистрируются как Scoped,
// чтобы корректно взаимодействовать с Scoped DbContext на каждый запрос
builder.Services.AddScoped<CommandDispatcher>();
builder.Services.AddScoped<ICommandFactory, CommandFactory>();

// -------------------- 3. Команды Бота (Bot Commands) --------------------
// Регистрирует все IBotCommand в DI через Keyed Service по имени команды
builder.Services.AddBotCommands();

// -------------------- 4. Адаптер Telegram (Telegram Adapter) ──────
var telegramEnabled = builder.Configuration.GetValue<bool>("Telegram:Enabled");
if (telegramEnabled)
{
    var telegramToken = builder.Configuration["Telegram:Token"];
    builder.Services.AddTelegramPlatform(telegramToken);
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
