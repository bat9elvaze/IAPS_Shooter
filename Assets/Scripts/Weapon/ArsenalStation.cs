using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Станция "Арсенал" — НЕ сетевой объект, просто триггер-зона с UI.
/// Существует одинаково у хоста и у клиента, т.к. это обычный GameObject
/// сцены (не NetworkObject). Покупка/открытие кейсов идёт через ServerRpc
/// уже существующего PlayerInventory того игрока, который стоит в зоне —
/// никакой собственной сетевой логики станции не требуется.
/// </summary>
public class ArsenalStation : MonoBehaviour
{
    private enum StationTab { Shop, Discounts, Cases }

    /// <summary>
    /// true, пока панель ЛЮБОЙ станции открыта у локального игрока.
    /// TopDownShooting и TopDownPlayerController читают это, чтобы
    /// приостановить стрельбу/движение — как и для Tab-инвентаря
    /// (PlayerInventoryUI.IsOpen).
    /// </summary>
    public static bool IsOpenForLocalPlayer { get; private set; }

    [Header("Что продаёт эта станция")]
    [SerializeField] private string stationTitle = "Арсенал";
    [SerializeField] private List<WeaponDefinition> availableWeapons = new List<WeaponDefinition>();
    [SerializeField] private List<WeaponCaseDefinition> availableCases = new List<WeaponCaseDefinition>();

    [Header("Внешний вид")]
    [SerializeField] private Vector2 panelSize = new Vector2(440, 380);
    [SerializeField] private float cellSize = 72f;
    [SerializeField] private float cellSpacing = 10f;
    [SerializeField] private float resultMessageDuration = 4f;

    private static readonly Color ActiveTabColor = new Color(0.35f, 0.6f, 1f);
    private static readonly Color CurrencyColor = new Color(1f, 0.85f, 0.2f);
    private static readonly Color DiscountPriceColor = new Color(0.4f, 1f, 0.4f);
    private static readonly Color NewDropColor = new Color(0.4f, 1f, 0.4f);
    private static readonly Color DuplicateColor = new Color(1f, 0.7f, 0.3f);

    private PlayerInventory localPlayerInventory;
    private PlayerInventoryUI localPlayerInventoryUI;
    private bool playerInRange;
    private bool isOpen;
    private StationTab currentTab = StationTab.Shop;
    private Vector2 scrollPos;

    private string resultMessage;
    private Color resultMessageColor;
    private float resultMessageUntilTime;

    private GUIStyle titleStyle;
    private GUIStyle balanceStyle;
    private GUIStyle tabButtonStyle;
    private GUIStyle nameStyle;
    private GUIStyle priceStyle;
    private GUIStyle discountPriceStyle;
    private GUIStyle ownedPriceStyle;
    private GUIStyle hintStyle;
    private GUIStyle getButtonStyle;
    private GUIStyle placeholderStyle;
    private GUIStyle resultStyle;
    private bool stylesReady;

    private void OnTriggerEnter(Collider other)
    {
        PlayerInventory inventory = other.GetComponentInParent<PlayerInventory>();
        if (inventory == null || !inventory.IsOwner) return; // чужой игрок — не наш локальный

        localPlayerInventory = inventory;
        localPlayerInventoryUI = inventory.GetComponent<PlayerInventoryUI>();
        localPlayerInventory.CaseOpened += HandleCaseOpened;
        playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerInventory inventory = other.GetComponentInParent<PlayerInventory>();
        if (inventory == null || inventory != localPlayerInventory) return;

        playerInRange = false;
        CloseStation();
        localPlayerInventory.CaseOpened -= HandleCaseOpened;
        localPlayerInventory = null;
        localPlayerInventoryUI = null;
    }

    private void Update()
    {
        if (!playerInRange) return;
        if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;

        if (!isOpen && localPlayerInventoryUI != null && localPlayerInventoryUI.IsOpen)
        {
            return; // Tab-инвентарь уже открыт — не открываем Арсенал поверх него
        }

        if (isOpen) CloseStation();
        else OpenStation();
    }

    private void OpenStation()
    {
        isOpen = true;
        IsOpenForLocalPlayer = true;
    }

    private void CloseStation()
    {
        isOpen = false;
        IsOpenForLocalPlayer = false;
    }

    private void OnDisable()
    {
        // На случай выгрузки станции/сцены, пока панель была открыта —
        // не оставляем игрока навсегда "замороженным".
        if (isOpen) CloseStation();

        if (localPlayerInventory != null)
        {
            localPlayerInventory.CaseOpened -= HandleCaseOpened;
        }
    }

    private void HandleCaseOpened(int weaponId, bool wasDuplicate, int refundAmount)
    {
        WeaponDefinition weapon = WeaponDatabase.Instance != null ? WeaponDatabase.Instance.GetById(weaponId) : null;
        string weaponName = weapon != null ? weapon.WeaponName : $"#{weaponId}";

        if (wasDuplicate)
        {
            resultMessage = $"Дубликат «{weaponName}» — возвращено {refundAmount} монет";
            resultMessageColor = DuplicateColor;
        }
        else
        {
            resultMessage = $"Выпало: {weaponName}!";
            resultMessageColor = NewDropColor;
        }

        resultMessageUntilTime = Time.time + resultMessageDuration;
    }

    private void OnGUI()
    {
        if (!playerInRange || localPlayerInventory == null) return;

        EnsureStyles();

        if (!isOpen)
        {
            DrawHint();
            return;
        }

        DrawPanel();
    }

    private void DrawHint()
    {
        string text = $"E — {stationTitle}";
        Vector2 size = hintStyle.CalcSize(new GUIContent(text));
        GUI.Label(new Rect((Screen.width - size.x) / 2f, Screen.height - 80, size.x, 24), text, hintStyle);
    }

    private void DrawPanel()
    {
        Rect panelRect = new Rect(
            (Screen.width - panelSize.x) / 2f,
            (Screen.height - panelSize.y) / 2f,
            panelSize.x,
            panelSize.y);

        GUI.Box(panelRect, string.Empty);
        GUILayout.BeginArea(panelRect);
        GUILayout.Space(8);

        GUILayout.BeginHorizontal();
        GUILayout.Label(stationTitle, titleStyle);
        GUILayout.FlexibleSpace();
        GUILayout.Label($"Монеты: {localPlayerInventory.currency.Value}", balanceStyle);
        GUILayout.EndHorizontal();
        GUILayout.Space(6);

        DrawTabBar();

        if (Time.time < resultMessageUntilTime && !string.IsNullOrEmpty(resultMessage))
        {
            GUILayout.Space(6);
            GUIStyle coloredResultStyle = new GUIStyle(resultStyle) { normal = { textColor = resultMessageColor } };
            GUILayout.Label(resultMessage, coloredResultStyle);
        }

        GUILayout.Space(8);

        scrollPos = GUILayout.BeginScrollView(scrollPos);

        switch (currentTab)
        {
            case StationTab.Shop:
                DrawWeaponGrid(availableWeapons, showDiscountDetail: false);
                break;
            case StationTab.Discounts:
                DrawDiscountsTab();
                break;
            case StationTab.Cases:
                DrawCasesTab();
                break;
        }

        GUILayout.EndScrollView();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Закрыть (E)", GUILayout.Height(28)))
        {
            CloseStation();
        }

        GUILayout.EndArea();
    }

    private void DrawTabBar()
    {
        GUILayout.BeginHorizontal();
        DrawTabButton("Магазин", StationTab.Shop);
        DrawTabButton("Скидки", StationTab.Discounts);
        DrawTabButton("Кейсы", StationTab.Cases);
        GUILayout.EndHorizontal();
    }

    private void DrawTabButton(string label, StationTab tab)
    {
        bool isActive = currentTab == tab;
        Color previousBackground = GUI.backgroundColor;

        if (isActive) GUI.backgroundColor = ActiveTabColor;

        if (GUILayout.Button(label, tabButtonStyle, GUILayout.Height(26)))
        {
            currentTab = tab;
        }

        GUI.backgroundColor = previousBackground;
    }

    private void DrawDiscountsTab()
    {
        List<WeaponDefinition> discounted = new List<WeaponDefinition>();
        foreach (WeaponDefinition weapon in availableWeapons)
        {
            if (weapon != null && weapon.HasDiscount) discounted.Add(weapon);
        }

        if (discounted.Count == 0)
        {
            GUILayout.Label("Сейчас нет товаров со скидкой.", placeholderStyle);
            return;
        }

        DrawWeaponGrid(discounted, showDiscountDetail: true);
    }

    private void DrawCasesTab()
    {
        if (availableCases.Count == 0)
        {
            GUILayout.Label("Эта станция пока не продаёт кейсы.", placeholderStyle);
            return;
        }

        int columns = Mathf.Max(1, Mathf.FloorToInt((panelSize.x - 24) / (cellSize + cellSpacing)));
        int column = 0;

        GUILayout.BeginHorizontal();

        foreach (WeaponCaseDefinition caseDefinition in availableCases)
        {
            if (caseDefinition == null) continue;

            if (column == columns)
            {
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                column = 0;
            }

            DrawCaseCell(caseDefinition);
            column++;
        }

        GUILayout.EndHorizontal();
    }

    private void DrawCaseCell(WeaponCaseDefinition caseDefinition)
    {
        bool canAfford = localPlayerInventory.currency.Value >= caseDefinition.Price;

        GUILayout.BeginVertical(GUILayout.Width(cellSize + 6));

        Rect cellRect = GUILayoutUtility.GetRect(cellSize, cellSize);
        GUI.Box(cellRect, GUIContent.none);
        DrawIcon(ShrinkRect(cellRect, 4f), caseDefinition.Icon);

        GUILayout.Label(caseDefinition.CaseName, nameStyle, GUILayout.Width(cellSize + 6));
        GUILayout.Label($"{caseDefinition.Price} монет", priceStyle, GUILayout.Width(cellSize + 6));

        GUI.enabled = canAfford;
        if (GUILayout.Button("Открыть", getButtonStyle))
        {
            localPlayerInventory.OpenCaseServerRpc(caseDefinition.CaseId);
        }
        GUI.enabled = true;

        GUILayout.EndVertical();
        GUILayout.Space(cellSpacing);
    }

    private void DrawWeaponGrid(List<WeaponDefinition> weapons, bool showDiscountDetail)
    {
        int columns = Mathf.Max(1, Mathf.FloorToInt((panelSize.x - 24) / (cellSize + cellSpacing)));
        int column = 0;

        GUILayout.BeginHorizontal();

        foreach (WeaponDefinition weapon in weapons)
        {
            if (weapon == null) continue;

            if (column == columns)
            {
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                column = 0;
            }

            DrawWeaponCell(weapon, showDiscountDetail);
            column++;
        }

        GUILayout.EndHorizontal();
    }

    private void DrawWeaponCell(WeaponDefinition weapon, bool showDiscountDetail)
    {
        bool owned = localPlayerInventory.HasWeapon(weapon.WeaponId);
        int price = weapon.EffectivePrice;
        bool canAfford = localPlayerInventory.currency.Value >= price;

        GUILayout.BeginVertical(GUILayout.Width(cellSize + 6));

        Rect cellRect = GUILayoutUtility.GetRect(cellSize, cellSize);
        GUI.Box(cellRect, GUIContent.none);
        DrawIcon(ShrinkRect(cellRect, 4f), weapon.Icon);

        GUILayout.Label(weapon.WeaponName, nameStyle, GUILayout.Width(cellSize + 6));

        if (owned)
        {
            GUILayout.Label("Куплено", ownedPriceStyle, GUILayout.Width(cellSize + 6));
        }
        else if (showDiscountDetail && weapon.HasDiscount)
        {
            Rect oldPriceRect = GUILayoutUtility.GetRect(cellSize + 6, 16);
            DrawStrikethroughLabel(oldPriceRect, $"{weapon.Price}", priceStyle, Color.gray);
            GUILayout.Label($"{price} монет", discountPriceStyle, GUILayout.Width(cellSize + 6));
        }
        else
        {
            GUILayout.Label($"{price} монет", priceStyle, GUILayout.Width(cellSize + 6));
        }

        GUI.enabled = !owned && canAfford;
        if (GUILayout.Button(owned ? "Есть" : "Купить", getButtonStyle))
        {
            localPlayerInventory.BuyWeaponServerRpc(weapon.WeaponId);
        }
        GUI.enabled = true;

        GUILayout.EndVertical();
        GUILayout.Space(cellSpacing);
    }

    /// <summary>
    /// Рисует текст и поверх него — тонкую линию через середину, чтобы
    /// изобразить зачёркнутую (старую) цену. У OnGUI нет встроенного
    /// зачёркивания шрифта, поэтому линия рисуется отдельно поверх текста
    /// через Texture2D.whiteTexture — встроенную белую текстуру 1x1.
    /// Предполагает, что style выравнивает текст по центру (MiddleCenter).
    /// </summary>
    private void DrawStrikethroughLabel(Rect rect, string text, GUIStyle style, Color lineColor)
    {
        GUI.Label(rect, text, style);

        Vector2 textSize = style.CalcSize(new GUIContent(text));
        float lineY = rect.y + rect.height / 2f;
        float lineX = rect.x + (rect.width - textSize.x) / 2f;

        Color previous = GUI.color;
        GUI.color = lineColor;
        GUI.DrawTexture(new Rect(lineX, lineY, textSize.x, 1.5f), Texture2D.whiteTexture);
        GUI.color = previous;
    }

    // Та же логика вырезания иконки из спрайта, что и в PlayerInventoryUI —
    // обычный GUI.DrawTexture показал бы всю текстуру целиком, а не одну иконку.
    private void DrawIcon(Rect rect, Sprite sprite)
    {
        if (sprite == null || sprite.texture == null) return;

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

    private void EnsureStyles()
    {
        if (stylesReady) return;
        stylesReady = true;

        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
        titleStyle.normal.textColor = Color.white;

        balanceStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
        balanceStyle.normal.textColor = CurrencyColor;

        tabButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold };

        nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        nameStyle.normal.textColor = Color.white;

        priceStyle = new GUIStyle(nameStyle) { fontSize = 12 };
        priceStyle.normal.textColor = CurrencyColor;

        discountPriceStyle = new GUIStyle(priceStyle);
        discountPriceStyle.normal.textColor = DiscountPriceColor;
        discountPriceStyle.fontStyle = FontStyle.Bold;

        ownedPriceStyle = new GUIStyle(priceStyle);
        ownedPriceStyle.normal.textColor = Color.gray;

        hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        hintStyle.normal.textColor = Color.yellow;

        getButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = 12 };

        placeholderStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
        placeholderStyle.normal.textColor = Color.gray;

        resultStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
    }
}