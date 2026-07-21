using System.ComponentModel.DataAnnotations;
using Marketplace.Web.Modules.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Marketplace.Web.Pages.Account;

[Authorize]
public sealed class ProfileModel(UserManager<ApplicationUser> userManager, MarketplaceDbContext db, IWebHostEnvironment environment) : PageModel
{
    public sealed class ProfileInput
    {
        [Required(ErrorMessage = "Укажите имя."), StringLength(80, MinimumLength = 2, ErrorMessage = "Имя должно содержать от 2 до 80 символов.")] public string DisplayName { get; set; } = string.Empty;
        [Required(ErrorMessage = "Укажите полный адрес."), StringLength(500, ErrorMessage = "Адрес слишком длинный.")] public string Address { get; set; } = string.Empty;
        [Range(41, 82, ErrorMessage = "Выберите российский адрес из подсказок.")] public decimal? CityLatitude { get; set; }
        [Range(19, 191, ErrorMessage = "Выберите российский адрес из подсказок.")] public decimal? CityLongitude { get; set; }
        [StringLength(800)] public string? Bio { get; set; }
        public string? AvatarCropData { get; set; }
        public bool ShowPhone { get; set; }
        public bool ShowEmail { get; set; }
        public bool AllowMessages { get; set; }
        public bool ShowExactAddress { get; set; }
        public bool ShowOnlineStatus { get; set; }
        public bool UseHistoryForRecommendations { get; set; }
        public bool EmailNotifications { get; set; }
    }

    public sealed record PrivacySetting(string Name, string Label, string Description, bool Checked);
    [BindProperty] public ProfileInput Input { get; set; } = new();
    [TempData] public string? StatusMessage { get; set; }
    public string Email { get; private set; } = string.Empty;
    public string Initial { get; private set; } = "М";
    public DateTimeOffset RegisteredAt { get; private set; }
    public string? AvatarUrl { get; private set; }
    public IReadOnlyList<PrivacySetting> PrivacySettings { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        MapFromUser(user);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        if ((Input.CityLatitude is null) != (Input.CityLongitude is null)) ModelState.AddModelError("Input.Address", "Выберите адрес из подсказок.");
        if (!string.Equals(user.Address ?? user.City, Input.Address.Trim(), StringComparison.OrdinalIgnoreCase) && Input.CityLatitude is null) ModelState.AddModelError("Input.Address", "Выберите полный адрес из подсказок по всей России.");
        if (!ModelState.IsValid) { MapMetadata(user); BuildPrivacySettings(); return Page(); }
        user.DisplayName = Input.DisplayName.Trim(); user.Address = Input.Address.Trim(); user.City = ExtractCity(Input.Address, user.City); user.CityLatitude = Input.CityLatitude; user.CityLongitude = Input.CityLongitude; user.Bio = Input.Bio?.Trim();
        user.ShowPhone = Input.ShowPhone; user.ShowEmail = Input.ShowEmail; user.AllowMessages = Input.AllowMessages;
        user.ShowExactAddress = Input.ShowExactAddress; user.ShowOnlineStatus = Input.ShowOnlineStatus;
        user.UseHistoryForRecommendations = Input.UseHistoryForRecommendations; user.EmailNotifications = Input.EmailNotifications;
        if (!string.IsNullOrWhiteSpace(Input.AvatarCropData))
        {
            const string prefix = "data:image/jpeg;base64,";
            if (!Input.AvatarCropData.StartsWith(prefix, StringComparison.Ordinal)) ModelState.AddModelError("Input.AvatarCropData", "Не удалось обработать изображение.");
            else
            {
                try
                {
                    var bytes = Convert.FromBase64String(Input.AvatarCropData[prefix.Length..]);
                    if (bytes.Length is < 4 or > 800_000 || bytes[0] != 0xFF || bytes[1] != 0xD8 || bytes[2] != 0xFF) ModelState.AddModelError("Input.AvatarCropData", "Изображение слишком большое или повреждено.");
                    else
                    {
                        var directory = Path.Combine(environment.WebRootPath, "uploads", "avatars"); Directory.CreateDirectory(directory);
                        var fileName = $"{user.Id:N}.jpg"; var target = Path.Combine(directory, fileName); var temporary = target + ".uploading";
                        await System.IO.File.WriteAllBytesAsync(temporary, bytes, HttpContext.RequestAborted); System.IO.File.Move(temporary, target, true); user.AvatarUrl = $"/uploads/avatars/{fileName}?v={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
                    }
                }
                catch (FormatException) { ModelState.AddModelError("Input.AvatarCropData", "Не удалось обработать изображение."); }
            }
            if (!ModelState.IsValid) { MapMetadata(user); BuildPrivacySettings(); return Page(); }
        }
        await userManager.UpdateAsync(user);
        db.AuditEvents.Add(new AuditEvent { UserId = user.Id, EventType = "profile.updated", Detail = "Обновлены публичные данные или настройки приватности.", IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown" });
        await db.SaveChangesAsync();
        StatusMessage = "Профиль и настройки приватности сохранены.";
        return RedirectToPage();
    }

    private void MapFromUser(ApplicationUser user)
    {
        Input = new ProfileInput { DisplayName = user.DisplayName, Address = user.Address ?? user.City, CityLatitude = user.CityLatitude, CityLongitude = user.CityLongitude, Bio = user.Bio, ShowPhone = user.ShowPhone, ShowEmail = user.ShowEmail, AllowMessages = user.AllowMessages, ShowExactAddress = user.ShowExactAddress, ShowOnlineStatus = user.ShowOnlineStatus, UseHistoryForRecommendations = user.UseHistoryForRecommendations, EmailNotifications = user.EmailNotifications };
        MapMetadata(user); BuildPrivacySettings();
    }

    private void MapMetadata(ApplicationUser user)
    {
        Email = user.PhoneNumberConfirmed && !string.IsNullOrWhiteSpace(user.PhoneNumber) ? FormatPhone(user.PhoneNumber) : user.Email ?? string.Empty; RegisteredAt = user.RegisteredAt; AvatarUrl = user.AvatarUrl;
        Initial = string.IsNullOrWhiteSpace(Input.DisplayName) ? "М" : Input.DisplayName[..1].ToUpperInvariant();
    }

    private void BuildPrivacySettings() => PrivacySettings =
    [
        new(nameof(Input.ShowPhone), "Показывать телефон", "Только в активных объявлениях", Input.ShowPhone),
        new(nameof(Input.ShowEmail), "Показывать email", "В публичном профиле", Input.ShowEmail),
        new(nameof(Input.AllowMessages), "Разрешать сообщения", "Другие пользователи смогут начать чат", Input.AllowMessages),
        new(nameof(Input.ShowExactAddress), "Показывать точный адрес", "По умолчанию показывается примерный район", Input.ShowExactAddress),
        new(nameof(Input.ShowOnlineStatus), "Показывать активность", "Статус онлайн и время посещения", Input.ShowOnlineStatus),
        new(nameof(Input.UseHistoryForRecommendations), "Персональные рекомендации", "Использовать историю просмотров", Input.UseHistoryForRecommendations),
        new(nameof(Input.EmailNotifications), "Email-уведомления", "Важные события аккаунта и объявлений", Input.EmailNotifications)
    ];

    private static string FormatPhone(string phone) => phone.Length == 12 && phone.StartsWith("+7", StringComparison.Ordinal) ? $"+7 ({phone[2..5]}) {phone[5..8]}-{phone[8..10]}-{phone[10..12]}" : phone;

    private static string ExtractCity(string address, string fallback)
    {
        var excludedEndings = new[] { "область", "край", "республика", "район", "округ", "улица", "переулок", "проспект", "бульвар", "шоссе" };
        return address.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Reverse()
            .FirstOrDefault(part => part.Length >= 2 && !part.Equals("Россия", StringComparison.OrdinalIgnoreCase) && !part.All(character => char.IsDigit(character) || char.IsWhiteSpace(character)) && !excludedEndings.Any(ending => part.EndsWith(ending, StringComparison.OrdinalIgnoreCase)))
            ?? fallback;
    }
}
