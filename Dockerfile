FROM mcr.microsoft.com/dotnet/sdk:5.0 as build
WORKDIR /build
RUN echo "new rev"
RUN git clone --depth=1 https://github.com/Ekwav/websocket-sharp
RUN git clone --depth=1 -b separation https://github.com/Coflnet/HypixelSkyblock.git dev
RUN git clone --depth=1 https://github.com/Coflnet/SkyFilter.git
RUN mkdir -p /build/skyblock/External/api
WORKDIR /build/SkyCommand
COPY SkyCommands.csproj SkyCommands.csproj
RUN dotnet restore
COPY . .
RUN touch /build/dev/keyfile.p12 
RUN cp -n /build/dev/appsettings.json /build/dev/custom.conf.json
RUN dotnet publish -c release

FROM mcr.microsoft.com/dotnet/aspnet:5.0
WORKDIR /app

COPY --from=build /build/SkyCommand/bin/release/net5.0/publish/ .
RUN mkdir -p ah/files

ENTRYPOINT ["dotnet", "SkyCommands.dll"]

VOLUME /data

