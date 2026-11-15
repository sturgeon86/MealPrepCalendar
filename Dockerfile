# Étape 1 : compilation et publication
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Les .csproj d'abord : la restauration NuGet reste en cache tant qu'ils ne changent pas.
COPY src/Repas.Domaine/Repas.Domaine.csproj src/Repas.Domaine/
COPY src/Repas.Donnees/Repas.Donnees.csproj src/Repas.Donnees/
COPY src/Repas.Web/Repas.Web.csproj src/Repas.Web/
RUN dotnet restore src/Repas.Web/Repas.Web.csproj

COPY src/ src/
COPY db/ db/
RUN dotnet publish src/Repas.Web/Repas.Web.csproj -c Release -o /app/publish --no-restore

# Étape 2 : image d'exécution
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Repas.Web.dll"]
