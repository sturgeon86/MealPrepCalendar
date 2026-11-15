# syntax=docker/dockerfile:1

# Étape 1 : compilation et publication
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Les .csproj d'abord : la restauration NuGet reste en cache tant qu'ils ne changent pas.
COPY global.json ./
COPY src/Repas.Domaine/Repas.Domaine.csproj src/Repas.Domaine/
COPY src/Repas.Donnees/Repas.Donnees.csproj src/Repas.Donnees/
COPY src/Repas.Web/Repas.Web.csproj src/Repas.Web/
RUN dotnet restore src/Repas.Web/Repas.Web.csproj

COPY src/ src/
COPY db/ db/
RUN dotnet publish src/Repas.Web/Repas.Web.csproj -c Release -o /app --no-restore -p:UseAppHost=false

# Étape 2 : image d'exécution
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Les clés de protection des données et les photos vivent sur des volumes ;
# leurs dossiers doivent appartenir à l'utilisateur de l'app.
RUN mkdir -p /data/keys /data/photos && chown app:app /data/keys /data/photos

COPY --from=build /app .

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DataProtection__KeysPath=/data/keys \
    Photos__Chemin=/data/photos

USER app
EXPOSE 8080
VOLUME ["/data/keys", "/data/photos"]

ENTRYPOINT ["dotnet", "Repas.Web.dll"]
