﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ButtonManager : MonoBehaviour {

    public GameObject leftPannel;
    public GameObject rightPannel;
    public GameObject activePlayer;
    public ControlsManager controls;

    public bool endTurn = false;

    private Client client;

    void Start()
    {
        controls = FindObjectOfType<ControlsManager>();
        client = FindObjectOfType<Client>();
    }

    public void EndTurnButton()
    {
        if(client.player.canPlay)
        {
            client.Send("CEND|" + client.clientName);
            client.player.canPlay = false;
            activePlayer.SetActive(false);
            foreach (GameObject g in client.DeadList)
                Destroy(g);
            client.DeadList.Clear();
        }
    }

    public void TakeTurn()
    {
        client.player.canPlay = true; ;
        activePlayer.SetActive(true);
    }

    public void DeconnectionButton()
    {
        Server server = FindObjectOfType<Server>();
        if(server != null)
        {
            if(GameManager.Instance.gamemode == GameManager.Gamemode.MULTI)
                server.Broadcast("SKIK|The Host left the game", server.clients);
            server.server.Stop();
            Destroy(server.gameObject);
        }

        client = FindObjectOfType<Client>();
        if(client != null)
            Destroy(client.gameObject);

        Destroy(GameObject.Find("GameManager"));
        Destroy(GameObject.Find("HexMapGenerator"));

        SceneManager.LoadScene("Menu");
    }
}
