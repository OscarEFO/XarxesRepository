using UnityEngine;

[RequireComponent(typeof(PlayerMovement))]
public class NetworkPlayer : MonoBehaviour
{
    public GameObject remoteServer;

    private PlayerMovement pm;

    void Start()
    {
        pm = GetComponent<PlayerMovement>();

        if (remoteServer == null)
            Debug.LogWarning("[NetworkPlayer] remoteServer not assigned - remote player won't be visible.");
    }

    void Update()
{
    if (ClientUDP.Instance == null) return;

    Vector2 inputVec = pm.GetInputVector();
    float rotationZ = transform.eulerAngles.z;

    ClientUDP.Instance.SendInput(inputVec, rotationZ);

    if (ClientUDP.Instance.hasUpdate)
    {
        Vector2 authoritativeClientPos = new Vector2(ClientUDP.Instance.client_x, ClientUDP.Instance.client_y);
        pm.ApplyPosition(authoritativeClientPos);

        if (remoteServer != null)
        {
            remoteServer.transform.position =
                new Vector3(ClientUDP.Instance.server_x, ClientUDP.Instance.server_y, 0f);
        }

        ClientUDP.Instance.hasUpdate = false;
    }
}
}
