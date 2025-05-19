using ItunesRPC.Models;
using System;
using System.IO;
using System.Drawing;

namespace ItunesRPC.Services
{
    /// <summary>
    /// Classe utilitaire pour améliorer l'affichage des informations de piste
    /// </summary>
    public static class TrackDisplayHelper
    {
        /// <summary>
        /// Formate le temps de lecture pour l'affichage
        /// </summary>
        /// <param name="timeSpan">Durée à formater</param>
        /// <returns>Chaîne formatée (ex: "3:45")</returns>
        public static string FormatTime(TimeSpan timeSpan)
        {
            return timeSpan.Hours > 0 
                ? $"{timeSpan.Hours}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}" 
                : $"{timeSpan.Minutes}:{timeSpan.Seconds:D2}";
        }
        
        /// <summary>
        /// Formate les informations de position de piste
        /// </summary>
        /// <param name="trackNumber">Numéro de piste</param>
        /// <param name="trackCount">Nombre total de pistes</param>
        /// <returns>Chaîne formatée (ex: "3 sur 12")</returns>
        public static string FormatTrackPosition(int trackNumber, int trackCount)
        {
            if (trackNumber <= 0 || trackCount <= 0)
                return "-";
                
            return $"{trackNumber} sur {trackCount}";
        }
        
        /// <summary>
        /// Formate les informations de progression de lecture
        /// </summary>
        /// <param name="trackInfo">Informations sur la piste</param>
        /// <returns>Chaîne formatée (ex: "1:23 / 3:45")</returns>
        public static string FormatProgress(TrackInfo trackInfo)
        {
            if (!trackInfo.IsPlaying || trackInfo.Duration.TotalSeconds <= 0)
                return "-";
                
            var elapsed = DateTime.Now - trackInfo.StartTime;
            if (elapsed > trackInfo.Duration)
                elapsed = trackInfo.Duration;
                
            return $"{FormatTime(elapsed)} / {FormatTime(trackInfo.Duration)}";
        }
        
        /// <summary>
        /// Tronque une chaîne si elle dépasse une longueur maximale
        /// </summary>
        /// <param name="text">Texte à tronquer</param>
        /// <param name="maxLength">Longueur maximale</param>
        /// <returns>Chaîne tronquée avec ellipsis si nécessaire</returns>
        public static string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;
                
            return text.Substring(0, maxLength - 3) + "...";
        }
        
        /// <summary>
        /// Formate les informations de genre musical
        /// </summary>
        /// <param name="genre">Genre musical</param>
        /// <returns>Chaîne formatée ou valeur par défaut si vide</returns>
        public static string FormatGenre(string genre)
        {
            return string.IsNullOrEmpty(genre) ? "Non spécifié" : genre;
        }
        
        /// <summary>
        /// Formate l'année de sortie
        /// </summary>
        /// <param name="year">Année de sortie</param>
        /// <returns>Chaîne formatée ou valeur par défaut si non valide</returns>
        public static string FormatYear(int year)
        {
            return year <= 0 ? "Non spécifiée" : year.ToString();
        }
        
        /// <summary>
        /// Génère une description riche pour l'affichage dans Discord
        /// </summary>
        /// <param name="trackInfo">Informations sur la piste</param>
        /// <param name="maxLength">Longueur maximale de la description</param>
        /// <returns>Description formatée pour Discord</returns>
        public static string GenerateRichDescription(TrackInfo trackInfo, int maxLength = 128)
        {
            if (trackInfo == null)
                return "Aucune piste en lecture";
                
            var description = $"{trackInfo.Artist} • {trackInfo.Album}";
            
            // Ajouter l'année si disponible
            if (trackInfo.Year > 0)
                description += $" ({trackInfo.Year})";
                
            // Ajouter le genre si disponible
            if (!string.IsNullOrEmpty(trackInfo.Genre))
                description += $" • {trackInfo.Genre}";
                
            return TruncateText(description, maxLength);
        }
        
        /// <summary>
        /// Calcule une couleur dominante à partir du chemin de la pochette d'album
        /// </summary>
        /// <param name="artworkPath">Chemin vers la pochette d'album</param>
        /// <returns>Couleur dominante ou couleur par défaut si non disponible</returns>
        public static System.Drawing.Color GetDominantColor(string artworkPath)
        {
            // Couleur par défaut si pas de pochette
            if (string.IsNullOrEmpty(artworkPath) || !File.Exists(artworkPath))
                return System.Drawing.Color.FromArgb(88, 101, 242); // Couleur Discord par défaut
                
            try
            {
                using var bitmap = new System.Drawing.Bitmap(artworkPath);
                int r = 0, g = 0, b = 0;
                int total = 0;
                
                // Échantillonner l'image pour trouver une couleur moyenne
                // Pour des performances optimales, on ne traite qu'un pixel sur 10
                for (int x = 0; x < bitmap.Width; x += 10)
                {
                    for (int y = 0; y < bitmap.Height; y += 10)
                    {
                        var clr = bitmap.GetPixel(x, y);
                        r += clr.R;
                        g += clr.G;
                        b += clr.B;
                        total++;
                    }
                }
                
                if (total > 0)
                {
                    r /= total;
                    g /= total;
                    b /= total;
                    return System.Drawing.Color.FromArgb(r, g, b);
                }
                
                return System.Drawing.Color.FromArgb(88, 101, 242); // Couleur Discord par défaut
            }
            catch
            {
                return System.Drawing.Color.FromArgb(88, 101, 242); // Couleur Discord par défaut en cas d'erreur
            }
        }
    }
}