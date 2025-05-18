# Utiliser l'image SDK .NET pour la compilation
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

# Copier les fichiers projet et restaurer les dépendances
COPY ["ItunesRPC/ItunesRPC.csproj", "ItunesRPC/"]
RUN dotnet restore "ItunesRPC/ItunesRPC.csproj"

# Copier le reste du code et compiler l'application
COPY . .
WORKDIR "/src/ItunesRPC"
RUN dotnet build "ItunesRPC.csproj" -c Release -o /app/build

# Publier l'application
FROM build AS publish
RUN dotnet publish "ItunesRPC.csproj" -c Release -o /app/publish

# Créer l'image finale
FROM mcr.microsoft.com/dotnet/runtime:6.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Note: Cette application nécessite une interface graphique Windows pour fonctionner correctement
# Ce Dockerfile est principalement utilisé pour la distribution et le packaging

ENTRYPOINT ["dotnet", "ItunesRPC.dll"]