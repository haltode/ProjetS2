﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

public class Player
{
    public HexGrid hexGrid;
    public Client client;

    public bool isDead;

    public int[] unitLevel;

    public int money;
    public int science;
    public int[] resources;

    public List<Unit> playerUnits;
    public List<City> playerCities;
    public List<Resource> playerResources;

    public Color color;

    public bool canPlay;
    public string name;

    public float economyMalus;

    public DisplayInformationManager displayer;

    public Color[] PlayerColors = { Color.blue, Color.red, Color.green, Color.yellow, Color.black, Color.white, Color.magenta, Color.cyan };

    public Player(string name)
    {
        this.name = name;
        unitLevel = new int[] {1, 1, 1};

        money = 1000;
        science = 0;
        resources = new int[] {200, 0, 0, 0, 500, 500}; // Iron, gold, Diamond, Horses, Wood, Food
        playerUnits = new List<Unit>();
        playerCities = new List<City>();
        playerResources = new List<Resource>();

        canPlay = false;
        client = Object.FindObjectOfType<Client>();
        hexGrid = Object.FindObjectOfType<HexGrid>();

        economyMalus = /*(GameManager.Instance.gamemode == GameManager.Gamemode.SOLO ? 0.8f : */1f/*)*/; //Deactivated until more stable economy. Base value = 0.8f
    }


    //UNIT
    ///////////////////////
    public void InitialSpawnUnit(string settler, string attacker, bool focus)
    {
        hexGrid = Object.FindObjectOfType<HexGrid>();
        NetworkAddUnit(settler);
        NetworkAddUnit(attacker);

        if(!focus)
            return;

        string[] settlerT = settler.Split('#');
        HexCell cell = hexGrid.GetCell(new HexCoordinates(int.Parse(settlerT[1]), int.Parse(settlerT[2])));

        HexMapCamera.FocusOnPosition(cell.Position);
    }

    public void AddUnit(Unit.UnitType type, HexCell location)
    {
        client.Send("CUNI|UNC|" + (int)type + "#" + location.coordinates.X + "#" + location.coordinates.Z + "#" + GetUnitLevel(type));
    }

    public void NetworkAddUnit(string data)
    {
        hexGrid = Object.FindObjectOfType<HexGrid>();
        string[] receivedData = data.Split('#');

        Unit.UnitType type = (Unit.UnitType)int.Parse(receivedData[0]);
        HexCell location = hexGrid.GetCell(new HexCoordinates(int.Parse(receivedData[1]), int.Parse(receivedData[2])));
        Unit unit = null;

        if(location.HasCity && (type == Unit.UnitType.REGULAR || type == Unit.UnitType.RANGED ||
            type == Unit.UnitType.HEAVY))
            UnityEngine.Object.FindObjectOfType<SoundManager>().PlayNewAttacker();

        if(type == Unit.UnitType.SETTLER)
            unit = new Settler(this);
        else if(type == Unit.UnitType.WORKER)
            unit = new Worker(this);
        else if(type == Unit.UnitType.REGULAR)
        {
            Regular regular = new Regular(this);
            regular.Level = int.Parse(receivedData[3]);
            if(regular.Level > 10)
                type = (Unit.UnitType)((int)type + 4);
            unit = regular;
        }
        else if(type == Unit.UnitType.RANGED)
        {
            Ranged ranged = new Ranged(this);
            ranged.Level = int.Parse(receivedData[3]);
            if(ranged.Level > 10)
                type = (Unit.UnitType)((int)type + 4);
            unit = ranged;
        }
        else if(type == Unit.UnitType.HEAVY)
        {
            Heavy heavy = new Heavy(this);
            heavy.Level = int.Parse(receivedData[3]);
            if(heavy.Level > 10)
                type = (Unit.UnitType)((int)type + 4);
            unit = heavy;
        }
        else
            Debug.Log("Unknown unit type");

        unit.hexGameObject = Object.Instantiate(hexGrid.mainUnitPrefab);
        unit.HexUnit = unit.hexGameObject.GetComponent<HexUnit>();
        unit.HexUnit.Unit = unit;
        unit.SetGraphics(hexGrid.unitPrefab[(int)type]);

        float orientation = UnityEngine.Random.Range(0f, 360f);
        hexGrid.AddUnit(unit.HexUnit, location, orientation);
        unit.embark = false;

        playerUnits.Add(unit);
        if (location.HasCity)
            ((City)(location.Feature)).spawncount++;
    }

    public void NetworkTakeDamageUnit(string data)
    {
        hexGrid = Object.FindObjectOfType<HexGrid>();
        string[] receivedData = data.Split('#');
        HexCell attackedCell = hexGrid.GetCell(new HexCoordinates(int.Parse(receivedData[0]), int.Parse(receivedData[1])));
        Unit unit = attackedCell.Unit.Unit;

        unit.Hp -= int.Parse(receivedData[2]);

        if (unit.Hp <= 0)
        {
            unit.anim.animator.SetTrigger("DeathTrigger");
            RemoveUnit(unit);
        }
    }

    public void RemoveUnit(Unit unit)
    {
        hexGrid.RemoveUnit(unit.HexUnit);
        playerUnits.Remove(unit);
        unit = null;
    }

    public void MoveUnit(Unit unit, HexCell end)
    {
        int xStart = unit.HexUnit.location.coordinates.X;
        int zStart = unit.HexUnit.location.coordinates.Z;
        int xEnd = end.coordinates.X;
        int zEnd = end.coordinates.Z;

        client.Send("CMOV|" + xStart + "#" + zStart + "#" + xEnd + "#" + zEnd);
    }

    public void NetworkMoveUnit(string data, bool isAI=false)
    {
        hexGrid = Object.FindObjectOfType<HexGrid>();
        string[] receivedData = data.Split('#');

        int xStart = int.Parse(receivedData[0]);
        int zStart = int.Parse(receivedData[1]);

        int xEnd = int.Parse(receivedData[2]);
        int zEnd = int.Parse(receivedData[3]);

        HexCell cellStart = hexGrid.GetCell(new HexCoordinates(xStart, zStart));
        HexCell cellEnd = hexGrid.GetCell(new HexCoordinates(xEnd, zEnd));

        hexGrid.ClearPath();
        hexGrid.FindPath(cellStart, cellEnd, cellStart.Unit, isAI);

        if(hexGrid.HasPath)
            cellStart.Unit.Travel(hexGrid.GetPath());

        hexGrid.ClearPath();
    }

    public void LevelUp(Unit.UnitType type)
    {
        if(!Unit.CanAttack(type))
            return;

        if(unitLevel[(int)type - 2] < 20)
            client.Send("CUNI|LVP|" + ((int)type - 2));
    }

    public void NetworkLevelUp(string type)
    {
        unitLevel[int.Parse(type)]++;

        foreach(Unit u in playerUnits)
        {
            if(u.Type == Unit.UnitType.REGULAR && type == "0")
                ((Regular)u).NetworkLevelUp();
            else if(u.Type == Unit.UnitType.RANGED && type == "1")
                ((Ranged)u).NetworkLevelUp();
            else if(u.Type == Unit.UnitType.HEAVY && type == "2")
                ((Heavy)u).NetworkLevelUp();
        }
    }

    public Unit GetUnit(HexCell location)
    {
        for(int i = 0; i < playerUnits.Count; ++i)
            if(playerUnits[i].HexUnit.location == location)
                return playerUnits[i];
        return null;
    }


    //CITY
    ///////////////////////
    public void AddCity(HexCell location, City.CitySize type)
    {
        client.Send("CUNI|CIC|" + (int)type + "#" + location.coordinates.X + "#" + location.coordinates.Z);
    }

    public void NetworkAddCity(string data)
    {
        UnityEngine.Object.FindObjectOfType<SoundManager>().PlayNewCity();
        hexGrid = Object.FindObjectOfType<HexGrid>();
        string[] receivedData = data.Split('#');

        City.CitySize size = (City.CitySize)int.Parse(receivedData[0]);
        HexCell location = hexGrid.GetCell(new HexCoordinates(int.Parse(receivedData[1]), int.Parse(receivedData[2])));
        location.FeatureIndex = 1;
        location.IncreaseVisibility();

        City city = new City(this, location);
        city.Size = size;
        location.Feature = city;
        location.Feature.Type = Feature.FeatureType.CITY;

        playerCities.Add(city);
    }


    public void NetworkTakeDamageCity(string data)
    {
        hexGrid = Object.FindObjectOfType<HexGrid>();
        string[] receivedData = data.Split('#');
        HexCell attackedCell = hexGrid.GetCell(new HexCoordinates(int.Parse(receivedData[0]), int.Parse(receivedData[1])));
        City city = (City)attackedCell.Feature;
        //city.happiness *= 0.9f;
        city.Hp -= int.Parse(receivedData[2]);

        if(city.Hp <= 0)
            RemoveCity(city);
    }

    public void RemoveCity(City city)
    {
        HexCell location = city.Location;
        location.FeatureIndex = 0;
        playerCities.Remove(city);
        location.Feature = null;
        city = null;
    }

    public City GetCity(HexCell location)
    {
        for(int i = 0; i < playerCities.Count; ++i)
            if(playerCities[i].Location == location)
                return playerCities[i];
        return null;
    }


    //RESOURCES
    ///////////////////////
    public void NetworkTakeDamageResource(string data)
    {
        hexGrid = Object.FindObjectOfType<HexGrid>();
        string[] receivedData = data.Split('#');
        HexCell attackedCell = hexGrid.GetCell(new HexCoordinates(int.Parse(receivedData[0]), int.Parse(receivedData[1])));
        Resource resource = (Resource)attackedCell.Feature;
        resource.Hp -= int.Parse(receivedData[2]);

        if(resource.Hp <= 0)
            ResetResource(resource);
    }

    public void ResetResource(Resource resource)
    {
        resource.Location.FeatureIndex -= 6;
        resource.Owner = null;
        resource.Hp = resource.MaxHp;
        playerResources.Remove(resource);
    }


    public void SetDisplayer()
    {
        displayer = Object.FindObjectOfType<DisplayInformationManager>();
    }

    public int GetUnitLevel(Unit.UnitType type)
    {
        switch(type)
        {
            case Unit.UnitType.HEAVY:
                return unitLevel[2];

            case Unit.UnitType.RANGED:
                return unitLevel[1];

            case Unit.UnitType.REGULAR:
                return unitLevel[0];

            default:
                return 0;
        }
    }

    public bool CheckStillAliiiiiiive()
    {
        bool alive = false;
        foreach(Unit u in playerUnits)
            if(u != null)
            {
                alive = true;
                break;
            }
        if(alive)
            return true;

        foreach(City c in playerCities)
            if(c != null)
            {
                alive = true;
                break;
            }

        return alive;
    }

    public void Newturn(bool isPlayer)
    {
        hexGrid = Object.FindObjectOfType<HexGrid>();

        if(isPlayer)
        {
            string cityUpgrade = "";
            foreach(City c in playerCities)
                cityUpgrade += c.Update();
            if(cityUpgrade != "")
                client.Send("CUNI|CUP" + cityUpgrade);
        }

        foreach (Unit u in playerUnits)
            u.Update();

        foreach (Resource r in playerResources)
            r.Update();

        isDead = !CheckStillAliiiiiiive();
    }
}
