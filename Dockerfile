FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /build
ARG CACHEBUST=20261108
RUN git clone --depth=1 https://github.com/Ekwav/websocket-sharp \
    && git init dev && cd dev && git remote add origin https://github.com/Coflnet/HypixelSkyblock.git \
    && git fetch --depth=1 origin df02c6a675c68a54be6a7ad58a5fcd379db97e42 \
    && git checkout --detach FETCH_HEAD && cd .. \
    && git clone --depth=1 https://github.com/Coflnet/SkyFilter.git \
    && git clone --depth=1 https://github.com/Coflnet/SkyBackendForFrontend.git
RUN mkdir -p /build/skyblock/External/api
WORKDIR /build/SkyCommand
COPY SkyCommands.csproj SkyCommands.csproj
RUN dotnet restore
COPY . .
RUN dotnet test
RUN dotnet publish -c release -o /app /p:UseAppHost=false /p:PublishReadyToRun=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble
# apt in the base image has CVE-2026-109518 (HIGH); upgrade it until the base image ships the fix.
RUN apt-get update && apt-get install -y --only-upgrade libssl3t64 openssl apt libapt-pkg6.0t64 && rm -rf /var/lib/apt/lists/*
WORKDIR /app

COPY --from=build --chown=$APP_UID:$APP_UID /app .

ENV ASPNETCORE_URLS=http://+:8000 \
    DOTNET_EnableDiagnostics=0 \
    COMPlus_EnableDiagnostics=0 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    HOME=/tmp \
    TMPDIR=/tmp

USER $APP_UID

ENTRYPOINT ["dotnet", "SkyCommands.dll", "--hostBuilder:reloadConfigOnChange=false"]

