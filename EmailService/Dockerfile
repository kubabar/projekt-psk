FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file
COPY ["EmailService/EmailService.csproj", "EmailService/"]

# Restore dependencies
RUN dotnet restore "EmailService/EmailService.csproj"

# Copy all source files
COPY EmailService/ EmailService/

# Build
WORKDIR "/src/EmailService"
RUN dotnet build "EmailService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "EmailService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create a script to replace environment variables in appsettings.json
RUN echo '#!/bin/bash\n\
sed -i "s/\${RABBITMQ_USER}/$RABBITMQ_USER/g" /app/appsettings.json\n\
sed -i "s/\${RABBITMQ_PASSWORD}/$RABBITMQ_PASSWORD/g" /app/appsettings.json\n\
sed -i "s/\${SMTP_HOST}/$SMTP_HOST/g" /app/appsettings.json\n\
sed -i "s/\${SMTP_PORT}/$SMTP_PORT/g" /app/appsettings.json\n\
sed -i "s/\${SMTP_USERNAME}/$SMTP_USERNAME/g" /app/appsettings.json\n\
sed -i "s/\${SMTP_PASSWORD}/$SMTP_PASSWORD/g" /app/appsettings.json\n\
sed -i "s/\${SMTP_FROM_EMAIL}/$SMTP_FROM_EMAIL/g" /app/appsettings.json\n\
exec dotnet EmailService.dll' > /app/entrypoint.sh && chmod +x /app/entrypoint.sh

ENTRYPOINT ["/app/entrypoint.sh"]
