using Logic.Shared;
using Logic.Shared.Interfaces;
using Logic.Shared.Models;
using Shared.Models.Interfaces;

namespace Logic.Users;

/// <summary>Setting up two-factor authentication with an authenticator app (US-007).</summary>
public sealed class TwoFactorLogic(
    IAppSettings appSettings,
    IUserAccountStore accountStore,
    IAuditLog auditLog) : ALogicBase(appSettings)
{
    public Task<TwoFactorSetup?> GetSetupAsync(Guid userId, CancellationToken cancellationToken) =>
        accountStore.GetTwoFactorSetupAsync(userId, cancellationToken);

    /// <summary>Enables two-factor authentication; returns the recovery codes, or null if the code is invalid.</summary>
    public async Task<IReadOnlyList<string>?> EnableAsync(Guid userId, string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var recoveryCodes = await accountStore.EnableTwoFactorAsync(userId, code, cancellationToken);

        await auditLog.WriteAsync(
            new AuditRecord(
                AuditActions.TwoFactorEnabled,
                recoveryCodes is null ? AuditResult.Failure : AuditResult.Success,
                userId),
            cancellationToken);

        return recoveryCodes;
    }
}
