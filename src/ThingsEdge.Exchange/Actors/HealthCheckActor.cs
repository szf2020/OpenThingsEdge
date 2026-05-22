using Proto;
using ThingsEdge.Communication.Core.Device;
using ThingsEdge.Exchange.Connectors;

namespace ThingsEdge.Exchange.Actors;

/// <summary>
/// 设备连接器健康检查 Actor。
/// </summary>
internal sealed class HealthCheckActor(IDriverConnector connector, ILogger<HealthCheckActor> logger) : IActor
{
    private readonly CancellationTokenSource _cts = new();

    public async Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case HealthCheckMessage:
                if (_cts.IsCancellationRequested)
                {
                    return;
                }

                if (connector is { ConnectedStatus: not ConnectionStatus.Aborted, Driver: DeviceTcpNet networkDevice })
                {
                    connector.Available = await networkDevice.PingSuccessfulAsync(1_000).ConfigureAwait(false);
                    if (connector.Available && connector.ConnectedStatus is ConnectionStatus.Disconnected)
                    {
                        // 内部 Socket 异常，或是第一次尝试连接过服务器失败
                        if (networkDevice.IsSocketError)
                        {
                            var result = await networkDevice.ConnectServerAsync().ConfigureAwait(false);
                            if (result.IsSuccess)
                            {
                                connector.ConnectedStatus = ConnectionStatus.Connected;

                                if (logger.IsEnabled(LogLevel.Information))
                                {
                                    logger.LogInformation("已连接上服务，主机：{Host}", connector.Host);
                                }
                            }
                        }
                    }

                    // 执行下一次任务
                    if (!_cts.IsCancellationRequested)
                    {
                        context.ReenterAfter(
                            Task.Delay(6_000, _cts.Token),
                            task =>
                            {
                                if (task.IsCompletedSuccessfully && !_cts.IsCancellationRequested)
                                {
                                    context.Send(context.Self, new HealthCheckMessage());
                                }
                            });
                    }
                }

                break;

            case Stopping _:
                _cts.Cancel();

                break;
        }
    }
}

/// <summary>
/// 设备连接健康检查消息。
/// </summary>
public sealed record HealthCheckMessage;
