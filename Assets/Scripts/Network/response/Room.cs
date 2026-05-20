using UnityEngine;

[System.Serializable]
public class Room
{
    public string name;
    /// <summary>客户端应连端口：WebGL/WSS 多为 17777–17779；无 nginx 时可为游戏口 7777 等。</summary>
    public int port;
    /// <summary>空则用 <see cref="NetworkManagerUI.DeployHttpsHost"/>。</summary>
    public string host;
    /// <summary>接口可带，便于日志；连接行为以 host+port 为准。</summary>
    public bool secure;
    /// <summary>退房 <c>/fps/remove_room/</c> 上报用：无则退回 <see cref="port"/>。</summary>
    public int internal_port;
    public string url;
}
