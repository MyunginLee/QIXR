using Unity.Netcode;
using UnityEngine;

public class NetworkButtons : MonoBehaviour
{
    [SerializeField] private GameObject obj;

    public void Host()
    {
        
        NetworkManager.Singleton.StartHost();

    }

    public void Client()
    {
        NetworkManager.Singleton.StartClient();
    }
            
}
