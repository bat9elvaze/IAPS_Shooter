using Unity.Netcode;
using UnityEngine;

public class NetworkUI : MonoBehaviour
{
    private void OnGUI()
    {
        // Защита от обращения к менеджеру до его старта
        if (NetworkManager.Singleton == null) return;

        GUILayout.BeginArea(new Rect(15, 15, 220, 160));

        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            if (GUILayout.Button("Start Host", GUILayout.Height(35))) NetworkManager.Singleton.StartHost();
            if (GUILayout.Button("Start Client", GUILayout.Height(35))) NetworkManager.Singleton.StartClient();
        }
        else
        {
            string mode = NetworkManager.Singleton.IsHost ? "Host" : "Client";
            GUILayout.Label($"Режим: {mode}");

            // ConnectedClientsList доступен ТОЛЬКО серверу/хосту!
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