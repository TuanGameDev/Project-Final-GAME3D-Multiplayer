﻿using UnityEngine;
using Photon.Pun;

public class Bullet : MonoBehaviourPun
{
    [SerializeField] private float bulletSpeed = 20f;
    [SerializeField] private float destroyDelay = 2f;
    [SerializeField] public float bulletDamage;

    private void Update()
    {
        MoveBullet();
    }

    private void MoveBullet()
    {
        transform.Translate(Vector3.forward * bulletSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject != gameObject)
        {
            if (other.CompareTag("Zombie"))
            {
                ZombieHit(other.gameObject);
            }
            photonView.RPC("DestroyBullet", RpcTarget.AllBuffered);
        }
    }

    [PunRPC]
    private void DestroyBullet()
    {
        Invoke("DestroyDelayed", destroyDelay);
    }

    private void DestroyDelayed()
    {
        PhotonNetwork.Destroy(gameObject);
    }

    private void ZombieHit(GameObject zombie)
    {
        zombie.GetComponent<AIZombie>().TakeDamage(bulletDamage);
    }
}
