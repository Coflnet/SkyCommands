FROM mcr.microsoft.com/dotnet/sdk:10.0 as build
WORKDIR /build
RUN git clone --depth=1 https://github.com/Ekwav/websocket-sharp
RUN git clone --depth=1 https://github.com/Coflnet/HypixelSkyblock.git dev
RUN git clone --depth=1 https://github.com/Coflnet/SkyFilter.git
RUN git clone --depth=1 https://github.com/Coflnet/SkyBackendForFrontend.git
RUN mkdir -p /build/skyblock/External/api
WORKDIR /build/SkyCommand
COPY SkyCommands.csproj SkyCommands.csproj
RUN dotnet restore
COPY . .
RUN dotnet test
RUN touch /build/dev/keyfile.p12 
RUN dotnet publish -c release -o /app

FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /app

COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8000

RUN useradd --uid $(shuf -i 2000-65000 -n 1) app-user
USER app-user

ENTRYPOINT ["dotnet", "SkyCommands.dll", "--hostBuilder:reloadConfigOnChange=false"]

