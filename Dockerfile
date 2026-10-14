FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Копируем файлы проектов для кэширования слоев restore
COPY ["HandbookBot/HandbookBot.csproj", "HandbookBot/"]
COPY ["HandbookBot.Core/HandbookBot.Core.csproj", "HandbookBot.Core/"]
COPY ["HandbookBot.Data/HandbookBot.Data.csproj", "HandbookBot.Data/"]
COPY ["external/BotEngine/BotEngine.Core/BotEngine.Core.csproj", "external/BotEngine/BotEngine.Core/"]
COPY ["external/BotEngine/BotEngine.Telegram/BotEngine.Telegram.csproj", "external/BotEngine/BotEngine.Telegram/"]
COPY ["external/BotEngine/BotEngine.Max/BotEngine.Max.csproj", "external/BotEngine/BotEngine.Max/"]
COPY ["external/BotEngine/external/max-bot-dotnet/MAX.Bot/MAX.Bot.csproj", "external/BotEngine/external/max-bot-dotnet/MAX.Bot/"]
RUN dotnet restore "HandbookBot/HandbookBot.csproj"

# Копируем остальной код и собираем
COPY . .
WORKDIR "/src/HandbookBot"
RUN dotnet build "HandbookBot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "HandbookBot.csproj" -c Release -o /app/publish

# Финальный образ для запуска
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# 👇 Сертификаты Минцифры устанавливаем именно здесь — в runtime-образе
RUN apt-get update && \
    apt-get install -y --no-install-recommends ca-certificates curl && \
    curl -fsSL -o /usr/local/share/ca-certificates/russian_trusted_root_ca.crt \
      https://gu-st.ru/content/lending/russian_trusted_root_ca_pem.crt && \
    curl -fsSL -o /usr/local/share/ca-certificates/russian_trusted_sub_ca.crt \
      https://gu-st.ru/content/lending/russian_trusted_sub_ca_pem.crt && \
    update-ca-certificates && \
    apt-get purge -y curl && \
    apt-get autoremove -y && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app
EXPOSE 8080
COPY --from=publish /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "HandbookBot.dll"]