using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement; // usar el namespace de Unity

public class SceneManag : MonoBehaviour
{
    void Start()
    {
        GameObject clientServerInfo = GameObject.Find("ClientServerInfo");

        if (clientServerInfo != null)
        {
            clientServerInfo.GetComponent<ClientServerInfo>().ChangeUserAndIP();
        }
        else
        {
            Debug.Log("ClientServerInfo not found in the scene.");
        }
    }

    public void ChangeScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void CloseApp()
    {
        Application.Quit();
    }

    public void PlayAsClient()
    {
        //GameObject clientServerInfo = GameObject.Find("ClientServerInfo");
        //clientServerInfo?.GetComponent<ClientServerInfo>().ChangeUserAndIP();

        ChangeScene("Client");
    }

    public void PlayAsServer()
    {
        ChangeScene("Server");
    }
}
