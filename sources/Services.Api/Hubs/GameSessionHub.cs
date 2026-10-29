using System.Security.Claims;
using Logic.Sessions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Services.Api.Authorization;
using Shared.Models.Realtime;

namespace Services.Api.Hubs;

/// <summary>
/// Real-time channel for game sessions. Clients subscribe per session; only participants are added to
/// the session group, so nobody receives events of games they do not take part in.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.Player)]
public sealed class GameSessionHub(SessionLifecycleLogic sessions) : Hub
{
    public static string GroupName(Guid sessionId) => $"session:{sessionId:N}";

    [HubMethodName(GameHubMethods.JoinSession)]
    public async Task<bool> JoinSession(Guid sessionId)
    {
        if (!Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return false;
        }

        var access = await sessions.GetAsync(userId, sessionId, Context.ConnectionAborted);
        if (access is not SessionLifecycleOutcome.Success)
        {
            return false;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(sessionId), Context.ConnectionAborted);
        return true;
    }

    [HubMethodName(GameHubMethods.LeaveSession)]
    public Task LeaveSession(Guid sessionId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(sessionId), Context.ConnectionAborted);
}
