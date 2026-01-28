FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["WebSocketService/WebSocketService.csproj", "WebSocketService/"]
RUN dotnet restore "WebSocketService/WebSocketService.csproj"

COPY WebSocketService/ WebSocketService/

WORKDIR "/src/WebSocketService"
RUN dotnet build "WebSocketService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WebSocketService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

RUN echo '#!/bin/bash\n\
sed -i "s/\${RABBITMQ_USER}/$RABBITMQ_USER/g" /app/appsettings.json\n\
sed -i "s/\${RABBITMQ_PASSWORD}/$RABBITMQ_PASSWORD/g" /app/appsettings.json\n\
exec dotnet WebSocketService.dll' > /app/entrypoint.sh && chmod +x /app/entrypoint.sh

ENTRYPOINT ["/app/entrypoint.sh"]
