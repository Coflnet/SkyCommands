FROM mcr.microsoft.com/dotnet/sdk:6.0 as build
WORKDIR /build
RUN git clone --depth=1 https://github.com/Ekwav/websocket-sharp
RUN git clone --depth=1 -b net6 https://github.com/Coflnet/HypixelSkyblock.git dev
RUN git clone --depth=1 https://github.com/Coflnet/SkyFilter.git
RUN mkdir -p /build/skyblock/External/api
WORKDIR /build/SkyCommand
COPY SkyCommands.csproj SkyCommands.csproj
RUN dotnet restore
COPY . .
RUN dotnet test
RUN touch /build/dev/keyfile.p12 
RUN dotnet publish -c release

FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /app

COPY --from=build /build/SkyCommand/bin/release/net6.0/publish/ .
RUN mkdir -p ah/files
ENV ASPNETCORE_URLS=http://+:8000;http://+:80

ENTRYPOINT ["dotnet", "SkyCommands.dll", "--hostBuilder:reloadConfigOnChange=false"]

VOLUME /data

