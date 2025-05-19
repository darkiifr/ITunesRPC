# Utiliser l'image SDK .NET pour Windows pour la compilation
FROM mcr.microsoft.com/dotnet/sdk:6.0-windowsdesktop AS build
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
RUN dotnet publish "ItunesRPC.csproj" -c Release -o /app/publish /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true

# Créer l'image finale avec support pour WPF
FROM mcr.microsoft.com/dotnet/desktop-runtime:6.0-windowsdesktop AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Exposer le port pour Discord RPC
EXPOSE 6463

# Note: Cette application nécessite:
# - Une interface graphique Windows
# - iTunes installé sur l'hôte
# - Discord installé sur l'hôte pour la fonctionnalité Rich Presence

ENTRYPOINT ["ItunesRPC.exe"]