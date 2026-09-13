using System;

namespace SombraStudios.Shared.Networking.Sessions
{
    /// <summary>
    /// The transport half of a session: starting the server or opening the connection to it.
    /// </summary>
    /// <remarks>
    /// Kept separate from <see cref="ILobbyBackend"/> because the two are genuinely independent —
    /// a Steam lobby can front a local connection while iterating solo, and the netcode layer
    /// never has to know which.
    /// </remarks>
    public interface INetworkSessionDriver
    {
        /// <summary>Whether a server or client is currently running.</summary>
        bool IsRunning { get; }

        /// <summary>Raised once the local peer is connected and able to send traffic.</summary>
        event Action Connected;

        /// <summary>Raised when the local peer loses or closes its connection.</summary>
        event Action Disconnected;

        /// <summary>
        /// Starts a server that also plays locally.
        /// </summary>
        /// <returns>False when the netcode layer refused to start.</returns>
        bool StartHost();

        /// <summary>
        /// Connects to a host.
        /// </summary>
        /// <param name="hostId">
        /// The host's Steam ID, read from the lobby metadata. Ignored by drivers that connect
        /// over a local address instead.
        /// </param>
        /// <returns>False when the netcode layer refused to start.</returns>
        bool StartClient(ulong hostId);

        /// <summary>Stops whatever is running. Safe to call when nothing is.</summary>
        void Shutdown();
    }
}
