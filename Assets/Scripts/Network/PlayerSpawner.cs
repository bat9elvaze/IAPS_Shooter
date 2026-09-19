using Unity.Netcode;
using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    [Header("Префаб игрока")]
    [SerializeField] private GameObject playerPrefab;

    [Header("Точки появления (необязательно)")]
    [SerializeField] private Transform[] spawnPoints;

    private void Start()
    {
        // Спавнить сетевые объекты имеет право только сервер/хост
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        // 1. Спавним сразу тех, кто уже загрузился (решает проблему гонки событий)
        SpawnPlayers();

        // 2. Подписываемся на события догрузки для клиентов, которые входят позже
        if (NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneEvent;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEvent;
        }
    }

    private void OnSceneEvent(SceneEvent sceneEvent)
    {
        // Если клиент завершил загрузку сцены позже хоста — спавним его
        if (sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted ||
            sceneEvent.SceneEventType == SceneEventType.LoadComplete)
        {
            SpawnPlayers();
        }
    }

    public void SpawnPlayers()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[PlayerSpawner] Ошибка: Слот 'Player Prefab' в инспекторе пуст! Перетащите туда TopDownPlayer.");
            return;
        }

        var clients = NetworkManager.Singleton.ConnectedClientsList;
        int spawnIndex = 0;

        for (int i = 0; i < clients.Count; i++)
        {
            ulong clientId = clients[i].ClientId;

            // Если у этого игрока уже есть созданный персонаж — пропускаем
            if (clients[i].PlayerObject != null) continue;

            Vector3 spawnPos;
            Quaternion spawnRot = Quaternion.identity;

            if (spawnPoints != null && spawnPoints.Length > 0 && spawnPoints[spawnIndex % spawnPoints.Length] != null)
            {
                Transform point = spawnPoints[spawnIndex % spawnPoints.Length];
                spawnPos = point.position;
                spawnRot = point.rotation;
                spawnIndex++;
            }
            else
            {
                // Если точки спавна не заданы — расставляем игроков с шагом в 2.5 метра
                spawnPos = new Vector3(i * 2.5f, 0.5f, 0f);
            }

            GameObject playerInstance = Instantiate(playerPrefab, spawnPos, spawnRot);
            NetworkObject netObj = playerInstance.GetComponent<NetworkObject>();
            netObj.SpawnAsPlayerObject(clientId, true);

            Debug.Log($"[PlayerSpawner] Персонаж для ClientID {clientId} успешно заспавнен на {spawnPos}!");
        }
    }
}