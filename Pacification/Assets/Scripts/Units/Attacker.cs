﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;

public class Attacker : Unit
{
    protected int upgradeHP;
    protected int defaultATK;
    protected int upgradeATK;
    protected int range;

    protected Dictionary<Unit.UnitType, float> dmgMult;
    protected float dmgMultCity;

    public int UpgradeHP
    {
        get { return upgradeHP; }
        set { upgradeHP = value; }
    }

    public int DefaultATK
    {
        get { return defaultATK; }
        set { defaultATK = value; }
    }

    public int UpgradeATK
    {
        get { return upgradeATK; }
        set { upgradeATK = value; }
    }

    public int Range
    {
        get { return range; }
        set { range = value; }
    }

    public bool IsUpgraded()
    {
        return level > 10;
    }

    public bool IsMaxed()
    {
        return level == (IsUpgraded() ? 20 : 10);
    }

    public bool IsInRangeToAttack(HexCell target)
    {
        return HexUnit.location.coordinates.DistanceTo(target.coordinates) <= range;
    }
    
    public bool Attack(Unit target)
    {
        if (hasMadeAction)
            return false;

        float multiplier = 1f;
        dmgMult.TryGetValue(target.Type, out multiplier);
        int damage = (int)((float)((defaultATK - upgradeATK) + upgradeATK * level) * multiplier);

        Owner.client.Send("CUNI|UTD|" + HexUnit.Location.coordinates.X + "#" + HexUnit.Location.coordinates.Z + "|" + target.HexUnit.location.coordinates.X + "#" + target.HexUnit.location.coordinates.Z + "#" + damage + "|" + target.Owner.name);

        hasMadeAction = true;
        return true;
    }

    public bool Attack(City target)
    {
        if (hasMadeAction)
            return false;

        int damage = (int)((float)((defaultATK - upgradeATK) + upgradeATK * level) * dmgMultCity);

        Owner.client.Send("CUNI|CTD|" + HexUnit.Location.coordinates.X + "#" + HexUnit.Location.coordinates.Z + "|" + target.Location.coordinates.X + "#" + target.Location.coordinates.Z + "#" + damage + "|" + target.Owner.name);

        hasMadeAction = true;
        return true;
    }

    public bool Attack(Resource target)
    {
        if(hasMadeAction)
            return false;

        int damage = (int)((float)((defaultATK - upgradeATK) + upgradeATK * level) * dmgMultCity);

        Owner.client.Send("CUNI|RTD|" + HexUnit.Location.coordinates.X + "#" + HexUnit.Location.coordinates.Z + "|" + target.Location.coordinates.X + "#" + target.Location.coordinates.Z + "#" + damage + "|" + target.Owner.name);
        hasMadeAction = true;
        return true;
    }
}