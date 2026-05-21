using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Singleton;

    [SerializeField]
    public MatchingSettings MatchingSettings;

    [Tooltip("关闭后不再用 OnGUI 画左上角红字全员血量（正式包建议关）。需要调试时再勾选。")]
    [SerializeField]
    private bool showDebugPlayerListInGui = false;

    private Dictionary<string, Player> players = new Dictionary<string, Player>();

    private void Awake()
    {
        Singleton = this;
    }

    public void RegisterPlayer(string name, Player player)
    {
        player.transform.name = name;
        players[name] = player;
    }

    public void UnRegisterPlayer(string name)
    {
        players.Remove(name);
    }

    public Player GetPlayer(string name)
    {
        Player player;
        players.TryGetValue(name, out player);
        return player;
    }

    private void OnGUI()
    {
        if (!showDebugPlayerListInGui) return;

        GUILayout.BeginArea(new Rect(200f, 200f, 200f, 400f));
        GUILayout.BeginVertical();

        GUI.color = Color.red;
        foreach (string name in players.Keys)
        {
            Player player = GetPlayer(name);
            GUILayout.Label(name + " - " + player.GetHealth());
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}
