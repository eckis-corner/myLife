# syntax=docker/dockerfile:1
# myLife – Image für linux/amd64 (Synology DS1520+). Build: docker build -t mylife .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props .editorconfig MyLife.slnx ./
COPY src/MyLife.Shared/MyLife.Shared.csproj src/MyLife.Shared/
COPY src/MyLife.Crypto/MyLife.Crypto.csproj src/MyLife.Crypto/
COPY src/MyLife.Client/MyLife.Client.csproj src/MyLife.Client/
COPY src/MyLife.Server/MyLife.Server.csproj src/MyLife.Server/
RUN dotnet restore src/MyLife.Server/MyLife.Server.csproj

COPY src/ src/
ARG VERSION=0.1.0
RUN dotnet publish src/MyLife.Server/MyLife.Server.csproj \
        -c Release -o /app --no-restore \
        -p:Version=${VERSION} -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    MyLife__DataDirectory=/data \
    DOTNET_gcServer=0

RUN mkdir -p /data && chown -R $APP_UID:$APP_UID /data
COPY --from=build /app .

USER $APP_UID
EXPOSE 8080
VOLUME ["/data"]

ENTRYPOINT ["dotnet", "MyLife.Server.dll"]
