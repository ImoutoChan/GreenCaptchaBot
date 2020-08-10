FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-buster-slim AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["CaptchaBot/CaptchaBot.csproj", "CaptchaBot/"]
RUN dotnet restore "CaptchaBot/CaptchaBot.csproj"
COPY . .
WORKDIR "/src/CaptchaBot"
RUN dotnet build "CaptchaBot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CaptchaBot.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CaptchaBot.dll"]