using Marketplace.Web.Modules.Listings;

namespace Marketplace.Web.Modules.Moderation;

public static class DemoModerationEngine
{
    public static ModerationCase Evaluate(Listing listing)
    {
        var text = $"{listing.Title} {listing.Description}".ToLowerInvariant();
        var moderationCase = new ModerationCase { Id = Guid.NewGuid(), ListingId = listing.Id, RuleSetVersion = "demo-1.0" };
        AddFinding(moderationCase, text, ["оружие", "наркотик", "поддельный документ"], "PROHIBITED_GOODS", "Critical", "Карантин и эскалация старшему модератору.");
        AddFinding(moderationCase, text, ["http://", "https://", "t.me/"], "EXTERNAL_LINK", "Medium", "Запросить удаление внешней ссылки.");
        AddFinding(moderationCase, text, ["пишите в whatsapp", "номер телефона"], "CONTACT_BYPASS", "Medium", "Запросить перенос контактов в разрешённые поля.");
        if (listing.DealType == "Free" && text.Any(char.IsDigit)) Add(moderationCase, "HIDDEN_PRICE", "Medium", "числовая конструкция в бесплатном объявлении", "Запросить проверку скрытой цены.");
        moderationCase.RiskLevel = moderationCase.Findings.Any(x => x.RiskCategory == "Critical") ? "Critical" : moderationCase.Findings.Any(x => x.RiskCategory == "High") ? "High" : moderationCase.Findings.Any() ? "Medium" : "Low";
        listing.Status = moderationCase.RiskLevel == "Critical" ? "Quarantine" : "PendingManualReview";
        return moderationCase;
    }

    private static void AddFinding(ModerationCase target, string text, string[] needles, string code, string risk, string action) { var found = needles.FirstOrDefault(text.Contains); if (found is not null) Add(target, code, risk, found, action); }
    private static void Add(ModerationCase target, string code, string risk, string fragment, string action) => target.Findings.Add(new ModerationFinding { Id = Guid.NewGuid(), Code = code, RiskCategory = risk, Confidence = .98m, Fragment = fragment, RuleCode = $"DEMO_{code}", RuleVersion = "1.0", RecommendedAction = action, Evidence = "Совпадение demo-rule в тексте объявления." });
}
