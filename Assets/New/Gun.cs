﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Gun : MonoBehaviour
{
    [Header("WEAPON INFOR")]
    public PlayerController.WeaponSlot weaponSlot;
    public string weaponName;
    public int ammoCount; // đạn
    public int magSize; // số viên đạn trong 1 băng
    //public int mag; // băng đạn
    public GameObject magazine; // mag gameobject

    [SerializeField] float fireRate = 3f;
    public bool isFiring = false;

    float _nextFireTime = 0f;
    [HideInInspector] public GunRecoil recoil;


    [Header("EFFECTS")]
    public ParticleSystem[] muzzleFlash;
    public ParticleSystem hitEffect;
    public TrailRenderer trailEffect; // tia lửa 
    private TrailRenderer currentTrail;

    [Header("RAYCAST")]
    public Transform raycast;
    public Transform raycastDestination;

    Ray _ray;
    RaycastHit _hit;

    private void Awake()
    {
        recoil = GetComponent<GunRecoil>();
        //mag = Random.Range(1, 3); // random băng đạn ngẫu nhiên
        ammoCount = magSize; // ban đầu cho đạn = số viên đạn trong băng. VD: magSize = 30 => ammoCount = 30.
        
    }

    void Update()
    {
        if (isFiring && Time.time >= _nextFireTime)
        {
            FireBullet();
            _nextFireTime = Time.time + 1f / fireRate;
        }
    }

    public void StartFiring()
    {
        isFiring = true;  
        recoil.Reset();
    }
    public void StopFiring()
    {
        isFiring= false;
    }

    void FireBullet()
    {
    if (ammoCount <= 0) return;
    ammoCount--;

    foreach (var particle in muzzleFlash)
    {
        particle.Emit(1);
    }

    _ray.origin = raycast.position;
    _ray.direction = raycastDestination.position - raycast.position;

    if (currentTrail == null)
    {
        // Tạo hiệu ứng trail mới nếu không có hiệu ứng trail nào tồn tại
        currentTrail = Instantiate(trailEffect, _ray.origin, Quaternion.identity);
    }
    else
    {
        // Di chuyển hiệu ứng trail hiện tại đến vị trí xuất phát mới
        currentTrail.transform.position = _ray.origin;
    }

    // Đặt điểm xuất phát của hiệu ứng trail tại vị trí xuất phát của raycast
    currentTrail.Clear();
    currentTrail.AddPosition(_ray.origin);

    if (Physics.Raycast(_ray, out _hit))
    {
        hitEffect.transform.position = _hit.point;
        hitEffect.transform.forward = _hit.normal;
        hitEffect.Emit(1);

        // Di chuyển hiệu ứng trail đến hit point
        currentTrail.transform.position = _hit.point;
    }
    else
    {
        // Nếu không có va chạm, di chuyển hiệu ứng trail theo raycast
        currentTrail.transform.position = _ray.origin + _ray.direction * 100f;
    }
    recoil.GenerateRecoil(weaponName);
    }

}
