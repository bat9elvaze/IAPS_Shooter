using UnityEngine;

/// <summary>
/// Данные одного вида оружия. Сам по себе не участвует в сети —
/// по сети передаётся только WeaponId (int), а этот ассет
/// на клиентах находится через WeaponDatabase.
/// </summary>
[CreateAssetMenu(fileName = "Weapon_", menuName = "IAPS Shooter/Weapon Definition")]
public class WeaponDefinition : ScriptableObject
{
    [Header("Идентификация")]
    [Tooltip("Уникальный id. Не должен повторяться у разных WeaponDefinition — именно это число хранится в инвентаре и идёт по сети.")]
    [SerializeField] private int weaponId;
    [SerializeField] private string weaponName = "Новое оружие";

    [Header("UI")]
    [SerializeField] private Sprite icon;

    [Header("Стрельба")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float fireRate = 0.15f;
    [SerializeField] private int damage = 25;

    [Header("Визуал в руках")]
    [Tooltip("Модель, которая появится в WeaponSocket игрока при экипировке этого оружия.")]
    [SerializeField] private GameObject handModelPrefab;

    [Header("Магазин")]
    [Tooltip("Обычная цена в магазине.")]
    [SerializeField] private int price = 0;
    [Tooltip("Если включено, оружие попадает во вкладку \"Скидки\" с ценой ниже обычной.")]
    [SerializeField] private bool hasDiscount = false;
    [Tooltip("Цена со скидкой. Учитывается, только если Has Discount включён.")]
    [SerializeField] private int discountedPrice = 0;

    public int WeaponId => weaponId;
    public string WeaponName => weaponName;
    public Sprite Icon => icon;
    public GameObject BulletPrefab => bulletPrefab;
    public float FireRate => fireRate;
    public int Damage => damage;
    public GameObject HandModelPrefab => handModelPrefab;

    public int Price => price;
    public bool HasDiscount => hasDiscount;
    public int DiscountedPrice => discountedPrice;

    /// <summary>Цена, которую реально нужно заплатить — с учётом скидки, если она есть.</summary>
    public int EffectivePrice => hasDiscount ? discountedPrice : price;

    private void OnValidate()
    {
        if (price < 0) price = 0;
        if (discountedPrice < 0) discountedPrice = 0;

        // Скидка не должна оказаться дороже (или равна) обычной цены —
        // иначе это не скидка, а ошибка в данных.
        if (hasDiscount && discountedPrice >= price)
        {
            discountedPrice = Mathf.Max(0, price - 1);
        }
    }
}