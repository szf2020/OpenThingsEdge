using ThingsEdge.Exchange.Contracts.Variables;

namespace ThingsEdge.Exchange.Connectors;

/// <summary>
/// 设备驱动连接管理器。
/// </summary>
internal interface IDriverConnectorManager
{
    /// <summary>
    /// 创建连接器并尝试连接设备
    /// </summary>
    /// <param name="deviceInfo">要连接的设备信息</param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    /// 返回驱动连接器，更新 ConnectedStatus 状态判断是否有连接。
    /// </returns>
    Task<IDriverConnector> CreateAndConnectAsync(Device deviceInfo, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定的连接驱动。
    /// </summary>
    /// <param name="id">连接Id，与设备Id一致</param>
    /// <returns></returns>
    IDriverConnector? GetConnector(string id);

    /// <summary>
    /// 移除指定的连接驱动。
    /// </summary>
    /// <param name="id">连接Id，与设备Id一致</param>
    void Remove(string id);
}
