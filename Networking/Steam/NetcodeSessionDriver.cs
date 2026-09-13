using System;
using Netcode.Transports.Facepunch;
using SombraStudios.Shared.Networking.Sessions;
using Unity.Netcode;

namespace SombraStudios.Shared.Networking.Steam
{
    /// <summary>
    /// An <see cref="INetworkSessionDriver"/> over Netcode for GameObjects.
    /// </summary>
    /// <remarks>
    /// Transport-agnostic on purpose. When the assigned transport is a <c>FacepunchTransport</c>
    /// it fills in the host Steam ID before connecting; with any other transport (a local
    /// <c>UnityTransport</c>, say) the ID is ignored and the transport's own address is used.
    /// That is what lets the same session code iterate locally and then run over Steam unchanged.
    /// </remarks>
    public sealed class NetcodeSessionDriver : INetworkSessionDriver, IDisposable
    {
        private readonly NetworkManager _networkManager;

        private bool _isDisposed;

        /// <summary>
        /// Wraps a <see cref="NetworkManager"/>.
        /// </summary>
        /// <param name="networkManager">The manager to drive. Must not be null.</param>
        public NetcodeSessionDriver(NetworkManager networkManager)
        {
            _networkManager = networkManager != null
                ? networkManager
                : throw new ArgumentNullException(nameof(networkManager));

            _networkManager.OnClientConnectedCallback += HandleClientConnected;
            _networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        }

        /// <inheritdoc />
        public bool IsRunning => _networkManager != null && _networkManager.IsListening;

        /// <inheritdoc />
        public event Action Connected;

        /// <inheritdoc />
        public event Action Disconnected;

        /// <inheritdoc />
        public bool StartHost()
        {
            return _networkManager != null && _networkManager.StartHost();
        }

        /// <inheritdoc />
        public bool StartClient(ulong hostId)
        {
            if (_networkManager == null)
                return false;

            if (_networkManager.NetworkConfig?.NetworkTransport is FacepunchTransport facepunchTransport)
                facepunchTransport.targetSteamId = hostId;

            return _networkManager.StartClient();
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            if (_networkManager == null || !_networkManager.IsListening)
                return;

            _networkManager.Shutdown();
        }

        /// <summary>
        /// Unsubscribes from the manager's callbacks.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            if (_networkManager == null)
                return;

            _networkManager.OnClientConnectedCallback -= HandleClientConnected;
            _networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        }

        private void HandleClientConnected(ulong clientId)
        {
            // Also raised for every remote client on the server; only our own arrival counts.
            if (clientId == _networkManager.LocalClientId)
                Connected?.Invoke();
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (clientId == _networkManager.LocalClientId)
                Disconnected?.Invoke();
        }
    }
}
