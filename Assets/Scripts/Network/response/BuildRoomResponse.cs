using UnityEngine;

[System.Serializable]
public class BuildRoomResponse
{
    public string error_message;
    public string name;
    /// <summary>客户端连接端口（如 WSS 外壳 17777）。</summary>
    public int port;
    public string host;
    public bool secure;
    /// <summary>登记到退房接口的进程端口（如 7777）；0 表示未区分，退房用 <see cref="port"/>。</summary>
    public int internal_port;
    public string url;
}
