namespace SombraStudios.Shared.Networking.Sessions
{
    /// <summary>
    /// Who is allowed to find and enter a lobby.
    /// </summary>
    /// <remarks>
    /// Mirrors Steam's lobby types without depending on the Steamworks assembly, so the
    /// session layer stays compilable in a project that has no Steam packages installed.
    /// See https://partner.steamgames.com/doc/api/ISteamMatchmaking#ELobbyType
    /// </remarks>
    public enum LobbyVisibility
    {
        /// <summary>Only invited users may join, and the lobby is never listed.</summary>
        Private,

        /// <summary>Friends of the owner may join without an invite. Not listed in searches.</summary>
        FriendsOnly,

        /// <summary>Listed in lobby searches and joinable by anyone.</summary>
        Public,

        /// <summary>Joinable only through an invite, and invisible to friends.</summary>
        InviteOnly,
    }
}
