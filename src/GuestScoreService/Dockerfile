FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Shared/Shared.csproj", "Shared/"]
COPY ["GuestScoreService/GuestScoreService.csproj", "GuestScoreService/"]
RUN dotnet restore "GuestScoreService/GuestScoreService.csproj"
COPY . .
RUN dotnet build "GuestScoreService/GuestScoreService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "GuestScoreService/GuestScoreService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GuestScoreService.dll"]