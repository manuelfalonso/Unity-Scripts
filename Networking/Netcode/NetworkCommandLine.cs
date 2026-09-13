#if NETCODE_GAMEOBJECTS
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace SombraStudios.Shared.Networking.Netcode
{

    /// <summary>
    /// Class to launch our project outside Unity using command line.
    /// Example to start server: 
    /// Executable.exe -mlapi server
    /// Example to start client and log its activity:
    /// Executable.exe -logfile log-client.txt -mlapi client
    /// All in one Server and Client wit logs:
    /// Executable.exe -logfile log-server.txt -mlapi server & Executable.exe -logfile log-client.txt -mlapi client
    /// </summary>
    public class NetworkCommandLine : MonoBehaviour
    {
        private NetworkManager netManager;

        // Start is called before the first frame update
        void Start()
        {
            netManager = GetComponentInParent<NetworkManager>();

            if (Application.isEditor) return;

            var args = GetCommandlineArgs();

            if (args.TryGetValue("-mlapi", out string mlapiValue))
            {
                switch (mlapiValue)
                {
                    case "host":
                        netManager.StartHost();
                        break;
                    case "server":
                        netManager.StartServer();
                        break;
                    case "client":
                        netManager.StartClient();
                        break;
                }
            }
        }

        private Dictionary<string, string> GetCommandlineArgs()
        {
            Dictionary<string, string> argDictionary = new Dictionary<string, string>();

            var args = System.Environment.GetCommandLineArgs();
		    //https://docs.microsoft.com/en-us/dotnet/api/system.environment.getcommandlineargs?view=net-5.0
		
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i].ToLower();
                if (arg.StartsWith("-"))
                {
                    var value = i < args.Length - 1 ? args[i + 1].ToLower() : null;
                    value = (value?.StartsWith("-") ?? false) ? null : value;

                    argDictionary.Add(arg, value);
                }
            }
            return argDictionary;
        }
    }
}
#endif