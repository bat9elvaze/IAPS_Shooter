using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class EnemySpawner : NetworkBehaviour
{
    [Header("Настройки врагов")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private float spawnRadius = 18f;
    [SerializeField] private int maxEnemies = 25;

    [Header("Спавн по кнопке")]
    [SerializeField] private int enemiesPerClick = 1;

    private void Update()
    {
        // Горячая клавиша 'B' для быстрого спавна во время тестов
        if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
        {
            TriggerSpawn();
        }
    }

    /// <summary>
    /// Публичный метод для вызова по нажатию кнопки
    /// </summary>
    public void TriggerSpawn()
    {
        if (IsServer)
        {
            SpawnEnemiesServer(enemiesPerClick);
        }
        else
        {
            RequestSpawnServerRpc(enemiesPerClick);
        }
    }

    [Rpc(SendTo.Server)]
    private void RequestSpawnServerRpc(int count)
    {
        SpawnEnemiesServer(count);
    }

    private void SpawnEnemiesServer(int count)
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("[EnemySpawner] Префаб ChaserEnemy не назначен в инспекторе!");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            var activeEnemies = FindObjectsByType<ChaserEnemy>(FindObjectsSortMode.None);
            if (activeEnemies.Length >= maxEnemies) break;

            // Выбираем точку по кругу вокруг центра арены
            Vector2 randomCircle = Random.insideUnitCircle.normalized * spawnRadius;
            Vector3 spawnPos = new Vector3(randomCircle.x, 0.5f, randomCircle.y);

            GameObject enemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
            enemy.GetComponent<NetworkObject>().Spawn(true);
        }
    }

    private void OnGUI()
    {
        // Отображаем экранную кнопку в верхнем левом углу боевой сцены
        GUILayout.BeginArea(new Rect(15, 100, 180, 50));
        if (GUILayout.Button($"Спавн врага (+{enemiesPerClick}) [B]", GUILayout.Height(40)))
        {
            TriggerSpawn();
        }
        GUILayout.EndArea();
    }
}