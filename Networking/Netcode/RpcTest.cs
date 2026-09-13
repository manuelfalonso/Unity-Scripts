#if NETCODE_GAMEOBJECTS
using UnityEngine;
using Unity.Netcode;

namespace SombraStudios.Shared.Networking.Netcode
{

    /// <summary>
    /// Small Class example of the RPC messaging system.
    /// In this example the Client is calling a ServerRpc to log a meesage with a int.
    /// After that the server call ClientRpc to do the same but also increase the int.
    /// RPC Remote Procedure Call
    /// </summary>
    public class RpcTest : NetworkBehaviour
    {
        public override void OnNetworkSpawn()
        {
            if (IsClient)
            {
                TestServerRpc(0);
            }
        }

        [ServerRpc]
        private void TestServerRpc(int value)
        {
            Debug.Log("Server Received the RPC #" + value);
            TestClientRpc(value);
        }

        [ClientRpc]
        private void TestClientRpc(int value)
        {
            if (IsClient)
            {
                Debug.Log("Client Received the RPC #" + value);
                TestServerRpc(value + 1);
            }
        }
    }
}
#endif