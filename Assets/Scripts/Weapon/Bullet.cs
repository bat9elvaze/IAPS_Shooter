using Unity.Netcode;
using UnityEngine;

public class Bullet : NetworkBehaviour
{
    [Header("Параметры пули")]
    [SerializeField] private float speed = 28f;
    [SerializeField] private float lifeTime = 2.5f;
    [SerializeField] private int damage = 25;

    private float spawnTime;

    public override void OnNetworkSpawn()
    {
        spawnTime = Time.time;
    }

    private void Update()
    {
        // Пуля летит вперед с одинаковой скоростью и у сервера, и у клиентов
        transform.position += transform.forward * (speed * Time.deltaTime);

        // Уничтожение по истечении времени жизни (контролирует только сервер)
        if (IsServer && Time.time >= spawnTime + lifeTime)
        {
            DespawnBullet();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Урон и попадания обрабатывает строго сервер
        if (!IsServer) return;

        // Игнорируем самих игроков, чтобы пуля не взрывалась внутри стреляющего
        if (other.CompareTag("Player")) return;

        // Если попали в объект со здоровьем — наносим урон
        if (other.TryGetComponent<EnemyHealth>(out var health))
        {
            health.TakeDamage(damage);
        }

        DespawnBullet();
    }

    private void DespawnBullet()
    {
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
    }
}