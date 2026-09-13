#if NETCODE_GAMEOBJECTS
using Unity.Netcode;
using UnityEngine;

namespace SombraStudios.Shared.Networking.Netcode
{
    /// <summary>
    /// Approval check using a string room password.
    /// </summary>
    /// <remarks>
    /// The password travels in <see cref="NetworkConfig.ConnectionData"/>, which is sent in the
    /// clear during the handshake — it keeps honest players out of the wrong room, it is not
    /// security. See https://docs-multiplayer.unity3d.com/netcode/current/basics/connection-approval/
    /// </remarks>
    public class ConnectionApproval : MonoBehaviour
    {
        [SerializeField] private Vector3 _positionToSpawnAt;
        [SerializeField] private Quaternion _rotationToSpawnWith = Quaternion.identity;

        [SerializeField] private string _roomPassword = "1234";
        [SerializeField] private string _inputRoomPassword = "5678";

        private void SetupHost()
        {
            // Assigned, not subscribed: the setter rejects a delegate with more than one
            // handler, so "+=" throws as soon as a second one registers.
            NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;

            // Without this the callback is never invoked and Netcode logs a warning.
            NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;

            NetworkManager.Singleton.StartHost();
        }

        private void SetupClient()
        {
            NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
            NetworkManager.Singleton.NetworkConfig.ConnectionData =
                System.Text.Encoding.ASCII.GetBytes(_inputRoomPassword);
            NetworkManager.Singleton.StartClient();
        }

        private void ApprovalCheck(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            // Your logic here.
            var approve = System.Text.Encoding.ASCII.GetString(request.Payload) == _roomPassword;

            // If Approved is true the connection gets added. If it's false the client is
            // disconnected, and Reason is what it sees.
            response.Approved = approve;
            response.CreatePlayerObject = true;

            // Null uses the default player prefab.
            response.PlayerPrefabHash = null;
            response.Position = _positionToSpawnAt;
            response.Rotation = _rotationToSpawnWith;

            if (!approve)
                response.Reason = "Wrong room password.";
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 250, 300));

            GUILayout.Label("Test Setup Connection Approval");
            GUILayout.Label("Room Password: " + _roomPassword);
            if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            {
                if (GUILayout.Button("Start Host")) SetupHost();
                if (GUILayout.Button("Start Client")) SetupClient();
            }
            GUILayout.Label("Input Room Password");
            _inputRoomPassword = GUILayout.TextField(_inputRoomPassword, 4);

            GUILayout.EndArea();
        }
    }
}
#endif
