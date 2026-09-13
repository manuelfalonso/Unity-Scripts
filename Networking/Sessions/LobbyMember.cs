namespace SombraStudios.Shared.Networking.Sessions
{
    /// <summary>
    /// One participant in a lobby, described without any Steamworks type.
    /// </summary>
    public readonly struct LobbyMember
    {
        /// <summary>The member's Steam ID.</summary>
        public ulong SteamId { get; }

        /// <summary>The member's persona name, or an empty string when Steam has not supplied it yet.</summary>
        public string DisplayName { get; }

        /// <summary>
        /// Creates a lobby member.
        /// </summary>
        public LobbyMember(ulong steamId, string displayName)
        {
            SteamId = steamId;
            DisplayName = displayName ?? string.Empty;
        }
    }
}
