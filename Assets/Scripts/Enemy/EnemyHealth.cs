using Unity.Netcode;
using UnityEngine;

public class EnemyHealth : NetworkBehaviour
{
    [SerializeField] private int maxHealth = 100;

    public NetworkVariable<int> currentHealth = new NetworkVariable<int>(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Renderer rend;
    private Material materialInstance;
    private bool isDead;

    private void Awake()
    {
        rend = GetComponent<Renderer>();
        if (rend != null)
        {
            // Один раз создаём копию материала (а не при каждом обновлении цвета)
            materialInstance = rend.material;
        }
    }

    public override void OnNetworkSpawn()
    {
        currentHealth.OnValueChanged += OnHealthChanged;

        if (IsServer)
        {
            currentHealth.Value = maxHealth;
        }

        UpdateVisual();
    }

    public override void OnNetworkDespawn()
    {
        currentHealth.OnValueChanged -= OnHealthChanged;

        if (materialInstance != null)
        {
            Destroy(materialInstance);
            materialInstance = null;
        }
    }

    private void OnHealthChanged(int oldVal, int newVal)
    {
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        if (materialInstance == null) return;

        float t = Mathf.Clamp01((float)currentHealth.Value / maxHealth);
        materialInstance.color = Color.Lerp(Color.red, Color.white, t);
    }

    public void TakeDamage(int damage)
    {
        if (!IsServer || !IsSpawned || isDead) return;

        currentHealth.Value = Mathf.Max(0, currentHealth.Value - damage);

        if (currentHealth.Value <= 0)
        {
            isDead = true;

            // Despawn(true) уничтожает объект и на сервере, и у всех клиентов.
            // Отдельный RPC на отключение больше не нужен.
            NetworkObject.Despawn(true);
        }
    }
}