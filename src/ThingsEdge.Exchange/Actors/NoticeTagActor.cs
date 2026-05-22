using Proto;
using ThingsEdge.Exchange.Configuration;
using ThingsEdge.Exchange.Connectors;
using ThingsEdge.Exchange.Contracts.Variables;
using ThingsEdge.Exchange.Infrastructure.Actors;
using ThingsEdge.Exchange.Messages;
using ThingsEdge.Exchange.Utils;

namespace ThingsEdge.Exchange.Actors;

/// <summary>
/// 监控通知的 Actor。
/// </summary>
internal sealed class NoticeTagActor(
    string channelName,
    Device device,
    SignalTag signalTag,
    IDriverConnector connector,
    IOptions<ExchangeOptions> options,
    ILogger<NoticeTagActor> logger) : IActor
{
    private object? _latestData;
    private readonly CancellationTokenSource _cts = new();

    public async Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case Started:
                context.Send(context.Self, new NoticeTagPollTickMessage());
                break;

            case NoticeTagPollTickMessage:
                if (_cts.IsCancellationRequested)
                {
                    return;
                }

                if (!connector.CanConnect)
                {
                    MoveNext();
                    return;
                }

                // 若读取失败，该信号点不会复位，下次会继续读取执行。
                var (ok, data, err) = await connector.ReadAsync(signalTag).ConfigureAwait(false);
                if (!ok)
                {
                    logger.LogError("[NoticeActor] Notice 数据读取异常，设备：{DeviceName}，标记：{TagName}, 地址：{TagAddress}，错误：{Err}",
                        device.Name, signalTag.Name, signalTag.Address, err);

                    MoveNext();
                    return;
                }

                // 是否需要发布消息
                var shouldPublishMessage = signalTag.PublishMode switch
                {
                    PublishMode.EveryScan => true,
                    PublishMode.OnlyDataChanged => _latestData is null || !ObjectComparator.IsEqual(data!.Value, _latestData), // 检查是否有跳变（首次也会发送数据）
                    _ => false,
                };

                _latestData = data!.Value; // 重置为最新的数据

                // 发布通知事件
                if (shouldPublishMessage)
                {
                    context.Publish(new NoticeMessage(connector, channelName, device, signalTag, data!));
                }

                // 执行下一次任务
                MoveNext();

                break;

            case Stopping:
                _cts.Cancel();

                break;
        }

        // 执行下一次操作
        void MoveNext()
        {
            if (!_cts.IsCancellationRequested)
            {
                var delayMilliseconds = signalTag.ScanRate > 0 ? signalTag.ScanRate : options.Value.DefaultScanRate;
                context.ReenterAfter(
                    Task.Delay(delayMilliseconds, _cts.Token),
                    task =>
                    {
                        if (task.IsCompletedSuccessfully && !_cts.IsCancellationRequested)
                        {
                            context.Send(context.Self, new NoticeTagPollTickMessage());
                        }
                    });
            }
        }
    }
}

/// <summary>
/// Notice 节点轮询消息。
/// </summary>
public sealed record NoticeTagPollTickMessage;
