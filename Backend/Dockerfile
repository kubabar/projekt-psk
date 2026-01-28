FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["Backend/Backend.csproj", "Backend/"]
COPY ["NBPExchangeRatesLib/NBPExchangeRatesLib.csproj", "NBPExchangeRatesLib/"]
COPY ["Gus.Regon.BIR11.Proxy/Gus.Regon.BIR11.Proxy.csproj", "Gus.Regon.BIR11.Proxy/"]

# Restore dependencies
RUN dotnet restore "Backend/Backend.csproj"

# Copy all source files
COPY Backend/ Backend/
COPY NBPExchangeRatesLib/ NBPExchangeRatesLib/
COPY Gus.Regon.BIR11.Proxy/ Gus.Regon.BIR11.Proxy/

# Build
WORKDIR "/src/Backend"
RUN dotnet build "Backend.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Backend.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create a script to replace environment variables in appsettings.json
RUN echo '#!/bin/bash\n\
sed -i "s/\${DB_PASSWORD}/$DB_PASSWORD/g" /app/appsettings.json\n\
sed -i "s/\${JWT_SECRET}/$JWT_SECRET/g" /app/appsettings.json\n\
sed -i "s/\${RABBITMQ_USER}/$RABBITMQ_USER/g" /app/appsettings.json\n\
sed -i "s/\${RABBITMQ_PASSWORD}/$RABBITMQ_PASSWORD/g" /app/appsettings.json\n\
sed -i "s/\${BIR_API_KEY}/$BIR_API_KEY/g" /app/appsettings.json\n\
sed -i "s|\${BIR_API_ENDPOINT}|$BIR_API_ENDPOINT|g" /app/appsettings.json\n\
exec dotnet Backend.dll' > /app/entrypoint.sh && chmod +x /app/entrypoint.sh

ENTRYPOINT ["/app/entrypoint.sh"]
