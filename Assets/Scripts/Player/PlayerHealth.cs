using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class PlayerHealth : NetworkBehaviour
{
    [Header("Параметры здоровья")]
    [SerializeField] private int maxHealth = 100;

    public NetworkVariable<int> currentHealth = new NetworkVariable<int>(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private GUIStyle hpStyle;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            currentHealth.Value = maxHealth;
        }
    }

    public void TakeDamage(int damage)
    {
        if (!IsServer) return;

        currentHealth.Value = Mathf.Max(0, currentHealth.Value - damage);

        if (currentHealth.Value <= 0)
        {
            Respawn();
        }
    }

    private void Respawn()
    {
        currentHealth.Value = maxHealth;

        // Позицией игрока управляет его владелец (ClientNetworkTransform),
        // поэтому телепортировать нужно именно на стороне владельца.
        RespawnRpc(Vector3.zero);
    }

    [Rpc(SendTo.Owner)]
    private void RespawnRpc(Vector3 position)
    {
        TryGetComponent<CharacterController>(out var cc);
        if (cc != null) cc.enabled = false;

        transform.position = position;

        if (TryGetComponent<NetworkTransform>(out var netTransform))
        {
            // Телепорт без плавного «пролёта» через всю карту у других игроков
            netTransform.Teleport(transform.position, transform.rotation, transform.localScale);
        }

        if (cc != null) cc.enabled = true;
    }

    private void OnGUI()
    {
        // Отображаем индикатор здоровья только локальному игроку
        if (!IsOwner || !IsSpawned) return;

        if (hpStyle == null)
        {
            hpStyle = new GUIStyle { fontSize = 22 };
        }

        hpStyle.normal.textColor = currentHealth.Value > 30 ? Color.green : Color.red;
        GUI.Label(new Rect(20, Screen.height - 50, 200, 40), $"HP: {currentHealth.Value} / {maxHealth}", hpStyle);
    }
}