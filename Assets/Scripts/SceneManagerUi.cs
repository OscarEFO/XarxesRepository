using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneManagerUI : MonoBehaviour
{
    public GameObject player;
    public GameObject serverObject;
    public void StartServerTCP()
    {
        GameObject go = new GameObject("ServerTCP");
        go.AddComponent<ServerTCP>();
    }

    public void StartServerUDP()
    {
        GameObject go = new GameObject("ServerUDP");
        var server = go.AddComponent<ServerUDP>();

        server.player = player;
        server.serverObject = serverObject;
    }

    public void StartClientTCP()
    {
        GameObject go = new GameObject("ClientTCP");
        go.AddComponent<ClientTCP>(); 
    }

    public void StartClientUDP()
    {
        GameObject go = new GameObject("ClientUDP");
        var client = go.AddComponent<ClientUDP>();

        client.player = player;
        client.serverObject = serverObject;
    }

    public void SwitchScene()
    {
        string current = SceneManager.GetActiveScene().name;
        string next = current == "Server" ? "Client" : "Server";
        SceneManager.LoadScene(next);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
