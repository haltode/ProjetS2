﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Worker : Unit
{
    public Worker(Player owner)
    {
        Owner = owner;
        type = UnitType.WORKER;
        hasMadeAction = false;
        mvtSPD = 3;
        currMVT = 0;
        hp = 300;
        MaxHP = hp;

        // TODO : couleur du joueur
    }

    public void Exploit()
    {
        if (hasMadeAction)
            return;

        HexCell cell = HexUnit.location;
        if(cell.FeatureIndex <= 3)
            return;

        if(cell.FeatureIndex + 6 < 16)
            Owner.client.Send("CUNI|WEX|" + cell.coordinates.X + "#" + cell.coordinates.Z + "#" + (cell.FeatureIndex - 4));
        hasMadeAction = true;
    }

    public bool AddRoad(HexCell roadCell)
    {
        HexCell currentCell = HexUnit.location;
        bool isNeighbor = false;

        HexDirection roadDir = HexDirection.NE;
        for(HexDirection dir = HexDirection.NE; dir <= HexDirection.NW && !isNeighbor; ++dir)
        {
            HexCell neighbor = currentCell.GetNeighbor(dir);
            if(neighbor == roadCell)
            {
                isNeighbor = true;
                roadDir = dir;
            }
        }
        if(!isNeighbor || !currentCell.IsReachable(roadDir))
            return false;

        currentCell.AddRoad(roadDir);
        return true;
    }
}