using System;
using ItunesRPC.Models;

namespace ItunesRPC.Services
{
    // Classes d'événements communes pour les services de détection de musique
    public class TrackInfoEventArgs : EventArgs
    {
        public TrackInfo? TrackInfo { get; }
        public string? Source { get; }

        public TrackInfoEventArgs(TrackInfo? trackInfo, string? source = null)
        {
            TrackInfo = trackInfo;
            Source = source;
        }
    }

    public class PlayStateEventArgs : EventArgs
    {
        public bool IsPlaying { get; }
        public string? Source { get; }

        public PlayStateEventArgs(bool isPlaying, string? source = null)
        {
            IsPlaying = isPlaying;
            Source = source;
        }
    }
}