using UnityEngine;
using TMPro;

public class ClientServerInfoStatic : MonoBehaviour
{
    public TMP_InputField userNameInput;
    public TMP_InputField serverIPInput;

    void Start()
    {
        // Inicializamos los InputFields con los valores actuales del ClientServerInfo persistente
        if (ClientServerInfo.Instance != null)
        {
            if (userNameInput != null) userNameInput.text = ClientServerInfo.Instance.userName;
            if (serverIPInput != null) serverIPInput.text = ClientServerInfo.Instance.serverIP;
        }
    }

    public void ChangeUserName(string name)
    {
        if (ClientServerInfo.Instance != null)
        {
            ClientServerInfo.Instance.ChangeUserName(name);
        }
    }

    public void ChangeServerIP(string ip)
    {
        if (ClientServerInfo.Instance != null)
        {
            ClientServerInfo.Instance.ChangeServerIP(ip);
        }
    }
}
