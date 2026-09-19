using Unity.Netcode;
using UnityEngine;

public class ChaserEnemy : NetworkBehaviour
{
    [Header("Параметры врага")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private int damage = 15;
    [SerializeField] private float attackRange = 1.4f;
    [SerializeField] private float attackCooldown = 1.0f;
    [SerializeField] private float retargetInterval = 0.25f;

    private Transform targetPlayer;
    private PlayerHealth targetHealth;
    private float nextAttackTime;
    private float nextSearchTime;

    public override void OnNetworkSpawn()
    {
        // ИИ считает только сервер. На клиентах Update выключаем полностью —
        // позицию и поворот приносит NetworkTransform.
        enabled = IsServer;

        // Разносим поиск цели по времени, чтобы все враги не искали в одном кадре
        nextSearchTime = Time.time + Random.value * retargetInterval;
    }

    private void Update()
    {
        if (!IsServer) return;

        if (Time.time >= nextSearchTime)
        {
            nextSearchTime = Time.time + retargetInterval;
            FindClosestPlayer();
        }

        if (targetPlayer == null) return;

        Vector3 direction = targetPlayer.position - transform.position;
        direction.y = 0f;
        float distance = direction.magnitude;

        if (distance > 0.1f)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }

        if (distance > attackRange)
        {
            transform.position += direction.normalized * (moveSpeed * Time.deltaTime);
        }
        else if (Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + attackCooldown;
            if (targetHealth != null)
            {
                targetHealth.TakeDamage(damage);
            }
        }
    }

    private void FindClosestPlayer()
    {
        var clients = NetworkManager.Singleton.ConnectedClientsList;
        Vector3 myPos = transform.position;

        float bestSqr = float.MaxValue;
        Transform best = null;

        for (int i = 0; i < clients.Count; i++)
        {
            NetworkObject playerObject = clients[i].PlayerObject;
            if (playerObject == null) continue;

            Vector3 offset = playerObject.transform.position - myPos;
            offset.y = 0f;
            float sqr = offset.sqrMagnitude;

            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = playerObject.transform;
            }
        }

        if (best != targetPlayer)
        {
            targetPlayer = best;
            targetHealth = best != null ? best.GetComponent<PlayerHealth>() : null;
        }
    }
}