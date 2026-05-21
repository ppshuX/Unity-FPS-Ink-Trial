using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerSetup : NetworkBehaviour
{
    [SerializeField]
    private Behaviour[] componentsToDisable;

    /// <summary>
    /// 菜单 / 场景观察用相机（Hierarchy 里通常名为 SceneCamera）。不要用 Camera.main：
    /// 本地玩家子物体上的 FPS 相机若也带 MainCamera，会抢先成为 Camera.main，误关 FPS 后只剩 SceneCamera，表现为第三人称。
    /// </summary>
    [SerializeField]
    private Camera menuWorldCamera;

    private Camera sceneCamera;

    // Start is called before the first frame update
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsLocalPlayer)
        {
            SetLayerMaskForAllChildren(transform, LayerMask.NameToLayer("Remote Player"));
            DisableComponents();
        } else
        {
            if (PlayerUI.Singleton != null)
            {
                PlayerUI.Singleton.setPlayer(GetComponent<Player>());
            }
            FpsPolishController.AttachToLocalPlayer(gameObject);
            TrialPlayerAbility.Attach(gameObject);
            TrialChallengeDirector.SetLocalPlayer(GetComponent<Player>());
            SetLayerMaskForAllChildren(transform, LayerMask.NameToLayer("Player"));
            sceneCamera = menuWorldCamera;
            if (sceneCamera == null)
            {
                GameObject sceneCamGo = GameObject.Find("SceneCamera");
                if (sceneCamGo != null)
                {
                    sceneCamera = sceneCamGo.GetComponent<Camera>();
                }
            }
            if (sceneCamera != null)
            {
                sceneCamera.gameObject.SetActive(false);
            }
        }

        NetworkObject networkObject = GetComponent<NetworkObject>();
        string name = networkObject != null
            ? "Player " + networkObject.NetworkObjectId.ToString()
            : "Player " + GetInstanceID().ToString();
        Player player = GetComponent<Player>();
        player.Setup();

        if (GameManager.Singleton != null)
        {
            GameManager.Singleton.RegisterPlayer(name, player);
        }
    }

    private void SetLayerMaskForAllChildren(Transform transform, LayerMask layerMask)
    {
        transform.gameObject.layer = layerMask;
        for (int i = 0; i < transform.childCount; i ++ )
        {
            SetLayerMaskForAllChildren(transform.GetChild(i), layerMask);
        }
    }

    private void DisableComponents()
    {
        for (int i = 0; i < componentsToDisable.Length; i++)
        {
            componentsToDisable[i].enabled = false;
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (sceneCamera != null)
        {
            sceneCamera.gameObject.SetActive(true);
        }

        if (GameManager.Singleton != null)
        {
            GameManager.Singleton.UnRegisterPlayer(transform.name);
        }
    }
}
