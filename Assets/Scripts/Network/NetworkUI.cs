using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class NetworkUI : MonoBehaviour
{
    private string targetIp = "127.0.0.1";

    private void OnGUI()
    {
        if (NetworkManager.Singleton == null) return;

        GUILayout.BeginArea(new Rect(15, 15, 240, 220));

        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            if (GUILayout.Button("Start Host", GUILayout.Height(35)))
            {
                NetworkManager.Singleton.StartHost();
            }

            GUILayout.Space(10);
            GUILayout.Label("IP хоста для подключения:");
            targetIp = GUILayout.TextField(targetIp);

            if (GUILayout.Button("Start Client", GUILayout.Height(35)))
            {
                // Применяем введенный IP в сетевой транспорт
                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                if (transport != null)
                {
                    transport.ConnectionData.Address = targetIp.Trim();
                }

                NetworkManager.Singleton.StartClient();
            }
        }
        else
        {
            string mode = NetworkManager.Singleton.IsHost ? "Host" : "Client";
            GUILayout.Label($"Режим: {mode}");

            if (NetworkManager.Singleton.IsServer)
            {
                GUILayout.Label($"Игроков: {NetworkManager.Singleton.ConnectedClientsList.Count}");
            }

            if (GUILayout.Button("Отключиться", GUILayout.Height(30)))
            {
                NetworkManager.Singleton.Shutdown();
            }
        }

        GUILayout.EndArea();
    }
}