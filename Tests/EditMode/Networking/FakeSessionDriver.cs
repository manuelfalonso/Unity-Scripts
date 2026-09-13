using System;
using SombraStudios.Shared.Networking.Sessions;

namespace SombraStudios.Shared.Tests.Networking
{
    /// <summary>
    /// An <see cref="INetworkSessionDriver"/> that records calls instead of starting a netcode layer.
    /// </summary>
    /// <remarks>
    /// <see cref="Shutdown"/> raises <see cref="Disconnected"/> the way Netcode for GameObjects does,
    /// because that re-entrancy is precisely what the controller teardown guard has to survive.
    /// </remarks>
    internal sealed class FakeSessionDriver : INetworkSessionDriver
    {
        public bool IsRunning { get; private set; }

        public bool StartHostResult { get; set; } = true;

        public bool StartClientResult { get; set; } = true;

        public int StartHostCalls { get; private set; }

        public int StartClientCalls { get; private set; }

        public int ShutdownCalls { get; private set; }

        public ulong LastHostId { get; private set; }

        public event Action Connected;
        public event Action Disconnected;

        public bool StartHost()
        {
            StartHostCalls++;

            if (!StartHostResult)
                return false;

            IsRunning = true;
            return true;
        }

        public bool StartClient(ulong hostId)
        {
            StartClientCalls++;
            LastHostId = hostId;

            if (!StartClientResult)
                return false;

            IsRunning = true;
            return true;
        }

        public void Shutdown()
        {
            ShutdownCalls++;

            if (!IsRunning)
                return;

            IsRunning = false;
            Disconnected?.Invoke();
        }

        /// <summary>Mimics the local client completing its handshake with the host.</summary>
        public void RaiseConnected()
        {
            Connected?.Invoke();
        }

        /// <summary>Mimics an unexpected drop, as opposed to a deliberate shutdown.</summary>
        public void RaiseDisconnected()
        {
            IsRunning = false;
            Disconnected?.Invoke();
        }
    }
}
