FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY TranslatorApi.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish TranslatorApi.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

RUN adduser --disabled-password --gecos "" appuser \
    && mkdir -p /app/json-history \
    && chown -R appuser:appuser /app

USER appuser

COPY --from=build --chown=appuser:appuser /app/publish ./

VOLUME ["/app/json-history"]

ENV ASPNETCORE_URLS=http://+:5100
EXPOSE 5100

ENTRYPOINT ["dotnet", "TranslatorApi.dll"]