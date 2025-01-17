﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using TMPro;
using Photon.Realtime;
using Cinemachine;
using Unity.VisualScripting;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using UnityEngine.SceneManagement;
using static Gun;
using static AmmoPickUp;
using Unity.Burst.CompilerServices;


public class PlayerController : MonoBehaviourPun
{
    CharacterController _controller;
    public static PlayerController me;

    [Header("CAMERA")]
    [SerializeField] Camera _maincam;
    [SerializeField] Transform playerCam;
    CinemachineFreeLook CMfreelook;
    float turnspeed = 15;

    [Header("HUD")]
    public bool _isDead = false;
    public int id;
    public float _health = 100.0f;
    private float maxhealthslider;
    public float armor;
    public Slider sliderhealth;
    public TextMeshProUGUI nametagText;
    public TextMeshProUGUI pickupText;
    public TextMeshProUGUI ammoText;
    public TextMeshProUGUI magText;
    public TextMeshProUGUI weaponNameText;
    public Image weaponIcon;
    public RawImage runIcon;
    public RawImage flashlightIcon;
    public GameObject settingPopup;

    public Player photonPlayer;
    [SerializeField] private CameraBloodEffect _cameraBloodEffect = null;
    [SerializeField] private Canvas cavansHUD;

    [Header("MOVEMENT")]
    [SerializeField] public AudioSource footstepAudioSource;
    [SerializeField] public AudioClip[] footstepSounds;
    [SerializeField] public float jumpHeight;
    float _hor, _ver;
    float _turnCalmTime = 0.1f;
    float _turnCalmVelocity;
    bool _isJumping = false;
    Vector3 direction;
    Vector3 _velocity;


    [Header("WEAPON")]
    public int rifleAmmo;
    public int smgAmmo;
    [SerializeField] Gun detectedGun;
    [SerializeField] AmmoPickUp detectedAmmo;
    [SerializeField] Transform detectPos;
    [SerializeField] float detectRange;
    [SerializeField] private float ReloadRate;
    [SerializeField] private Image ReloadIMG;
    [SerializeField] private Image cooldownReloadIMG;
    [SerializeField] private Image crosshairIMG;
    [SerializeField] Transform[] weaponSlots;
    [SerializeField] Transform crosshairTarget;
    float aimZoomDistance = 20f;
    float zoomSpeed = 10f; // Tốc độ zoom

    private float defaultZoomDistance;
    private float targetZoomDistance; // Khoảng cách zoom mục tiêu

    GameObject magazineHand;
    Gun[] _equipWeapons = new Gun[2];
    int _activeWeaponIndex;
    bool _weaponActive = false;
    bool _isHolstered = false;
    bool _aiming = false;
    bool _isReloading = false;
    bool isGamePaused = false;

    [Header("RIGGING")]
    [SerializeField] Animator rigController;
    [SerializeField] Transform leftHand;
    [SerializeField] GameObject head;
    [SerializeField] GameObject spine1;
    [SerializeField] GameObject spine2;
    public WeaponAnimationEvents animationEvents;
    public Animator _anim;

    [Header("Items")]
    [SerializeField] BandagePickUp detectedBandage;
    public int Bandage;
    private float cooldownDuration = 5f;
    public Image cooldownImage;
    public TextMeshProUGUI bandageText;
    public TextMeshProUGUI addbandageText;
    public TextMeshProUGUI addbandagecooldownText;
    private bool isCooldown = false;
    public enum WeaponSlot
    {
        Primary = 0,
        Secondary = 1
    }
    [PunRPC]
    public void Initialized(Player player)
    {
        id = player.ActorNumber;
        photonPlayer = player;
        UpdateNameTag(player.NickName);
        GameManager.gamemanager.playerCtrl[id - 1] = this;
        _health = 100.0f;
        UpdateValue(_health);
        SetHashes();
        if (player.IsLocal)
            me = this;
    }
    void Start()
    {
        weaponIcon.gameObject.SetActive(false);
        _controller = GetComponent<CharacterController>();
        _anim = GetComponent<Animator>();

        CMfreelook = GetComponentInChildren<CinemachineFreeLook>();
        animationEvents = GetComponentInChildren<WeaponAnimationEvents>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (!photonView.IsMine)
        {
            if (_maincam)
                _maincam.gameObject.SetActive(false);
            cavansHUD.enabled = false;
        }

        defaultZoomDistance = CMfreelook.m_Lens.FieldOfView;
        targetZoomDistance = defaultZoomDistance;

        animationEvents.WeaponAnimationEvent.AddListener(OnAnimationEvent);

        Gun existingweapon = GetComponentInChildren<Gun>();
        if (existingweapon)
        {
            photonView.RPC("PickUpWeapon", RpcTarget.AllBuffered, existingweapon.GetComponent<PhotonView>().ViewID);
        }
    }
    void Update()
    {
        if (!photonView.IsMine) return;
        if (_isDead) return;
        UpdateBandageText(Bandage);
        UpdateValue(_health);
        HandleInput();
        CameraNameTag();
        _anim.SetBool("weaponActive", _weaponActive);
    }
    void FixedUpdate()
    {
        if (!photonView.IsMine) return;
        if (_isDead) return;

        _hor = Input.GetAxisRaw("Horizontal");
        _ver = Input.GetAxisRaw("Vertical");
        direction = new Vector3(_hor, 0f, _ver).normalized;

        if (_weaponActive && !_isHolstered)
        {
            SetCam_WithWeapon();
            MovementWithWeapon();
            head.gameObject.SetActive(true);
            spine1.gameObject.SetActive(true);
            spine2.gameObject.SetActive(true);
        }
        else
        {
            MovementWithoutWeapon();
            head.gameObject.SetActive(false);
            spine1.gameObject.SetActive(false);
            spine2.gameObject.SetActive(false);
        }

        if (_isJumping)
        {
            _velocity.y += Physics.gravity.y * Time.fixedDeltaTime;
            _controller.Move(_velocity * Time.fixedDeltaTime);

            if (_controller.isGrounded)
            {
                _isJumping = false;
                _anim.SetBool("isJumping", _isJumping);
                _velocity = Vector3.zero;
            }
        }
        else
        {
            _velocity.y += Physics.gravity.y * Time.fixedDeltaTime;
            _controller.Move(_velocity * Time.fixedDeltaTime);
            if (!_controller.isGrounded)
            {
                _isJumping = true;
                _anim.SetBool("isJumping", _isJumping);
                _velocity = Vector3.zero;
            }
        }

        DetectObj();
        UpdateWeaponState();
        UpdateAimingState();

    }
    public void DetectObj()
    {
        if (detectPos == null || crosshairTarget == null)
        {
            Debug.LogError("detectPos or crosshairTarget is null.");
            return;
        }

        Vector3 direction = crosshairTarget.position - detectPos.position;
        RaycastHit hit;
        bool isHit = Physics.Raycast(detectPos.position, direction, out hit, detectRange);

        if (isHit)
        {
            HandleHitObject(hit.collider);
        }
        else
        {
            ResetDetectedObjects();
        }
    }

    private void HandleHitObject(Collider collider)
    {
        Gun gun = collider.GetComponent<Gun>();
        AmmoPickUp ammoObj = collider.GetComponent<AmmoPickUp>();
        BandagePickUp bandageOjb = collider.GetComponent<BandagePickUp>();
        PlayerController player = collider.GetComponent<PlayerController>();

        if (gun != null)                                // raycast hit weapon
        {
            HandleDetectedGun(gun);
        }
        else if (ammoObj != null)                       // raycast hit ammo
        {
            HandleDetectedAmmo(ammoObj);
        }
        else if (bandageOjb)
        {
            HandleDetectedBandage(bandageOjb);
        }
        else if (player != null && player._isDead)      // raycast hit if player dead
        {
            HandleDetectedPlayer(player);
        }
        else
        {
            ResetDetectedObjects();
        }
    }

    private void HandleDetectedGun(Gun gun)
    {
        if (detectedGun != null && detectedGun != gun && !gun.isPickedUp)
        {
            detectedGun.highlight.ToggleHighlight(false);
        }
        detectedGun = gun;
        detectedGun.highlight.ToggleHighlight(true);

        pickupText.gameObject.SetActive(true);
        pickupText.text = "Pick up: " + gun.weaponName;
        detectedAmmo = null;
    }

    private void HandleDetectedAmmo(AmmoPickUp ammoObj)
    {
        if (detectedAmmo != null)
        {
            detectedAmmo.highlight.ToggleHighlight(false);
            detectedAmmo = null;
        }

        detectedAmmo = ammoObj;
        pickupText.gameObject.SetActive(true);
        detectedAmmo.highlight.ToggleHighlight(true);
        pickupText.text = "Take: " + detectedAmmo.amount + " bullets " + detectedAmmo.ammoName;
    }
    private void HandleDetectedBandage(BandagePickUp bandageObj)
    {
        if (detectedBandage != null)
        {
            detectedBandage.highlight.ToggleHighlight(false);
            detectedBandage = null;
        }
        detectedBandage = bandageObj;
        detectedBandage.highlight.ToggleHighlight(true);

        pickupText.gameObject.SetActive(true);
        pickupText.text = "Take bandage";
    }

    private void HandleDetectedPlayer(PlayerController player)
    {
        pickupText.gameObject.SetActive(true);
        pickupText.text = "Press F to revive " + player.name;

        if (Input.GetKeyDown(KeyCode.F))
        {
            Vector3 spawnPos = GameManager.gamemanager.spawnPoint[Random.Range(0, GameManager.gamemanager.spawnPoint.Length)].position;
            player.photonView.RPC("Respawn", RpcTarget.All, spawnPos);
            pickupText.gameObject.SetActive(false);
        }
    }

    private void ResetDetectedObjects()
    {
        if (detectedGun != null)
        {
            detectedGun.highlight.ToggleHighlight(false);
            detectedGun = null;
        }

        if (detectedAmmo != null)
        {
            detectedAmmo.highlight.ToggleHighlight(false);
            detectedAmmo = null;
        }
        
        if (detectedBandage != null)
        {
            detectedBandage.highlight.ToggleHighlight(false);
            detectedBandage = null;
        }

        pickupText.gameObject.SetActive(false);
    }



    #region CameraNametag
    public void CameraNameTag()
    {
        if (_maincam == null)
        {
            return;
        }
        Vector3 directionToCamera = _maincam.transform.position - nametagText.transform.position;
        nametagText.transform.rotation = Quaternion.LookRotation(-directionToCamera);
    }
    #endregion
    //
    #region HEALTH + ARMOR + SPAWNPLAYER
    [PunRPC]
    public void TakeDamage(float damageAmount)
    {
        _health -= damageAmount;
        AudioManager._audioManager.PlaySFX(Random.Range(0, 7));
        if (_cameraBloodEffect != null)
        {
            _cameraBloodEffect.minBloodAmount = (1.0f - _health / 100.0f);
            _cameraBloodEffect.bloodAmount = Mathf.Min(_cameraBloodEffect.minBloodAmount + 0.05f, 0.5f);
        }
        if (_health <= 0)
        {
            Die();
            AudioManager._audioManager.StopSFX(Random.Range(0, 7));
        }
        UpdateValue(_health);
        if (!photonView.IsMine) return;
        {
            SetHashes();
        }
    }
    public void SetHashes()
    {
        try
        {
            Hashtable hash = new Hashtable();
            hash["Health"] = _health;
            PhotonNetwork.LocalPlayer.SetCustomProperties(hash);
            UpdateValue(_health);
        }
        catch
        {
            //
        }
    }
    void Die()
    {
        _isDead = true;
        photonView.RPC("Dead",RpcTarget.All);
    }
    [PunRPC]
    public void Dead()
    {
        _anim.SetTrigger("isDead");
        var currentWeapon = GetActiveWeapon();
        if (currentWeapon != null)
        {
            DropWeapon();
        }
    }
    [PunRPC]
    public void Respawn(Vector3 spawnPos)
    {
        StartCoroutine(Spawn(spawnPos, GameManager.gamemanager.respawnTime));
    }

    public IEnumerator Spawn(Vector3 spawnPos, float timeToSpawn)
    {
        yield return new WaitForSeconds(timeToSpawn);
        _isDead = false;

        _anim.Play("Move");
        _controller.enabled = false;
        transform.position = spawnPos;
        _controller.enabled = true;


        _health = Mathf.Min(_health + 25f, 100f);
        UpdateValue(_health);
        _cameraBloodEffect.minBloodAmount = (1.0f - _health / 100.0f);
        _cameraBloodEffect.bloodAmount = Mathf.Min(_cameraBloodEffect.minBloodAmount + 0.05f, 0.5f);
    }
    void UpdateNameTag(string name)
    {
        nametagText.text = name;
    }
    public void UpdateValue(float maxVal)
    {
        sliderhealth.value = maxVal;
    }
    #endregion
    //
    #region INPUT
    void HandleInput()
    {
        var _weapon = GetWeapon(_activeWeaponIndex);
        if (_weapon != null && !_isHolstered)
        {
            _weaponActive = true;
            if (Input.GetMouseButton(0) && !_isReloading)
            {
                _weapon.photonView.RPC("StartFiring", RpcTarget.All);
                UpdateAmmo();
            }
            else
            {
                _weapon.photonView.RPC("StopFiring", RpcTarget.All);
            }

            if (Input.GetMouseButton(1))
            {
                _aiming = true;
            }
            else
            {
                _aiming = false;
            }
            if (Input.GetKeyDown(KeyCode.G))
            {
                if (_weapon.flashActive == false)
                {
                    _weapon.photonView.RPC("TurnOnFlashlight", RpcTarget.All);
                    flashlightIcon.color = Color.white;
                }
                else if (_weapon.flashActive == true)
                {
                    _weapon.photonView.RPC("TurnOffFlashlight", RpcTarget.All);
                    flashlightIcon.color = new Color(0.42f, 0.42f, 0.42f); // = 6B6B6B
                }
            }
            if (Input.GetKeyDown(KeyCode.R) && _weapon.ammoCount < _weapon.magSize && !_isReloading)
            {
                if (_weapon.weaponType == WeaponType.Rifle && rifleAmmo > 0)
                {
                    Reload();
                }
                else if (_weapon.weaponType == WeaponType.SMG_Pistol)
                {
                    Reload();
                }
            }
        }                                  // behavior if weapon actived
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (detectedGun != null)
            {
                PickUpWeapon(detectedGun);
                detectedGun.highlight.ToggleHighlight(false);
                detectedGun = null;
            }

            if (detectedAmmo != null)
            {
                detectedAmmo.PickUp(this);
                detectedAmmo.highlight.ToggleHighlight(false);
                detectedAmmo = null;
            }

            if (detectedBandage != null)
            {
                detectedBandage.PickUpBandage(this);
                detectedBandage.highlight.ToggleHighlight(false);
                detectedBandage = null;
            }
        }                                       // pick up item if detected
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SetActiveWeapon(WeaponSlot.Primary);
        }                                  // use primary weapon                                  
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SetActiveWeapon(WeaponSlot.Secondary);
        }                                  // use secondary weapon
        if (Input.GetKeyDown(KeyCode.X))
        {
            ToggleWeaponHolster();
        }                                       // holsted weapon
        if (Input.GetKeyDown(KeyCode.Space) && !_isJumping)
        {
            Jump();
        }                    // jump
        if (_health < 100 && Input.GetKeyDown(KeyCode.I) && !isCooldown)
        {
            UseBandage();
            StartCoroutine(StartCooldown());
        }       // healing
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isGamePaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }                                  // open ESC menu
        if (Input.GetKeyDown(KeyCode.M))
        {
            Main();
        }                                       // go back Main if press M
        if (Input.GetKeyDown(KeyCode.N))
        {
            Exit();
        }                                       // exit if press N
    }
    #endregion
    //
    #region MOVEMENT
    void MovementWithWeapon()
    {
        if (direction.magnitude >= 0.1)
        {
            _anim.SetFloat("xValue", _hor);
            _anim.SetFloat("zValue", _ver);
            if (!footstepAudioSource.isPlaying)
            {
                AudioClip randomFootstepSound = footstepSounds[Random.Range(0, footstepSounds.Length)];
                footstepAudioSource.clip = randomFootstepSound;
                footstepAudioSource.Play();
            }
        }
        else
        {
            _anim.SetFloat("xValue", 0f);
            _anim.SetFloat("zValue", 0f);
            footstepAudioSource.Stop();
        }
        _anim.speed = 1f;
        runIcon.color = new Color(0.42f, 0.42f, 0.42f); // = 6B6B6B
    }
    void MovementWithoutWeapon()
    {
        if (direction.magnitude >= 0.1)
        {
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + playerCam.eulerAngles.y;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _turnCalmVelocity, _turnCalmTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            _anim.SetFloat("Speed", direction.magnitude * (Input.GetKey(KeyCode.LeftShift) ? _anim.speed = 1.4f : _anim.speed = 1f));

            if (Input.GetKey(KeyCode.LeftShift))
            {
                runIcon.color = Color.white;
            }
            else
            {
                runIcon.color = new Color(0.42f, 0.42f, 0.42f); // = 6B6B6B
            }

            if (!footstepAudioSource.isPlaying)
            {
                AudioClip randomFootstepSound = footstepSounds[Random.Range(0, footstepSounds.Length)];
                footstepAudioSource.clip = randomFootstepSound;
                footstepAudioSource.Play();
            }
        }
        else
        {
            _anim.SetFloat("Speed", 0f);
            footstepAudioSource.Stop();
        }
    }
    void SetCam_WithWeapon()
    {
        float yCam = _maincam.transform.rotation.eulerAngles.y;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, yCam, 0), turnspeed * Time.fixedDeltaTime);
    }
    public void Jump()
    {
        if (_controller.isGrounded && !_isJumping)
        {
            _isJumping = true;
            _anim.SetBool("isJumping", _isJumping);

            Vector3 forwardVelocity = transform.forward * 1f * 1.5f;
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * Physics.gravity.y);
            _velocity += forwardVelocity;

            photonView.RPC("JumpRPC", RpcTarget.All);
        }
    }

    [PunRPC]
    public void JumpRPC()
    {
        _anim.SetBool("isJumping", _isJumping);
    }
    #endregion
    //
    #region WEAPON
    void UpdateWeaponState()
    {
        var _weapon = GetWeapon(_activeWeaponIndex);
        if (_weapon != null && !_isHolstered)
        {
            UpdateAmmo();
            _weaponActive = true;
        }
        else
        {
            _weaponActive = false;
            ammoText.text = "";
            magText.text = "";
            weaponNameText.text = "";
            weaponIcon.sprite = null;
            weaponIcon.gameObject.SetActive(false);
        }
    }
    void UpdateAimingState()
    {
        var _weapon = GetWeapon(_activeWeaponIndex);
        if (_aiming)
        {
            _weapon.recoil.recoilModifier = _aiming ? _weapon.recoil.aimRecoil : _weapon.recoil.noaimRecoil;
            CMfreelook.m_Lens.FieldOfView = Mathf.Lerp(CMfreelook.m_Lens.FieldOfView, aimZoomDistance, Time.deltaTime * zoomSpeed);
        }
        else
        {
            CMfreelook.m_Lens.FieldOfView = Mathf.Lerp(CMfreelook.m_Lens.FieldOfView, defaultZoomDistance, Time.deltaTime * zoomSpeed);
        }
    }
    Gun GetWeapon(int index)
    {
        if (index < 0 || index >= _equipWeapons.Length)
        {
            return null;
        }
        return _equipWeapons[index];
    }
    Gun GetActiveWeapon() => GetWeapon(_activeWeaponIndex);
    [PunRPC]
    public void StartShoot()
    {
        var _weapon = GetWeapon(_activeWeaponIndex);
        if (_weapon != null)
        {
            _weapon.StartFiring();
        }

    }
    [PunRPC]
    public void StopShoot()
    {
        var _weapon = GetWeapon(_activeWeaponIndex);
        if (_weapon != null)
        {
            _weapon.StopFiring();
        }
    }
    void PickUpWeapon(Gun gun)
    {
        photonView.RPC("PickUpWeaponRPC", RpcTarget.AllBuffered, gun.GetComponent<PhotonView>().ViewID);
    }
    [PunRPC]
    public void PickUpWeaponRPC(int weaponViewID)
    {
        PhotonView weaponPhotonView = PhotonView.Find(weaponViewID);
        Gun newWeapon = weaponPhotonView.GetComponent<Gun>();
        int weaponSlotIndex = (int)newWeapon.weaponSlot;
        var currentWeapon = GetActiveWeapon();

        if (currentWeapon != null && currentWeapon.weaponType == newWeapon.weaponType)
        {
            DropWeapon();
        }

        photonView.RPC("EquipWeapon", RpcTarget.AllBuffered, weaponViewID, weaponSlotIndex);
    }
    [PunRPC]
    public void EquipWeapon(int weaponViewID, int weaponSlotIndex)
    {
        PhotonView weaponPhotonView = PhotonView.Find(weaponViewID);
        Gun newWeapon = weaponPhotonView.GetComponent<Gun>();

        var weapon = GetWeapon(weaponSlotIndex);
        if (weapon == null)
        {
            weapon = newWeapon;

            newWeapon.transform.localPosition = Vector3.zero;
            newWeapon.transform.localRotation = Quaternion.identity;
            newWeapon.GetComponent<BoxCollider>().enabled = false;
            newWeapon.GetComponent<Rigidbody>().isKinematic = true;
            newWeapon.isPickedUp = true;
            weapon.raycastDestination = crosshairTarget;
            weapon.recoil.playerCamera = CMfreelook;
            weapon.recoil.rig = rigController;
            weapon.transform.SetParent(weaponSlots[weaponSlotIndex], false);
            _equipWeapons[weaponSlotIndex] = weapon;
            SetActiveWeapon(newWeapon.weaponSlot);
        }
    }
    public void DropWeapon()
    {
        if (_weaponActive && !_isHolstered)
        {
            Gun currentWeapon = GetActiveWeapon();
            if (currentWeapon != null)
            {
                Vector3 position = currentWeapon.transform.position + transform.forward;
                photonView.RPC("DropWeaponRPC", RpcTarget.All, currentWeapon.photonView.ViewID, position, _activeWeaponIndex);
            }
        }
    }
    [PunRPC]
    public void DropWeaponRPC(int weaponViewID, Vector3 position, int weaponIndex)
    {
        PhotonView weaponPhotonView = PhotonView.Find(weaponViewID);
        Gun currentWeapon = GetActiveWeapon();
        if (currentWeapon != null)
        {
            currentWeapon.transform.position = position;
            currentWeapon.GetComponent<BoxCollider>().enabled = true;
            currentWeapon.GetComponent<Rigidbody>().isKinematic = false;
            currentWeapon.isPickedUp = false;
            currentWeapon.flashActive = false;
            currentWeapon.flashlight.gameObject.SetActive(false);

            Rigidbody rb = currentWeapon.GetComponent<Rigidbody>();
            rb.AddForce((position - currentWeapon.transform.position).normalized * 2f, ForceMode.Impulse);

            currentWeapon.transform.parent = null;

            UpdateWeaponStateAfterDrop(weaponIndex);
        }
    }
    private void UpdateWeaponStateAfterDrop(int weaponIndex)
    {
        _equipWeapons[weaponIndex] = null;
        _activeWeaponIndex = -1;
        _weaponActive = false;
        _aiming = false;
        rigController.Play("weapon_unarmed");
    }
    void SetActiveWeapon(WeaponSlot weaponSlot)
    {
        int holsterIndex = _activeWeaponIndex;
        int activateIndex = (int)weaponSlot;

        if (photonView.IsMine)
        {
            photonView.RPC("SetActiveWeaponRPC", RpcTarget.All, holsterIndex, activateIndex);
        }
    }
    [PunRPC]
    public void SetActiveWeaponRPC(int holsterIndex, int activateIndex)
    {
        var weapon = GetWeapon(holsterIndex);
        if (weapon != null)
        {
            weapon.StopFiring();
        }

        if (holsterIndex == activateIndex)
        {
            holsterIndex = -1;
        }

        StartCoroutine(SwitchWeapon(holsterIndex, activateIndex));

    }
    void ToggleWeaponHolster()
    {
        photonView.RPC("WeaponHolster", RpcTarget.All);
    }
    [PunRPC]
    public void WeaponHolster()
    {
        _aiming = false;
        bool _isHolster = rigController.GetBool("holster_weapon");
        if (_isHolster)
        {
            StartCoroutine(ActivateWeapon(_activeWeaponIndex));
        }
        else
        {
            StartCoroutine(HolsterWeapon(_activeWeaponIndex));
        }
    }
    IEnumerator SwitchWeapon(int holsterIndex, int activateIndex)
    {
        _aiming = false;
        yield return StartCoroutine(HolsterWeapon(holsterIndex));
        yield return StartCoroutine(ActivateWeapon(activateIndex));
        _activeWeaponIndex = activateIndex;
        UpdateAmmo();
    }
    IEnumerator HolsterWeapon(int index)
    {
        _isHolstered = true;

        var weapon = GetWeapon(index);
        if (weapon != null)
        {
            flashlightIcon.color = new Color(0.42f, 0.42f, 0.42f); // = 6B6B6B
            weapon.photonView.RPC("TurnOffFlashlight", RpcTarget.All);
            rigController.SetBool("holster_weapon", true);
            do
            {
                yield return new WaitForEndOfFrame();
            }
            while (rigController.GetCurrentAnimatorStateInfo(0).normalizedTime < 1.0f);
        }
    }
    IEnumerator ActivateWeapon(int index)
    {
        var weapon = GetWeapon(index);
        if (weapon != null)
        {
            rigController.SetBool("holster_weapon", false);
            rigController.Play("weapon_" + weapon.weaponName);
            do
            {
                yield return new WaitForEndOfFrame();
            }
            while (rigController.GetCurrentAnimatorStateInfo(0).normalizedTime < 1.0f);
            _isHolstered = false;
        }
    }
    void OnAnimationEvent(string eventName)
    {
        switch (eventName)
        {
            case "detach_mag":
                DetachMag();
                break;
            case "drop_mag":
                DropMag();
                break;
            case "refill_mag":
                RefillMag();
                break;
            case "attach_mag":
                AttachMag();
                break;
        }
    }
    void DetachMag()
    {
        Gun weapon = GetActiveWeapon();
        magazineHand = Instantiate(weapon.magazine, leftHand, true);
        weapon.magazine.SetActive(false);
    }
    void DropMag()
    {
        GameObject dropMag = Instantiate(magazineHand, magazineHand.transform.position, magazineHand.transform.rotation);
        dropMag.AddComponent<Rigidbody>();
        dropMag.AddComponent<BoxCollider>();
        magazineHand.SetActive(false);
        Destroy(dropMag, 3f);
    }
    void RefillMag()
    {
        magazineHand.SetActive(true);
    }
    void AttachMag()
    {
        Gun weapon = GetActiveWeapon();
        if (weapon != null)
        {
            int ammoNeeded = weapon.magSize - weapon.ammoCount;

            if (weapon.weaponType == WeaponType.Rifle)
            {
                int ammoToLoad = ammoNeeded;
                if (ammoToLoad > rifleAmmo)
                {
                    ammoToLoad = rifleAmmo;
                }
                weapon.ammoCount += ammoToLoad;
                rifleAmmo -= ammoToLoad;
            }
            else if (weapon.weaponType == WeaponType.SMG_Pistol)
            {
                weapon.ammoCount = weapon.magSize;
            }
            weapon.magazine.SetActive(true);
            Destroy(magazineHand);
            rigController.ResetTrigger("reload");
        }
    }
    public void SetCrosshairActive(bool isActive)
    {
        crosshairIMG.enabled = isActive;
    }
    void Reload()
    {
        AudioManager._audioManager.PlaySFX(8);
        StartCoroutine(DelayedReload());
        photonView.RPC("ReloadRPC", RpcTarget.All);
    }
    IEnumerator DelayedReload()
    {
        cooldownReloadIMG.fillAmount = 1f;
        float startTime = Time.time;
        float endTime = startTime + ReloadRate;

        while (Time.time < endTime)
        {
            float remainingTime = endTime - Time.time;
            float fillAmount = remainingTime / ReloadRate;
            cooldownReloadIMG.fillAmount = fillAmount;
            _isReloading = true;
            ReloadIMG.gameObject.SetActive(true);
            crosshairIMG.enabled = false;
            yield return null;
        }
        cooldownReloadIMG.fillAmount = 0f;
        ReloadIMG.gameObject.SetActive(false);
        _isReloading = false;
        crosshairIMG.enabled = true;
    }
    [PunRPC]
    public void ReloadRPC()
    {
        rigController.SetTrigger("reload");
    }
    void UpdateAmmo()
    {
        Gun weapon = GetActiveWeapon();
        if (weapon != null)
        {
            ammoText.text = weapon.ammoCount + "";
            weaponNameText.text = "" + weapon.weaponName;
            weaponIcon.sprite = weapon.gunIcon;
            weaponIcon.gameObject.SetActive(true);

            if (weapon.weaponType == WeaponType.Rifle)
            {
                magText.text = rifleAmmo + "";
            }
            else if (weapon.weaponType == WeaponType.SMG_Pistol)
            {
                magText.text = smgAmmo + "";
            }
        }
    }
    #endregion
    //
    #region HUD
    void PauseGame()
    {
        settingPopup.SetActive(true);
        Time.timeScale = 0;
        isGamePaused = true;
    }

    void ResumeGame()
    {
        settingPopup.SetActive(false);
        Time.timeScale = 1;
        isGamePaused = false;
    }
    public void Main()
    {
        PhotonNetwork.LeaveRoom();
        SceneManager.LoadScene(0);
    }
    public void Exit()
    {
        Application.Quit();
    }
    #endregion
    //
    #region PickUpItems
    void UseBandage()
    {
        int BandageAmount = 1;
        if (Bandage >= BandageAmount)
        {
            Bandage -= BandageAmount;
            _health += 50;

            if (_health > 100)
            {
                _health = 100;
            }
            if (!photonView.IsMine) return;
            {
                SetHashes();
            }
        }
        _cameraBloodEffect.minBloodAmount = (1.0f - _health / 100.0f);
        _cameraBloodEffect.bloodAmount = Mathf.Min(_cameraBloodEffect.minBloodAmount + 0.05f, 0.5f);
        addbandageText.text = "+50Hp";
        addbandageText.color = Color.green;
        StartCoroutine(HideAndShowText(3));
    }
    [PunRPC]
    void GetBandage(int item)
    {
        Bandage += item;
    }
    void UpdateBandageText(int amount)
    {
        bandageText.text = "" + amount;
    }
    private IEnumerator HideAndShowText(float delay)
    {
        yield return new WaitForSeconds(delay);

        addbandageText.gameObject.SetActive(false);

        yield return new WaitForSeconds(delay);
        addbandageText.text = "";
        addbandageText.gameObject.SetActive(true);
    }
    private IEnumerator StartCooldown()
    {
        isCooldown = true;
        float cooldownTimer = cooldownDuration;

        while (cooldownTimer > 0)
        {
            cooldownTimer -= Time.deltaTime;
            float fillAmount = cooldownTimer / cooldownDuration;
            cooldownImage.fillAmount = fillAmount;

            int cooldownTime = Mathf.CeilToInt(cooldownTimer);
            addbandagecooldownText.text = "" + cooldownTime.ToString();

            yield return null;
        }
        isCooldown = false;
        addbandagecooldownText.text = "";
    }
    #endregion
    //
}
