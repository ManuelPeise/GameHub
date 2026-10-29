using Data.Database;
using Data.Database.Entities;
using Data.DataAccessor.Audit;
using Data.DataAccessor.Mail;
using Logic.Shared.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Shared.Models.Account;

namespace Data.DataAccessor.Users;

public static class UserAccountsRegistration
{
    /// <summary>
    /// Registers ASP.NET Core Identity on the GameDbContext and the adapters for user accounts,
    /// audit log and e-mail. Requires <c>IAppSettings</c> and <c>AddGameDatabase</c>.
    /// </summary>
    public static IHostApplicationBuilder AddUserAccounts(this IHostApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton(TimeProvider.System);

        // Protects the e-mail confirmation tokens. Keys must be persisted in Docker (US-029).
        builder.Services.AddDataProtection().SetApplicationName("GameHub");

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
            })
            .AddRoles<Role>()
            .AddEntityFrameworkStores<GameDbContext>()
            .AddDefaultTokenProviders();

        builder.Services.AddScoped<IUserAccountStore, UserAccountStore>();
        builder.Services.AddScoped<IAuditLog, AuditLog>();
        builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

        return builder;
    }
}
