using UnityEngine;

[RequireComponent(typeof(PlayerMovement))]
public class NetworkPlayer : MonoBehaviour
{
    public GameObject remoteServer; // assign in inspector: the object that shows the server player

    private PlayerMovement pm;
    private string lastSentInput = "NONE";

    void Start()
    {
        pm = GetComponent<PlayerMovement>();

        if (remoteServer == null)
            Debug.LogWarning("[NetworkPlayer] remoteServer not assigned - remote player won't be visible.");
    }

    void Update()
    {
        if (ClientUDP.Instance == null) return; // not started yet

        // 1) read current local input from PlayerMovement
        string input = pm.GetInputString();

        if (input != "NONE")
        {
                Vector2 currentPos = transform.position;
                float rotationZ = transform.eulerAngles.z;
                ClientUDP.Instance.SendPlayerState(currentPos, rotationZ);
        }

        // 3) if server sent an authoritative update, apply it
        if (ClientUDP.Instance.hasUpdate)
        {
            // server_x/server_y = server player pos
            // client_x/client_y = authoritative client pos (yours)
            // Apply authoritative client position to local PlayerMovement (reconciliation)
            Vector2 authoritativeClientPos = new Vector2(ClientUDP.Instance.client_x, ClientUDP.Instance.client_y);
            pm.ApplyPosition(authoritativeClientPos);

            // Update remote server visualization
            if (remoteServer != null)
            {
                remoteServer.transform.position = new Vector3(ClientUDP.Instance.server_x, ClientUDP.Instance.server_y, 0f);
            }

            // clear update flag so we don't reapply repeatedly until next server packet
            ClientUDP.Instance.hasUpdate = false;
        }
    }
}
