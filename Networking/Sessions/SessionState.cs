namespace SombraStudios.Shared.Networking.Sessions
{
    /// <summary>
    /// The lifecycle of a multiplayer session, from offline to connected.
    /// </summary>
    /// <remarks>
    /// Hosting and connecting are two different terminal states on purpose: a host owns
    /// the lobby and runs the server, a client owns neither.
    /// </remarks>
    public enum SessionState
    {
        /// <summary>No lobby and no network session.</summary>
        Offline,

        /// <summary>A lobby has been requested but is not confirmed yet.</summary>
        Creating,

        /// <summary>Lobby created and the server is running.</summary>
        Hosting,

        /// <summary>A lobby has been entered but the client is not connected yet.</summary>
        Joining,

        /// <summary>Connected to a remote host.</summary>
        Connected,
    }
}
