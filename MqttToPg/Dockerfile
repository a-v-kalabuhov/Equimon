FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["MqttToPg.csproj", "."]
RUN dotnet restore
COPY . .
WORKDIR "/src"
RUN dotnet publish "MqttToPg.csproj" -c Release -o /app

# FROM mcr.microsoft.com/dotnet/runtime:8.0
# WORKDIR /app
# COPY --from=build /app .
# ENTRYPOINT ["dotnet", "MqttToPg.dll"]

FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine AS final
USER root
RUN apk add --no-cache \
    krb5-libs \
    postgresql-client  # psql для debug
USER 1000
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "MqttToPg.dll"]