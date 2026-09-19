using Unity.Netcode;
using UnityEngine;

public class ChaserEnemy : NetworkBehaviour
{
    [Header("Параметры движения")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float attackRange = 1.4f;

    [Header("Параметры атаки")]
    [SerializeField] private int attackDamage = 15;
    [SerializeField] private float attackCooldown = 1.0f;

    private float nextAttackTime;
    private Transform currentTarget;

    private void Update()
    {
        // Логику поведения рассчитывает только сервер/хост
        if (!IsServer) return;

        FindClosestPlayer();

        if (currentTarget == null) return;

        Vector3 targetPosition = currentTarget.position;
        targetPosition.y = transform.position.y; // Двигаемся только в плоскости пола

        float distance = Vector3.Distance(transform.position, targetPosition);

        // Поворот к цели
        Vector3 direction = (targetPosition - transform.position).normalized;
        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }

        // Если не дошли до дистанции удара — идем вперед
        if (distance > attackRange)
        {
            transform.position += direction * (moveSpeed * Time.deltaTime);
        }
        else
        {
            // Наносим урон, если кулдаун прошел
            if (Time.time >= nextAttackTime)
            {
                AttackCurrentTarget();
            }
        }
    }

    private void AttackCurrentTarget()
    {
        if (currentTarget != null && currentTarget.TryGetComponent<PlayerHealth>(out var playerHealth))
        {
            playerHealth.TakeDamage(attackDamage);
            nextAttackTime = Time.time + attackCooldown;
        }
    }

    private void FindClosestPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        float closestDistance = float.MaxValue;
        Transform bestTarget = null;

        foreach (GameObject p in players)
        {
            float dist = Vector3.Distance(transform.position, p.transform.position);
            if (dist < closestDistance)
            {
                closestDistance = dist;
                bestTarget = p.transform;
            }
        }

        currentTarget = bestTarget;
    }
}