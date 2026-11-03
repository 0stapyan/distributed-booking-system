FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["AdminGateway/AdminGateway.csproj", "AdminGateway/"]
COPY ["Shared/Shared.csproj", "Shared/"]
RUN dotnet restore "AdminGateway/AdminGateway.csproj"
COPY . .
RUN dotnet build "AdminGateway/AdminGateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AdminGateway/AdminGateway.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AdminGateway.dll"]