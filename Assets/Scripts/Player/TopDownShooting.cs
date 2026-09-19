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
        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position + transform.forward;
        Quaternion spawnRot = transform.rotation;

        FireBulletRpc(spawnPos, spawnRot);
    }

    [Rpc(SendTo.Server)]
    private void FireBulletRpc(Vector3 position, Quaternion rotation)
    {
        if (bulletPrefab == null) return;

        GameObject bulletInstance = Instantiate(bulletPrefab, position, rotation);
        bulletInstance.GetComponent<NetworkObject>().Spawn(true);
    }
}