using Proto;
using ThingsEdge.Communication.Core.Device;
using ThingsEdge.Exchange.Connectors;
using ThingsEdge.Exchange.Contracts.Variables;
using ThingsEdge.Exchange.Exceptions;
using ThingsEdge.Exchange.Infrastructure.Actors;

namespace ThingsEdge.Exchange.Actors;

/// <summary>
/// 设备连接器 Actor。
/// </summary>
internal sealed class ConnectorActor(
    IDriverConnectorManager connectorManager,
    Device device) : IActor
{
    private IDriverConnector? _connector;

    public async Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case ConnectorCreateAndConnectMessage:
                // 创建并连接
                _connector = await connectorManager.CreateAndConnectAsync(device).ConfigureAwait(false);

                // 将连接器回传给发送者
                context.Respond(_connector);

                // 等待指定时间后，启用健康检查
                context.ReenterAfter(Task.Delay(5_000, context.CancellationToken), () =>
                {
                    var actor = context.SpawnFor<HealthCheckActor>(
                        props => props.WithChildSupervisorStrategy(new OneForOneStrategy((_, reason) =>
                            {
                                return reason switch
                                {
                                    TimeoutExeption => SupervisorDirective.Resume,
                                    _ => SupervisorDirective.Stop,
                                };
                            },
                            3,
                            TimeSpan.FromSeconds(30))), // 当子 Actor 崩溃时，在30秒内最多重启3次
                        [_connector]);
                    context.Send(actor, new HealthCheckMessage());
                });

                break;

            case Stopping:
                // 收到停止指令，先将连接标记为 "终止" 状态 
                if (_connector is { ConnectedStatus: not ConnectionStatus.Aborted })
                {
                    _connector.ConnectedStatus = ConnectionStatus.Aborted;
                }

                break;

            case Stopped:
                // 待子 Actor 停止后，关闭连接
                if (_connector is { Driver: DeviceTcpNet networkDevice })
                {
                    networkDevice.Close(); // 关闭连接
                    connectorManager.Remove(_connector.Id); // 移除连接
                }

                break;
        }
    }
}

/// <summary>
/// 创建并连接设备消息。
/// </summary>
public sealed record ConnectorCreateAndConnectMessage;
