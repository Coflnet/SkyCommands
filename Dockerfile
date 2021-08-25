FROM mcr.microsoft.com/dotnet/core/sdk:3.1 as build
WORKDIR /build
RUN git clone --depth=1 https://github.com/Ekwav/websocket-sharp
RUN git clone --depth=1 -b separation https://github.com/Coflnet/HypixelSkyblock.git dev
RUN mkdir -p /build/skyblock/External/api
WORKDIR /build/SkyCommand
COPY SkyCommands.csproj SkyCommands.csproj
RUN dotnet restore
COPY . .
RUN touch /build/dev/keyfile.p12 
RUN cp -n /build/dev/appsettings.json /build/dev/custom.conf.json
RUN dotnet publish -c release

FROM mcr.microsoft.com/dotnet/aspnet:3.1
WORKDIR /app

COPY --from=build /build/SkyCommand/bin/release/netcoreapp3.1/publish/ .
RUN mkdir -p ah/files
#COPY --from=frontend /build/build/ /data/files

ENTRYPOINT ["dotnet", "SkyCommands.dll"]

VOLUME /data

