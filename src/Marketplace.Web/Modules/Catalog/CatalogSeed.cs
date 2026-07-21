using System.Security.Cryptography;
using System.Text;
using Marketplace.Web.Modules.Identity;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Modules.Catalog;

public static class CatalogSeed
{
    private sealed record Node(string Slug, string Name, string? ParentSlug = null, string? Description = null);
    private sealed record AttributeSeed(string Code, string Name, string Type, bool Required = false, string? Unit = null, string? Options = null);

    public static async Task InitializeAsync(MarketplaceDbContext db)
    {
        if (await db.CatalogCategories.AnyAsync()) return;

        var roots = new[]
        {
            "Транспорт", "Недвижимость", "Электроника", "Дом, мебель и интерьер", "Строительство и ремонт", "Сад и дача", "Одежда, обувь и аксессуары", "Детские товары", "Хобби, спорт и развлечения", "Животные", "Работа", "Услуги", "Для бизнеса", "Обмен и бесплатно", "Бюро находок"
        };
        var nodes = roots.Select((name, index) => new Node(Slugify(name), name, null, $"Раздел {name.ToLowerInvariant()}")).ToList();
        nodes.AddRange(
        [
            new("legkovye-avtomobili", "Легковые автомобили", "transport"), new("sedany", "Седаны", "legkovye-avtomobili"), new("krossovery", "Кроссоверы", "legkovye-avtomobili"), new("zapchasti", "Запчасти", "transport"),
            new("kvartiry", "Квартиры", "nedvizhimost"), new("prodazha-kvartir", "Продажа квартир", "kvartiry"), new("arenda-kvartir", "Долгосрочная аренда", "kvartiry"), new("doma", "Дома и коттеджи", "nedvizhimost"), new("uchastki", "Участки", "nedvizhimost"),
            new("noutbuki", "Ноутбуки", "elektronika"), new("igrovye-noutbuki", "Игровые ноутбуки", "noutbuki"), new("smartfony", "Смартфоны", "elektronika"), new("bytovaya-tehnika", "Бытовая техника", "elektronika"),
            new("myagkaya-mebel", "Мягкая мебель", "dom-mebel-i-interer"), new("divany", "Диваны", "myagkaya-mebel"), new("stoly-i-stulya", "Столы и стулья", "dom-mebel-i-interer"), new("krovati-i-matrasy", "Кровати и матрасы", "dom-mebel-i-interer")
        ]);

        var categories = nodes.Select((node, index) => new CatalogCategory
        {
            Id = Id($"category:{node.Slug}"), ParentId = node.ParentSlug is null ? null : Id($"category:{node.ParentSlug}"),
            Slug = node.Slug, Name = node.Name, Description = node.Description, SortOrder = index
        }).ToList();
        db.CatalogCategories.AddRange(categories);

        foreach (var schema in new[]
        {
            ("sedany", CarAttributes()), ("krossovery", CarAttributes()), ("prodazha-kvartir", ApartmentAttributes()), ("arenda-kvartir", ApartmentAttributes()),
            ("igrovye-noutbuki", LaptopAttributes()), ("smartfony", PhoneAttributes()), ("divany", FurnitureAttributes()), ("stoly-i-stulya", FurnitureAttributes())
        })
        {
            var version = new CategorySchemaVersion { Id = Id($"schema:{schema.Item1}:1"), CategoryId = Id($"category:{schema.Item1}"), Version = 1, IsPublished = true };
            db.CategorySchemaVersions.Add(version);
            db.CategoryAttributes.AddRange(schema.Item2.Select((item, index) => new CategoryAttribute
            {
                Id = Id($"attribute:{schema.Item1}:{item.Code}"), SchemaVersionId = version.Id, Code = item.Code, Name = item.Name, DataType = item.Type, IsRequired = item.Required, Unit = item.Unit, OptionsJson = item.Options, IsFilterable = true, IsComparable = true, SortOrder = index
            }));
        }
        await db.SaveChangesAsync();
    }

    private static IEnumerable<AttributeSeed> CarAttributes() => [new("brand", "Марка", "select", true, null, "[\"BMW\",\"Lada\",\"Toyota\",\"Volkswagen\"]"), new("model", "Модель", "string", true), new("year", "Год выпуска", "integer", true), new("mileage", "Пробег", "integer", false, "км"), new("engine", "Двигатель", "select", false, null, "[\"Бензин\",\"Дизель\",\"Гибрид\",\"Электро\"]"), new("transmission", "Коробка передач", "select", false, null, "[\"Механика\",\"Автомат\",\"Вариатор\"]")];
    private static IEnumerable<AttributeSeed> ApartmentAttributes() => [new("rooms", "Количество комнат", "select", true, null, "[\"Студия\",\"1\",\"2\",\"3\",\"4+\"]"), new("area", "Площадь", "decimal", true, "м²"), new("floor", "Этаж", "integer", false), new("floors", "Этажей в доме", "integer", false), new("renovation", "Ремонт", "select", false, null, "[\"Без ремонта\",\"Косметический\",\"Евроремонт\",\"Дизайнерский\"]")];
    private static IEnumerable<AttributeSeed> LaptopAttributes() => [new("brand", "Бренд", "select", true, null, "[\"Apple\",\"ASUS\",\"Lenovo\",\"MSI\"]"), new("processor", "Процессор", "string", true), new("ram", "Оперативная память", "integer", true, "ГБ"), new("storage", "Накопитель", "integer", false, "ГБ"), new("screen", "Диагональ", "decimal", false, "дюйм"), new("condition", "Состояние", "select", true, null, "[\"Новое\",\"Отличное\",\"Хорошее\"]")];
    private static IEnumerable<AttributeSeed> PhoneAttributes() => [new("brand", "Бренд", "select", true, null, "[\"Apple\",\"Samsung\",\"Xiaomi\",\"Google\"]"), new("model", "Модель", "string", true), new("storage", "Память", "integer", false, "ГБ"), new("condition", "Состояние", "select", true, null, "[\"Новое\",\"Отличное\",\"Хорошее\"]")];
    private static IEnumerable<AttributeSeed> FurnitureAttributes() => [new("width", "Ширина", "integer", false, "см"), new("height", "Высота", "integer", false, "см"), new("depth", "Глубина", "integer", false, "см"), new("material", "Материал", "select", false, null, "[\"Дерево\",\"Металл\",\"Ткань\",\"Кожа\"]"), new("color", "Цвет", "string", false), new("condition", "Состояние", "select", true, null, "[\"Новое\",\"Отличное\",\"Хорошее\"]")];
    private static Guid Id(string key) => new(MD5.HashData(Encoding.UTF8.GetBytes(key)));
    private static string Slugify(string value) => value switch
    {
        "Транспорт" => "transport", "Недвижимость" => "nedvizhimost", "Электроника" => "elektronika", "Дом, мебель и интерьер" => "dom-mebel-i-interer", "Строительство и ремонт" => "stroitelstvo-i-remont", "Сад и дача" => "sad-i-dacha", "Одежда, обувь и аксессуары" => "odezhda-i-obuv", "Детские товары" => "detskie-tovary", "Хобби, спорт и развлечения" => "hobbi-i-sport", "Животные" => "zhivotnye", "Работа" => "rabota", "Услуги" => "uslugi", "Для бизнеса" => "dlya-biznesa", "Обмен и бесплатно" => "obmen-i-besplatno", "Бюро находок" => "byuro-nahodok", _ => "other"
    };
}
