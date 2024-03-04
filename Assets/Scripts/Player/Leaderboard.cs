﻿using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine.UI;

public class Leaderboard : MonoBehaviourPun
{
    [Header("LeaderBoard")]
    public float refreshRate = 0.5f;
    public GameObject[] slots;

    [Space]
    public TextMeshProUGUI[] namePlayerText;
    public Slider[] sliderhealthPlayer;
    public TextMeshProUGUI[] healthPlayerText;
    public TextMeshProUGUI[] armorrPlayerText;
    public Canvas canvas;
    public PlayerController me;
    private void Start()
    {
        if (!photonView.IsMine)
        {
            canvas.enabled = false;
        }
    }
    private void Update()
    {
        InvokeRepeating(nameof(Refresh), 0.5f, refreshRate);
    }
    public void Refresh()
    {
        Player[] players = PhotonNetwork.PlayerList;
        List<Player> sortedPlayers = players.OrderBy(p => p.ActorNumber).ToList();

        for (int i = 0; i < slots.Length; i++)
        {
            if (i < sortedPlayers.Count)
            {
                namePlayerText[i].text = sortedPlayers[i].NickName;

                if (sortedPlayers[i].CustomProperties.ContainsKey("Health") && sortedPlayers[i].CustomProperties.ContainsKey("Armorr"))
                {
                    float health = (float)sortedPlayers[i].CustomProperties["Health"];
                    int armor = (int)sortedPlayers[i].CustomProperties["Armorr"];
                    if (sliderhealthPlayer[i].value != health/me.maxHP)
                    {
                        sliderhealthPlayer[i].value = health/ me.maxHP;
                        healthPlayerText[i].text = health.ToString();
                        armorrPlayerText[i].text = armor.ToString();
                    }
                }
                else
                {
                    sliderhealthPlayer[i].value = 0f;
                    healthPlayerText[i].text = "";
                    armorrPlayerText[i].text = "";
                }

                sliderhealthPlayer[i].gameObject.SetActive(true);
            }
            else
            {
                namePlayerText[i].text = "";
                sliderhealthPlayer[i].gameObject.SetActive(false);
                healthPlayerText[i].text = "";
                armorrPlayerText[i].text = "";
            }
        }
    }
}