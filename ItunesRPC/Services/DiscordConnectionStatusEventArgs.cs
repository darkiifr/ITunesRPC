using System;

namespace ItunesRPC.Services
{
    public class DiscordConnectionStatusEventArgs : EventArgs
    {
        public bool IsConnected { get; }
        public string? Message { get; }

        public DiscordConnectionStatusEventArgs(bool isConnected, string? message = null)
        {
            IsConnected = isConnected;
            Message = message;
        }
    }
}