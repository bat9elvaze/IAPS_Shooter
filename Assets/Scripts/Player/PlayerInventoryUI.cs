using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// HUD (хотбар на два слота + баланс валюты) и полный экран инвентаря
/// (Tab — открыть/закрыть), в виде клеток с иконками оружия
/// (WeaponDefinition.Icon). Показывает только то оружие, которое у игрока
/// реально есть — непрополученное оружие в список не попадает вообще.
/// Видно только владельцу. Реализовано через OnGUI (как PlayerHealth) — без Canvas.
/// </summary>
public class PlayerInventoryUI : NetworkBehaviour
{
    [Header("Внешний вид панели")]
    [SerializeField] private Vector2 panelSize = new Vector2(420, 460);
    [SerializeField] private float cellSize = 72f;
    [SerializeField] private float cellSpacing = 10f;

    [Header("Внешний вид хотбара (когда инвентарь закрыт)")]
    [SerializeField] private Vector2 hotbarPosition = new Vector2(20, 20);
    [SerializeField] private float hotbarCellSize = 56f;

    private static readonly Color OwnedTint = Color.white;
    private static readonly Color Slot1Border = new Color(0.3f, 0.9f, 0.4f);
    private static readonly Color Slot2Border = new Color(0.3f, 0.6f, 1f);
    // Подсвечивает оружие, которое реально в руках (equippedWeaponId), а не то,
    // какой слот считается "активным" — эти два понятия могут разойтись
    // (например, если оружие переставили из активного слота в другой).
    private static readonly Color EquippedBorder = Color.yellow;
    private static readonly Color EquippedFill = new Color(0.35f, 0.28f, 0.05f, 0.95f);
    private static readonly Color NeutralFill = new Color(0.12f, 0.12f, 0.12f, 0.9f);
    private static readonly Color CurrencyColor = new Color(1f, 0.85f, 0.2f);

    private PlayerInventory inventory;
    private bool isOpen;
    private Vector2 scrollPos;

    private GUIStyle titleStyle;
    private GUIStyle nameStyle;
    private GUIStyle emptyStateStyle;
    private GUIStyle slotButtonStyle;
    private GUIStyle equippedHintStyle;
    private GUIStyle currencyStyle;
    private bool stylesReady;

    /// <summary>Пока true — TopDownShooting и TopDownPlayerController приостанавливают ввод.</summary>
    public bool IsOpen => isOpen;

    private void Awake()
    {
        inventory = GetComponent<PlayerInventory>();
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            if (!isOpen && ArsenalStation.IsOpenForLocalPlayer) return; // Арсенал уже открыт

            isOpen = !isOpen;
        }
    }

    private void OnGUI()
    {
        if (!IsOwner || !IsSpawned || inventory == null) return;

        EnsureStyles();

        if (!isOpen)
        {
            DrawHotbar();
            return;
        }

        DrawInventoryPanel();
    }

    private void DrawHotbar()
    {
        int equippedId = inventory.equippedWeaponId.Value;

        for (int slotIndex = 0; slotIndex < PlayerInventory.SlotCount; slotIndex++)
        {
            Rect cellRect = new Rect(
                hotbarPosition.x + slotIndex * (hotbarCellSize + cellSpacing),
                hotbarPosition.y,
                hotbarCellSize,
                hotbarCellSize);

            WeaponDefinition weapon = WeaponDefinitionOrNull(inventory.GetSlotWeaponId(slotIndex));
            bool isEquipped = weapon != null && weapon.WeaponId == equippedId;

            DrawCellBackground(cellRect, isEquipped ? EquippedBorder : Color.black, strongHighlight: isEquipped);
            DrawWeaponIcon(ShrinkRect(cellRect, 3f), weapon);
            GUI.Label(new Rect(cellRect.x + 4, cellRect.y + 2, 18, 16), (slotIndex + 1).ToString(), nameStyle);

            if (isEquipped)
            {
                GUI.Label(new Rect(cellRect.x - 2, cellRect.y + hotbarCellSize + 2, hotbarCellSize + 4, 16),
                    "В руках", equippedHintStyle);
            }
        }

        // Баланс валюты — справа от двух слотов, по вертикали на уровне клеток.
        float currencyLabelX = hotbarPosition.x + PlayerInventory.SlotCount * (hotbarCellSize + cellSpacing) + 6f;
        float currencyLabelY = hotbarPosition.y + (hotbarCellSize - 24f) / 2f;
        GUI.Label(new Rect(currencyLabelX, currencyLabelY, 160, 24), $"Монеты: {inventory.currency.Value}", currencyStyle);

        GUI.Label(new Rect(hotbarPosition.x, hotbarPosition.y + hotbarCellSize + 24, 300, 20),
            "Tab — инвентарь", nameStyle);
    }

    private void DrawInventoryPanel()
    {
        Rect panelRect = new Rect(
            (Screen.width - panelSize.x) / 2f,
            (Screen.height - panelSize.y) / 2f,
            panelSize.x,
            panelSize.y);

        GUI.Box(panelRect, string.Empty);
        GUILayout.BeginArea(panelRect);
        GUILayout.Space(8);
        GUILayout.Label("Инвентарь", titleStyle);
        GUILayout.Space(6);

        DrawSlotSummaryRow();
        GUILayout.Space(10);

        scrollPos = GUILayout.BeginScrollView(scrollPos);
        DrawWeaponGrid();
        GUILayout.EndScrollView();

        if (GUILayout.Button("Закрыть (Tab)", GUILayout.Height(28)))
        {
            isOpen = false;
        }

        GUILayout.EndArea();
    }

    private void DrawSlotSummaryRow()
    {
        GUILayout.BeginHorizontal();

        for (int slotIndex = 0; slotIndex < PlayerInventory.SlotCount; slotIndex++)
        {
            WeaponDefinition weapon = WeaponDefinitionOrNull(inventory.GetSlotWeaponId(slotIndex));
            bool isActiveSlot = slotIndex == inventory.activeSlotIndex.Value;

            GUILayout.BeginVertical(GUILayout.Width(cellSize + 6));
            Rect cellRect = GUILayoutUtility.GetRect(cellSize, cellSize);
            DrawCellBackground(cellRect, isActiveSlot ? EquippedBorder : Color.black, strongHighlight: isActiveSlot);
            DrawWeaponIcon(ShrinkRect(cellRect, 3f), weapon);
            GUILayout.Label($"Слот {slotIndex + 1}: {(weapon != null ? weapon.WeaponName : "пусто")}", nameStyle);
            GUILayout.EndVertical();

            GUILayout.Space(cellSpacing);
        }

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    private void DrawWeaponGrid()
    {
        if (WeaponDatabase.Instance == null) return;

        int columns = Mathf.Max(1, Mathf.FloorToInt((panelSize.x - 24) / (cellSize + cellSpacing)));
        int column = 0;
        int shownCount = 0;

        GUILayout.BeginHorizontal();

        foreach (WeaponDefinition weapon in WeaponDatabase.Instance.AllWeapons)
        {
            // Показываем только то, что у игрока реально есть — непрополученное
            // оружие в инвентаре вообще не отображается (раньше было серым и
            // недоступным, теперь его тут просто нет).
            if (weapon == null || !inventory.HasWeapon(weapon.WeaponId)) continue;

            if (column == columns)
            {
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                column = 0;
            }

            DrawWeaponCell(weapon);
            column++;
            shownCount++;
        }

        GUILayout.EndHorizontal();

        if (shownCount == 0)
        {
            GUILayout.Label("Оружия пока нет — найди станцию «Арсенал».", emptyStateStyle);
        }
    }

    private void DrawWeaponCell(WeaponDefinition weapon)
    {
        int slot1 = inventory.GetSlotWeaponId(0);
        int slot2 = inventory.GetSlotWeaponId(1);

        GUILayout.BeginVertical(GUILayout.Width(cellSize + 6));

        Rect cellRect = GUILayoutUtility.GetRect(cellSize, cellSize);
        Color border = weapon.WeaponId == slot1 ? Slot1Border
                      : weapon.WeaponId == slot2 ? Slot2Border
                      : Color.black;
        DrawCellBackground(cellRect, border);
        DrawWeaponIcon(ShrinkRect(cellRect, 3f), weapon);

        GUILayout.Label(weapon.WeaponName, nameStyle, GUILayout.Width(cellSize + 6));

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("1", slotButtonStyle, GUILayout.Width((cellSize - 4) / 2f)))
        {
            inventory.AssignToSlotServerRpc(0, weapon.WeaponId);
        }
        if (GUILayout.Button("2", slotButtonStyle, GUILayout.Width((cellSize - 4) / 2f)))
        {
            inventory.AssignToSlotServerRpc(1, weapon.WeaponId);
        }
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
        GUILayout.Space(cellSpacing);
    }

    private void DrawCellBackground(Rect rect, Color borderColor, bool strongHighlight = false)
    {
        Color previous = GUI.color;

        GUI.color = borderColor;
        GUI.Box(rect, GUIContent.none);

        GUI.color = strongHighlight ? EquippedFill : NeutralFill;
        GUI.Box(ShrinkRect(rect, 2f), GUIContent.none);

        GUI.color = previous;
    }

    private void DrawWeaponIcon(Rect rect, WeaponDefinition weapon)
    {
        if (weapon == null || weapon.Icon == null) return;

        Color previous = GUI.color;
        GUI.color = OwnedTint; // всё, что тут рисуется, уже точно есть у игрока

        DrawSprite(rect, weapon.Icon);

        GUI.color = previous;
    }

    /// <summary>
    /// Рисует нужный участок текстуры спрайта. Обычный GUI.DrawTexture показал бы
    /// всю текстуру целиком — если несколько иконок упакованы в общий атлас,
    /// на месте одной иконки оказался бы весь атлас разом. DrawTextureWithTexCoords
    /// с UV, посчитанными из sprite.rect, вырезает именно нужный кусок.
    /// </summary>
    private void DrawSprite(Rect rect, Sprite sprite)
    {
        if (sprite.texture == null) return;

        Rect uv = new Rect(
            sprite.rect.x / sprite.texture.width,
            sprite.rect.y / sprite.texture.height,
            sprite.rect.width / sprite.texture.width,
            sprite.rect.height / sprite.texture.height);

        GUI.DrawTextureWithTexCoords(rect, sprite.texture, uv);
    }

    private Rect ShrinkRect(Rect rect, float amount)
    {
        return new Rect(rect.x + amount, rect.y + amount, rect.width - amount * 2f, rect.height - amount * 2f);
    }

    private WeaponDefinition WeaponDefinitionOrNull(int weaponId)
    {
        if (weaponId == PlayerInventory.NoWeaponId || WeaponDatabase.Instance == null) return null;
        return WeaponDatabase.Instance.GetById(weaponId);
    }

    private void EnsureStyles()
    {
        if (stylesReady) return;
        stylesReady = true;

        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
        titleStyle.normal.textColor = Color.white;

        nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        nameStyle.normal.textColor = Color.white;

        emptyStateStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
        emptyStateStyle.normal.textColor = Color.gray;

        slotButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = 12 };

        equippedHintStyle = new GUIStyle(nameStyle) { fontSize = 11, fontStyle = FontStyle.Bold };
        equippedHintStyle.normal.textColor = EquippedBorder;

        currencyStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
        currencyStyle.normal.textColor = CurrencyColor;
    }
}