using UnityEngine;

[RequireComponent(typeof(PlayerMovement))]
public class NetworkPlayer : MonoBehaviour
{
    public GameObject remoteServer;
    private PlayerMovement pm;

    public float sendInterval = 0.05f;
    private float sendTimer = 0f;

    void Start()
    {
        pm = GetComponent<PlayerMovement>();
    }

    void Update()
    {
        if (ClientUDP.Instance == null) return;

        // --- 1) enviar input + rotación ---
        sendTimer += Time.deltaTime;
        if (sendTimer >= sendInterval)
        {
            sendTimer = 0f;

            Vector2 input = pm.GetInputVector();
            float rotationZ = transform.eulerAngles.z;

            ClientUDP.Instance.SendInput(input, rotationZ);
        }

        // --- 2) aplicar snapshot autoritativo ---
        if (ClientUDP.Instance.hasUpdate)
        {
            Vector2 authoritative = new Vector2(ClientUDP.Instance.client_x, ClientUDP.Instance.client_y);

            pm.ApplyPosition(authoritative);
            transform.rotation = Quaternion.Euler(0, 0, ClientUDP.Instance.client_rot);

            // actualizar representación del server
            if (remoteServer != null)
            {
                remoteServer.transform.position =
                    new Vector3(ClientUDP.Instance.server_x, ClientUDP.Instance.server_y, 0);

                remoteServer.transform.rotation =
                    Quaternion.Euler(0, 0, ClientUDP.Instance.server_rot);
            }

            ClientUDP.Instance.hasUpdate = false;
        }
    }
}
