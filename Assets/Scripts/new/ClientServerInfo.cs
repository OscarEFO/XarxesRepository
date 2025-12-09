using UnityEngine;

public class ClientServerInfo : MonoBehaviour
{
    public static ClientServerInfo Instance;

    public string userName = "Player";
    public string serverIP = "127.0.0.1";

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject); // Evita duplicados al cargar otra escena
        }
    }

    // Cambiar nombre de usuario
    public void ChangeUserName(string name)
    {
        userName = name;
    }

    // Cambiar IP del servidor
    public void ChangeServerIP(string ip)
    {
        serverIP = ip;
    }

    // -------------------- NUEVO --------------------
    // Llamado por SceneManag para actualizar ClientManager al cambiar de escena
    public void ChangeUserAndIP()
    {
        // Buscamos el ClientManager en la escena
        ClientManagerUDP clientScript = GameObject.Find("ClientManager")?.GetComponent<ClientManagerUDP>();

        if (clientScript != null)
        {
            clientScript.userName = userName;
            clientScript.serverIP = serverIP;
        }
        else
        {
            Debug.Log("ClientManager not found in the scene.");
        }
    }

    // Aplicar manualmente a un ClientManager (por UI antes de cambiar de escena)
    public void ApplyToClient(ClientManagerUDP client)
    {
        if (client != null)
        {
            client.userName = userName;
            client.serverIP = serverIP;
        }
    }
}
