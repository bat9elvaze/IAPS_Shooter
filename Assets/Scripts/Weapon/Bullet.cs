using UnityEngine;

/// <summary>
/// Обычная (НЕ сетевая) пуля. Каждый игрок создаёт её у себя локально,
/// летит она у всех одинаково, а урон наносит только сервер/хост.
/// ВАЖНО: на префабе пули не должно быть NetworkObject и NetworkTransform.
/// </summary>
public class Bullet : MonoBehaviour
{
    [Header("Параметры снаряда")]
    [SerializeField] private float speed = 28f;
    [SerializeField] private int damage = 25;
    [SerializeField] private float lifeTime = 2.5f;

    [Header("Отладка")]
    [Tooltip("Писать в консоль, во что попала пуля")]
    [SerializeField] private bool logHits = false;

    private bool dealsDamage; // true только у боевой пули на сервере/хосте
    private bool hasHit;

    public void Init(bool authoritative)
    {
        dealsDamage = authoritative;
    }

    private void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    private void Update()
    {
        transform.Translate(Vector3.forward * (speed * Time.deltaTime));
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;

        if (logHits)
        {
            Debug.Log($"[Bullet] Контакт с '{other.name}' (trigger: {other.isTrigger})", other);
        }

        // 1. Игроки: игнорируем любые их коллайдеры, в том числе дочерние
        //    и "запаздывающие копии" игроков-клиентов на сервере
        if (other.GetComponentInParent<PlayerHealth>() != null) return;

        // 2. Враг
        EnemyHealth enemy = other.GetComponentInParent<EnemyHealth>();
        if (enemy != null)
        {
            hasHit = true;
            if (dealsDamage) enemy.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        // 3. Триггеры и прочие пули игнорируем
        if (other.isTrigger) return;

        // 4. Стены / препятствия
        hasHit = true;
        Destroy(gameObject);
    }
}