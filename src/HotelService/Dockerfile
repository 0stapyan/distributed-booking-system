FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Shared/Shared.csproj", "Shared/"]
COPY ["HotelService.Application/HotelService.Application.csproj", "HotelService.Application/"]
COPY ["HotelService/HotelService.csproj", "HotelService/"]
RUN dotnet restore "HotelService/HotelService.csproj"
COPY . .
RUN dotnet build "HotelService/HotelService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "HotelService/HotelService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HotelService.dll"]
