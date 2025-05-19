using System;

namespace ItunesRPC.Models
{
    public class TrackInfo
    {
        private string _artworkPath = string.Empty;
        
        public string Name { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public string Genre { get; set; } = string.Empty;
        public int Year { get; set; }
        public int TrackNumber { get; set; }
        public int TrackCount { get; set; }
        public bool IsPlaying { get; set; }
        
        public string ArtworkPath 
        { 
            get => _artworkPath; 
            set => _artworkPath = !string.IsNullOrEmpty(value) ? value : string.Empty; 
        }
        
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        
        // Propriété calculée pour la durée totale de la piste
        public TimeSpan Duration => EndTime - StartTime;
        
        // Propriété calculée pour la progression actuelle
        public double ProgressPercentage
        {
            get
            {
                if (!IsPlaying) return 0;
                
                var totalSeconds = Duration.TotalSeconds;
                if (totalSeconds <= 0) return 0;
                
                var elapsed = (DateTime.Now - StartTime).TotalSeconds;
                return Math.Min(100, Math.Max(0, (elapsed / totalSeconds) * 100));
            }
        }
    }
}