FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine-extra AS base
RUN mkdir -p /data && chown -R $APP_UID /data && chmod -R 755 /data
RUN apk add --no-cache curl tzdata krb5-libs
USER $APP_UID
WORKDIR /app
ENV DOTNET_RUNNING_IN_CONTAINER_FAST_MODE=true
ENV ASPNETCORE_HTTP_PORTS=5248
EXPOSE 5248

FROM mcr.microsoft.com/dotnet/sdk:10.0.300-alpine3.23 AS build
RUN apk add --no-cache nodejs npm
ARG BUILD_CONFIGURATION=Release
ARG BUILD_VERSION=v1.0.0
WORKDIR /src
COPY --parents src/**/*.csproj ./
COPY ./Directory.Build.props ./
COPY ./Directory.Packages.props ./
RUN dotnet restore "src/Balance.Web/Balance.Web.csproj"
COPY . .
WORKDIR "/src/src/Balance.Web"
RUN dotnet build "Balance.Web.csproj" \
    --no-restore \
    -c $BUILD_CONFIGURATION \
    -p:UseAppHost=false \
    -p:Version=${BUILD_VERSION#v}

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Balance.Web.csproj" \
    --no-build \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    -p:UseAppHost=false \
    -p:Version=${BUILD_VERSION#v}

FROM base AS final
ENV DOTNET_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER_FAST_MODE=false
WORKDIR /app
COPY --from=publish /app/publish .
HEALTHCHECK CMD curl --fail http://localhost:5248/healthz || exit 1
ENTRYPOINT ["dotnet", "Balance.Web.dll"]
