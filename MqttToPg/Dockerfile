FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["MqttToPg.csproj", "."]
RUN dotnet restore
COPY . .
WORKDIR "/src"
RUN dotnet publish "MqttToPg.csproj" -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine AS final
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "MqttToPg.dll"]