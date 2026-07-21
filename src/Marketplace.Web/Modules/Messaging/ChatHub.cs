using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Marketplace.Web.Modules.Messaging;

[Authorize]
public sealed class ChatHub(MessagingService messaging) : Hub
{
    private static readonly ConcurrentDictionary<Guid, int> Connections = new();
    private Guid UserId => Guid.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static string Group(Guid id) => $"conversation:{id}";

    public override async Task OnConnectedAsync()
    {
        Connections.AddOrUpdate(UserId, 1, (_, count) => count + 1);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        Connections.AddOrUpdate(UserId, 0, (_, count) => Math.Max(0, count - 1));
        ((ICollection<KeyValuePair<Guid, int>>)Connections).Remove(new(UserId, 0));
        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinConversation(Guid conversationId)
    {
        try
        {
            await messaging.MarkReadAsync(conversationId, UserId, Context.ConnectionAborted);
            await Groups.AddToGroupAsync(Context.ConnectionId, Group(conversationId), Context.ConnectionAborted);
            await Clients.Group(Group(conversationId)).SendAsync("PresenceChanged", UserId, Connections.GetValueOrDefault(UserId) > 0, Context.ConnectionAborted);
            await Clients.Group(Group(conversationId)).SendAsync("ReadReceipt", conversationId, UserId, Context.ConnectionAborted);
        }
        catch (OperationCanceledException) when (Context.ConnectionAborted.IsCancellationRequested)
        {
            // Navigating away can abort the socket while the read-state query is in flight.
        }
    }

    public async Task SendMessage(Guid conversationId, string text)
    {
        try
        {
            var message = await messaging.SendAsync(conversationId, UserId, text, cancellationToken: Context.ConnectionAborted);
            await Clients.Group(Group(conversationId)).SendAsync("ReceiveMessage", message, Context.ConnectionAborted);
        }
        catch (OperationCanceledException) when (Context.ConnectionAborted.IsCancellationRequested)
        {
            // A disconnected caller cannot receive the invocation result.
        }
    }

    public Task Typing(Guid conversationId, bool isTyping) => Clients.OthersInGroup(Group(conversationId)).SendAsync("TypingChanged", UserId, isTyping);
    public async Task MarkRead(Guid conversationId)
    {
        try
        {
            await messaging.MarkReadAsync(conversationId, UserId, Context.ConnectionAborted);
            await Clients.OthersInGroup(Group(conversationId)).SendAsync("ReadReceipt", conversationId, UserId, Context.ConnectionAborted);
        }
        catch (OperationCanceledException) when (Context.ConnectionAborted.IsCancellationRequested)
        {
            // Closing the page during read receipt persistence is an expected cancellation.
        }
    }
}
