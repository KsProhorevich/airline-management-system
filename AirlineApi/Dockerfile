# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY AirlineApi/AirlineApi.csproj AirlineApi/
RUN dotnet restore AirlineApi/AirlineApi.csproj
COPY AirlineApi/ AirlineApi/
RUN dotnet publish AirlineApi/AirlineApi.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
COPY AirlineApi/docker-entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh && mkdir -p /data
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
VOLUME ["/data"]
USER root
ENTRYPOINT ["/entrypoint.sh"]
