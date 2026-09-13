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

    private void Awake()
    {
        rend = GetComponent<Renderer>();
    }

    public override void OnNetworkSpawn()
    {
        currentHealth.OnValueChanged += OnHealthChanged;
        UpdateVisual();
    }

    public override void OnNetworkDespawn()
    {
        currentHealth.OnValueChanged -= OnHealthChanged;
    }

    private void OnHealthChanged(int oldVal, int newVal)
    {
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        if (rend != null)
        {
            float t = Mathf.Clamp01((float)currentHealth.Value / maxHealth);
            rend.material.color = Color.Lerp(Color.red, Color.white, t);
        }
    }

    public void TakeDamage(int damage)
    {
        if (!IsServer) return;

        currentHealth.Value -= damage;

        if (currentHealth.Value <= 0)
        {
            // 1. СНАЧАЛА отправляем RPC клиентам, пока объект активен в сети
            DisableRpc();

            // 2. И ТОЛЬКО ПОТОМ деспавним его
            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(false);
            }
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void DisableRpc()
    {
        gameObject.SetActive(false);
    }
}