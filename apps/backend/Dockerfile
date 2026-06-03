FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY App/App.csproj App/
RUN dotnet restore App/App.csproj
COPY App/ App/
RUN dotnet publish App/App.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# Npgsql carrega libgssapi_krb5.so.2 ao negociar criptografia GSS na conexão.
# A imagem aspnet (Debian) não a inclui — sem ela o processo sofre segfault
# (exit 139) ao abrir a conexão com o Postgres.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "App.dll"]
