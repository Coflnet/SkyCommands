using System;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands;

/// <summary>
/// Minimal view of a websocket connection needed by <see cref="LiveUpdateService"/> (seam for tests).
/// </summary>
public interface ILiveUpdateConnection
{
    /// <summary>Sends a message to the client.</summary>
    void SendBack(MessageData data);

    /// <summary>Registers a callback that is invoked once before the connection closes.</summary>
    void RegisterCloseHandler(Action onClose);
}
