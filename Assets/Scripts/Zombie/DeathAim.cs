using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class DeathAim : MonoBehaviourPun
{
    public float destroyTime;
    void Start()
    {
        Invoke("OnDestroyObject",destroyTime);
    }
    private void OnDestroyObject()
    {
        Destroy(gameObject);
    }
}
