using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneManagerUI : MonoBehaviour
{
    public void StartServerTCP()
    {
        GameObject go = new GameObject("ServerTCP");
        go.AddComponent<ServerTCP>();
    }

    public void StartServerUDP()
    {
        GameObject go = new GameObject("ServerUDP");
        go.AddComponent<ServerUDP>();
    }

    public void StartClientTCP()
    {
        GameObject go = new GameObject("ClientTCP");
        go.AddComponent<ClientTCP>();
    }

    public void StartClientUDP()
    {
        GameObject go = new GameObject("ClientUDP");
        go.AddComponent<ClientUDP>();
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
