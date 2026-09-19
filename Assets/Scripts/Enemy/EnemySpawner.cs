using Unity.Netcode;
using UnityEngine;

public class EnemySpawner : NetworkBehaviour
{
    [Header("Настройки спавна")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private float spawnInterval = 3f;
    [SerializeField] private float spawnRadius = 18f;
    [SerializeField] private int maxEnemies = 10;

    private float nextSpawnTime;

    private void Update()
    {
        // Спавнить сетевые объекты может ТОЛЬКО сервер
        if (!IsServer) return;

        if (Time.time >= nextSpawnTime)
        {
            nextSpawnTime = Time.time + spawnInterval;
            TrySpawnEnemy();
        }
    }

    private void TrySpawnEnemy()
    {
        if (enemyPrefab == null) return;

        // Ограничение на количество живых врагов на арене
        int currentEnemies = FindObjectsByType<ChaserEnemy>(FindObjectsSortMode.None).Length;
        if (currentEnemies >= maxEnemies) return;

        // Выбираем случайную точку по кругу
        Vector2 randomCircle = Random.insideUnitCircle.normalized * spawnRadius;
        Vector3 spawnPos = new Vector3(randomCircle.x, 0.5f, randomCircle.y);

        GameObject enemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
        enemy.GetComponent<NetworkObject>().Spawn(true);
    }
}