using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class CameraFollow : NetworkBehaviour
{
    [SerializeField] private Transform cameraObject;

    public override void OnNetworkSpawn()
    {
        if (IsOwner){
            cameraObject = Camera.main.transform;
        }
    }

    void Update()
    {
        if (IsOwner){
            transform.position = cameraObject.transform.position;
            transform.rotation = cameraObject.transform.rotation;
        }

    }
}
