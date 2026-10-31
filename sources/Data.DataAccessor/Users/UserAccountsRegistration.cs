using Data.Database;
using Data.Database.Entities;
using Data.DataAccessor.Audit;
using Data.DataAccessor.Mail;
using Logic.Shared.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Shared.Models.Account;
using Shared.Models.Interfaces;

namespace Data.DataAccessor.Users;

public static class UserAccountsRegistration
{
    /// <summary>Name of the authentication cookie.</summary>
    public const string AuthCookieName = "GameHub.Auth";

    /// <summary>
    /// Registers ASP.NET Core Identity on the GameDbContext with cookie sign-in, and the adapters for
    /// user accounts, audit log and e-mail. Requires <c>AddGameDatabase</c>.
    /// </summary>
    public static IHostApplicationBuilder AddUserAccounts(this IHostApplicationBuilder builder, IAppSettings appSettings)
    {
        ArgumentNullException.ThrowIfNull(appSettings);
        var security = appSettings.Security;

        builder.Services.TryAddSingleton(TimeProvider.System);

        // Protects auth cookies and the tokens in e-mail links. In Docker the keys live in a volume (US-029),
        // otherwise every container update would sign out all users and invalidate open e-mail links.
        var dataProtection = builder.Services.AddDataProtection().SetApplicationName("GameHub");
        if (!string.IsNullOrWhiteSpace(security.DataProtectionKeysPath))
        {
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(security.DataProtectionKeysPath));
        }

        builder.Services
            .AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();

        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = AuthCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            // Lax: the cookie is sent when following a link from an e-mail, but not on cross-site POSTs.
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;

            // API and SignalR: answer with status codes instead of redirecting to a login page.
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
            options.TokenLifespan = security.AccountTokenLifetime);

        builder.Services
            .AddIdentityCore<User>(options =>
            {
                options.User.RequireUniqueEmail = true;
                // The user name is the e-mail address; allow every character an address may contain.
                options.User.AllowedUserNameCharacters = string.Empty;

                options.SignIn.RequireConfirmedEmail = true;

                options.Password.RequiredLength = AccountRules.PasswordMinLength;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireDigit = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = security.LockoutMaxFailedAttempts;
                options.Lockout.DefaultLockoutTimeSpan = security.LockoutDuration;
            })
            .AddRoles<Role>()
            .AddEntityFrameworkStores<GameDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        builder.Services.AddScoped<IUserAccountStore, UserAccountStore>();
        builder.Services.AddScoped<IUserAdministrationStore, UserAdministrationStore>();
        builder.Services.AddScoped<IAdminLogStore, Administration.AdminLogStore>();

        // How often a sign-in cookie is checked against the account (security stamp): a locked or deleted
        // player is signed out after at most this time.
        builder.Services.Configure<SecurityStampValidatorOptions>(options =>
            options.ValidationInterval = security.SessionValidationInterval);
        builder.Services.AddScoped<AccountSeeder>();
        builder.Services.AddScoped<IAuditLog, AuditLog>();
        builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

        return builder;
    }
}
