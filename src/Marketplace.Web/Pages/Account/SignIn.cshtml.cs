using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Marketplace.Web.Modules.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Marketplace.Web.Modules.Observability;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Pages.Account;

[EnableRateLimiting("auth")]
public sealed class SignInModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IOneTimeCodeService codeService, MarketplaceDbContext db, IConfiguration configuration, IWebHostEnvironment environment, RuntimeMetrics metrics, ILogger<SignInModel> logger) : PageModel
{
    [BindProperty, Required(ErrorMessage = "Введите email или российский номер телефона."), StringLength(254)] public string Email { get; set; } = string.Empty;
    [BindProperty, RegularExpression("^[0-9]{6}$", ErrorMessage = "Введите 6 цифр.")] public string Code { get; set; } = string.Empty;
    [BindProperty, StringLength(80)] public string? InviteCode { get; set; }
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    public bool CodeSent { get; private set; }
    public string? DemoCode { get; private set; }
    public string? StatusMessage { get; private set; }
    public bool HasError { get; private set; }
    public bool RegistrationRestricted => !configuration.GetValue("Beta:RegistrationOpen", true);
    public bool ShowDemoAdministrator => environment.IsDevelopment() && !string.IsNullOrWhiteSpace(configuration["Identity:DemoAdminEmail"]);
    public string DemoAdminEmail => configuration["Identity:DemoAdminEmail"] ?? string.Empty;
    public bool ShowFakeProviderDetails => environment.IsDevelopment() && configuration["Identity:OtpProvider"]?.Equals("Fake", StringComparison.OrdinalIgnoreCase) != false;

    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true ? RedirectToPage("/Account/Profile") : Page();

    public async Task<IActionResult> OnPostSendCodeAsync()
    {
        ModelState.Remove(nameof(Code));
        Email = NormalizeIdentifier(Email);
        ValidateIdentifier();
        if (!ModelState.IsValid) return Page();
        var result = await codeService.SendAsync(Email, HttpContext.RequestAborted);
        metrics.AuthenticationEvent("send", result.Succeeded ? "success" : "failed");
        CodeSent = result.Succeeded;
        DemoCode = ShowFakeProviderDetails ? result.Code : null;
        StatusMessage = result.Message;
        return Page();
    }

    public async Task<IActionResult> OnPostVerifyAsync()
    {
        Email = NormalizeIdentifier(Email);
        ValidateIdentifier();
        CodeSent = true;
        if (!ModelState.IsValid || !await codeService.VerifyAsync(Email, Code, HttpContext.RequestAborted))
        {
            metrics.AuthenticationEvent("verify", "failed");
            logger.LogWarning("OTP verification failed from {RemoteIp}.", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
            HasError = true;
            StatusMessage = "Код неверный, просрочен или превышено число попыток. Запросите новый код.";
            return Page();
        }
        metrics.AuthenticationEvent("verify", "success");
        var phone = NormalizePhone(Email);
        var user = phone is null ? await userManager.FindByEmailAsync(Email) : await db.Users.FirstOrDefaultAsync(x => x.PhoneNumber == phone);
        var isNew = user is null;
        if (user is null)
        {
            if (RegistrationRestricted && !string.Equals(InviteCode?.Trim(), configuration["Beta:InviteCode"], StringComparison.Ordinal)) { HasError = true; StatusMessage = "Закрытая бета: требуется действующий код приглашения."; return Page(); }
            var internalEmail = phone is null ? Email : $"{phone[1..]}@phone.marketplace.local";
            user = new ApplicationUser { Id = Guid.NewGuid(), UserName = phone ?? Email, Email = internalEmail, EmailConfirmed = phone is null, PhoneNumber = phone, PhoneNumberConfirmed = phone is not null, DisplayName = phone ?? Email.Split('@')[0] };
            var created = await userManager.CreateAsync(user);
            if (!created.Succeeded)
            {
                HasError = true;
                StatusMessage = string.Join(" ", created.Errors.Select(x => x.Description));
                return Page();
            }
            var demoAdministrator = ShowDemoAdministrator && Email.Equals(configuration["Identity:DemoAdminEmail"], StringComparison.OrdinalIgnoreCase);
            await userManager.AddToRoleAsync(user, demoAdministrator ? "Administrator" : "Member");
        }
        var session = new UserSession
        {
            Id = Guid.NewGuid(), UserId = user.Id,
            Device = DescribeDevice(Request.Headers.UserAgent.ToString()),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
        };
        db.UserSessions.Add(session);
        db.AuditEvents.Add(new AuditEvent
        {
            UserId = user.Id, EventType = isNew ? "account.registered" : "account.signed_in",
            Detail = isNew ? "Аккаунт создан после подтверждения одноразового кода." : "Выполнен вход по одноразовому коду.", IpAddress = session.IpAddress
        });
        await db.SaveChangesAsync();
        await signInManager.SignInWithClaimsAsync(user, true, [new Claim("session_id", session.Id.ToString())]);
        return !string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) ? LocalRedirect(ReturnUrl) : RedirectToPage("/Account/Profile");
    }

    private static string DescribeDevice(string userAgent)
    {
        var browser = userAgent.Contains("Edg/", StringComparison.OrdinalIgnoreCase) ? "Edge" : userAgent.Contains("Chrome/", StringComparison.OrdinalIgnoreCase) ? "Chrome" : userAgent.Contains("Firefox/", StringComparison.OrdinalIgnoreCase) ? "Firefox" : "Браузер";
        var platform = userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase) ? "Windows" : userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase) ? "Android" : userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase) ? "iPhone" : userAgent.Contains("Mac", StringComparison.OrdinalIgnoreCase) ? "macOS" : "неизвестная ОС";
        return $"{browser} · {platform}";
    }

    private void ValidateIdentifier()
    {
        if (NormalizePhone(Email) is not null) return;
        if (!new EmailAddressAttribute().IsValid(Email)) ModelState.AddModelError(nameof(Email), "Введите корректный email или номер +7 (999) 999-99-99.");
    }

    private static string NormalizeIdentifier(string value) => NormalizePhone(value) ?? value.Trim().ToLowerInvariant();
    private static string? NormalizePhone(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length == 11 && digits[0] == '8') digits = "7" + digits[1..];
        return digits.Length == 11 && digits[0] == '7' ? "+" + digits : null;
    }
}
