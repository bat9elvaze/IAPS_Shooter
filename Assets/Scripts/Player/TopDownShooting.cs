using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class TopDownShooting : NetworkBehaviour
{
    [Header("Настройки стрельбы")]
    [SerializeField] private float fireRate = 0.15f;
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject bulletPrefab;

    private float nextFireTime;
    private static bool warnedAboutNetworkBullet;

    private void Update()
    {
        if (!IsOwner) return;

        if (Mouse.current != null && Mouse.current.leftButton.isPressed && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireRate;
            Shoot();
        }
    }

    private void Shoot()
    {
        Vector3 spawnPos = firePoint != null
            ? firePoint.position
            : transform.position + transform.forward * 1.2f + Vector3.up * 0.5f;

        Vector3 direction = transform.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;
        direction.Normalize();

        // Клиент сразу показывает свою пулю, не дожидаясь сервера (предсказание).
        // У хоста боевая пуля создаётся мгновенно внутри FireServerRpc.
        if (!IsServer)
        {
            SpawnBullet(spawnPos, direction, false);
        }

        FireServerRpc(spawnPos, direction);
    }

    // Выполняется на сервере (если стреляет хост — сразу локально)
    [Rpc(SendTo.Server)]
    private void FireServerRpc(Vector3 position, Vector3 direction)
    {
        // Боевая пуля: только она наносит урон
        SpawnBullet(position, direction, true);

        // Остальным игрокам отправляем только визуал
        FireVisualRpc(position, direction);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void FireVisualRpc(Vector3 position, Vector3 direction)
    {
        if (IsServer) return; // у сервера/хоста уже есть боевая пуля
        if (IsOwner) return;  // у стрелка уже есть своя предсказанная пуля

        SpawnBullet(position, direction, false);
    }

    private void SpawnBullet(Vector3 position, Vector3 direction, bool authoritative)
    {
        if (bulletPrefab == null) return;
        if (direction.sqrMagnitude < 0.0001f) return;

        GameObject instance = Instantiate(bulletPrefab, position, Quaternion.LookRotation(direction));

        if (!warnedAboutNetworkBullet && instance.GetComponent<NetworkObject>() != null)
        {
            warnedAboutNetworkBullet = true;
            Debug.LogError("[TopDownShooting] На префабе пули остался NetworkObject/NetworkTransform. " +
                           "Удалите их (сначала NetworkTransform, потом NetworkObject) — пуля теперь не сетевой объект.", bulletPrefab);
        }

        if (instance.TryGetComponent<Bullet>(out var bullet))
        {
            bullet.Init(authoritative);
        }
    }
}