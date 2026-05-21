using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PlayerInfo : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI playerName;
    [SerializeField]
    private Transform playerHealth;
    [SerializeField]
    private Transform infoUI;

    private Player player;
    private Camera cachedCamera;

    private void Start()
    {
        player = GetComponent<Player>();
    }

    // Update is called once per frame
    void Update()
    {
        if (player == null)
        {
            return;
        }

        if (playerName != null)
        {
            playerName.text = transform.name;
        }

        if (playerHealth != null)
        {
            playerHealth.localScale = new Vector3((float)player.GetHealth() / Mathf.Max(1, player.GetMaxHealth()), 1f, 1f);
        }

        if (cachedCamera == null || !cachedCamera.isActiveAndEnabled)
        {
            cachedCamera = Camera.main;
        }

        if (cachedCamera == null)
        {
            return;
        }
        if (infoUI != null)
        {
            infoUI.transform.LookAt(infoUI.transform.position + cachedCamera.transform.rotation * Vector3.back, cachedCamera.transform.rotation * Vector3.up);
            infoUI.Rotate(new Vector3(0f, 180f, 0f));
        }
    }
}
