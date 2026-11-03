FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Shared/Shared.csproj", "Shared/"]
COPY ["GuestService.Application/GuestService.Application.csproj", "GuestService.Application/"]
COPY ["GuestService/GuestService.csproj", "GuestService/"]
RUN dotnet restore "GuestService/GuestService.csproj"
COPY . .
RUN dotnet build "GuestService/GuestService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "GuestService/GuestService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GuestService.dll"]
