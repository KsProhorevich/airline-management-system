# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY AirlineClient/AirlineClient.csproj AirlineClient/
RUN dotnet restore AirlineClient/AirlineClient.csproj
COPY AirlineClient/ AirlineClient/
RUN dotnet publish AirlineClient/AirlineClient.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=5s --retries=5 CMD curl -f http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "AirlineClient.dll"]
