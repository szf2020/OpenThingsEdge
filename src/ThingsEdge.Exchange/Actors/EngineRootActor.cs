using Proto;
using ThingsEdge.Exchange.Addresses;

namespace ThingsEdge.Exchange.Actors;

/// <summary>
/// 引擎根 Actor。
/// </summary>
/// <param name="addressFactory"></param>
internal sealed class EngineRootActor(IAddressFactory addressFactory) : IActor
{
    private bool _isRunning;

    public Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case EngineStartMessage:
                // 防重入
                if (_isRunning)
                {
                    return Task.CompletedTask;
                }

                _isRunning = true;

                var channels = addressFactory.GetChannels();
                foreach (var channel in channels)
                {
                    var actor = context.Spawn(Props.FromProducer(() => new ChannelActor(channel)));
                    context.Send(actor, new ChannelStartMessage());
                }

                break;
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// 引擎启动消息。
/// </summary>
public sealed record EngineStartMessage;
