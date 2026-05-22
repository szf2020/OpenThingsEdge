using Proto;
using Proto.DependencyInjection;
using Proto.Mailbox;
using ThingsEdge.Exchange.Actors;
using ThingsEdge.Exchange.Handlers;
using ThingsEdge.Exchange.Messages;

namespace ThingsEdge.Exchange;

internal sealed class EngineExchange(
    ActorSystem actorSystem,
    HeartbeatMessageHandler heartbeatMessageHandler,
    NoticeMessageHandler noticeMessageHandler,
    TriggerMessageHandler triggerMessageHandler,
    SwitchMessageHandler switchMessageHandler,
    ILogger<EngineExchange> logger) : IExchange
{
    private PID? _sandboxActor;
    private readonly List<Guid> _subscribes = [];

    public bool IsRunning { get; private set; }

    public Task StartAsync()
    {
        if (IsRunning)
        {
            return Task.CompletedTask;
        }

        IsRunning = true;

        logger.LogInformation("[EngineExchange] 引擎启动");

        // 注册订阅消息
        RegisterMessageHandlers();

        // 创建 Actor
        _sandboxActor = actorSystem.Root.Spawn(actorSystem.DI().PropsFor<EngineRootActor>([]));
        actorSystem.Root.Send(_sandboxActor, new EngineStartMessage());

        return Task.CompletedTask;
    }

    private void RegisterMessageHandlers()
    {
        var heartbeatSubscribe = actorSystem.EventStream.Subscribe<HeartbeatMessage>(
            async message =>
            {
                await heartbeatMessageHandler.HandleAsync(message).ConfigureAwait(false);
            },
            Dispatchers.DefaultDispatcher);
        _subscribes.Add(heartbeatSubscribe.Id);

        var noticeSubscribe = actorSystem.EventStream.Subscribe<NoticeMessage>(
            async message =>
            {
                await noticeMessageHandler.HandleAsync(message).ConfigureAwait(false);
            },
            Dispatchers.DefaultDispatcher);
        _subscribes.Add(noticeSubscribe.Id);

        var triggerSubscribe = actorSystem.EventStream.Subscribe<TriggerMessage>(
            async message =>
            {
                await triggerMessageHandler.HandleAsync(message).ConfigureAwait(false);
            },
            Dispatchers.DefaultDispatcher);
        _subscribes.Add(triggerSubscribe.Id);

        var switchSubscribe = actorSystem.EventStream.Subscribe<SwitchMessage>(
            async message =>
            {
                await switchMessageHandler.HandleAsync(message).ConfigureAwait(false);
            },
            Dispatchers.DefaultDispatcher);
        _subscribes.Add(switchSubscribe.Id);
    }

    public async Task ShutdownAsync()
    {
        if (!IsRunning)
        {
            return;
        }

        IsRunning = false;

        // 停止 Actor 运行
        if (_sandboxActor != null)
        {
            await actorSystem.Root.StopAsync(_sandboxActor).ConfigureAwait(false);
        }

        // 取消订阅
        foreach (var subscribe in _subscribes)
        {
            actorSystem.EventStream.Unsubscribe(subscribe);
        }

        // 清空订阅
        _subscribes.Clear();

        logger.LogInformation("[EngineExchange] 引擎已停止");
    }

    public async ValueTask DisposeAsync()
    {
        await ShutdownAsync().ConfigureAwait(false);
    }
}
