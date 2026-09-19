using Unity.Netcode;
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
        // Простой респавн на стартовую позицию с полным HP
        currentHealth.Value = maxHealth;

        if (TryGetComponent<CharacterController>(out var cc))
        {
            cc.enabled = false;
            transform.position = Vector3.zero;
            cc.enabled = true;
        }
        else
        {
            transform.position = Vector3.zero;
        }
    }

    private void OnGUI()
    {
        // Отображаем индикатор здоровья только локальному игроку
        if (!IsOwner) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 22;
        style.normal.textColor = currentHealth.Value > 30 ? Color.green : Color.red;

        GUI.Label(new Rect(20, Screen.height - 50, 200, 40), $"HP: {currentHealth.Value} / {maxHealth}", style);
    }
}