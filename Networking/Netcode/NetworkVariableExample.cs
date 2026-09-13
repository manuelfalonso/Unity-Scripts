#if NETCODE_GAMEOBJECTS && DOTWEEN
using Unity.Netcode;
using UnityEngine;
using DG.Tweening;

namespace SombraStudios.Shared.Networking.Netcode
{

	/// <summary>
	/// Example Class of a Network Variable of type Vector3 where the server 
	/// directly modify the Variable. In the case of a client it ask through 
	/// Server Rpc to modify the Variable.
	/// Required: DoTween package installed and setup
	/// </summary>
	public class NetworkVariableExample : NetworkBehaviour
	{
		public NetworkVariable<Vector3> Position = new NetworkVariable<Vector3>();
			
		public override void OnNetworkSpawn()
		{
			Move();
		}

		public void Move()
		{
			if (NetworkManager.Singleton.IsServer)
			{
				var randomPosition = GetRandomPositionOnPlane();
			
				transform.DOLocalMove(randomPosition, 1f);
				Position.Value = randomPosition;
			}
			else
			{
				SubmitPositionRequestServerRpc();
			}
		}

		[ServerRpc]
		void SubmitPositionRequestServerRpc(ServerRpcParams rpcParams = default)
		{
			Position.Value = GetRandomPositionOnPlane();
		}

		static Vector3 GetRandomPositionOnPlane()
		{
			return new Vector3(Random.Range(-3f, 3f), 1f, Random.Range(-3f, 3f));
		}

		void Update()
		{
			transform.DOLocalMove(Position.Value, 1f);
		}
	}
}
#endif