﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class HexUnit : MonoBehaviour
{
    public Unit Unit;

    public HexCell location;
    HexCell currentTravelLocation;
    float orientation;

    List<HexCell> pathToTravel;
    const float TravelSpeed = 2f;
    const float RotationSpeed = 280f;

    public int Speed
    {
        get { return Unit.MvtSPD; }
    }

    public int VisionRange
    {
        // Temporary value
        get { return 3; }
    }

    public HexGrid Grid { get; set; }

    public HexCell Location
    {
        get { return location; }
        set
        {
            if(location)
            {
                Grid.DecreaseVisibility(location, VisionRange);
                location.Unit = null;
            }
            location = value;
            value.Unit = this;
            Grid.IncreaseVisibility(value, VisionRange);
            transform.localPosition = value.Position;
        }
    }

    public float Orientation
    {
        get { return orientation; }
        set
        {
            orientation = value;
            transform.localRotation = Quaternion.Euler(0f, value, 0f);
        }
    }

    void OnEnable()
    {
        if(location)
        {
            transform.localPosition = location.Position;
            if(currentTravelLocation)
            {
                Grid.IncreaseVisibility(location, VisionRange);
                Grid.DecreaseVisibility(currentTravelLocation, VisionRange);
                currentTravelLocation = null;
            }
        }
    }

    public void Die()
    {
        if(location)
        {
            Grid.DecreaseVisibility(location, VisionRange);
            location.DisableHighlight();
        }
        location.Unit = null;
        if (Unit.Type == Unit.UnitType.SETTLER || Unit.Type == Unit.UnitType.HEAVY || Unit.embark)
            Destroy(gameObject);
        else
            FindObjectOfType<Client>().DeadList.Add(gameObject);
    }

    public void ValidateLocation()
    {
        transform.localPosition = location.Position;
    }

    public void Save(BinaryWriter writer)
    {
        location.coordinates.Save(writer);
        writer.Write(orientation);
    }

    public static void Load(BinaryReader reader, HexGrid grid)
    {
        /* TODO: need to save player information to be able to recreate unit
        HexCoordinates coordinates = HexCoordinates.Load(reader);
        float orientation = reader.ReadSingle();
        grid.AddUnit(Instantiate(unitPrefab), grid.GetCell(coordinates), orientation);
        */
    }

    public int GetMoveCost(HexCell current, HexCell dest)
    {
        HexDirection dir = current.GetNeighborDir(dest);
        if(!current.IsReachable(dir))
            return -1;

        // Road and flat terrains are faster than cliffs
        int moveCost;
        if(current.GetElevationDifference(dir) == 0)
            moveCost = 1;
        else
            moveCost = 2;

        if(current.HasRoadThroughEdge(dir))
            moveCost = 1;
        if(current.TerrainBiomeIndex == (int)HexCell.BiomeType.ROCKY ||
            current.FeatureIndex == (int)HexCell.FeatureType.FOREST)
            moveCost = 2;
        return moveCost;
    }

    public void Travel(List<HexCell> path)
    {
        if(path.Count <= 1)
            return;
        List<HexCell> actualPath = new List<HexCell>();
        actualPath.Add(path[0]);
        for(int i = 1; i < path.Count; ++i)
        {
            int cost = GetMoveCost(path[i - 1], path[i]);
            if(Unit.currMVT + cost > Unit.MvtSPD)
                break;
            Unit.currMVT += cost;
            actualPath.Add(path[i]);
        }
        location.Unit = null;
        location = actualPath[actualPath.Count - 1];
        location.Unit = this;
        pathToTravel = actualPath;
        StopAllCoroutines();
        StartCoroutine(TravelPath());
    }

    IEnumerator TravelPath()
    {
        Vector3 a, b, c = pathToTravel[0].Position;
        yield return LookAt(pathToTravel[1].Position);
        Grid.DecreaseVisibility(
            currentTravelLocation ? currentTravelLocation : pathToTravel[0],
            VisionRange);

        Unit.anim.animator.SetTrigger("WalkTrigger");
        float t = Time.deltaTime * TravelSpeed;
        for(int i = 1; i < pathToTravel.Count; ++i)
        {
            currentTravelLocation = pathToTravel[i];
            a = c;
            b = pathToTravel[i - 1].Position;
            c = (b + currentTravelLocation.Position) * 0.5f;
            Grid.IncreaseVisibility(pathToTravel[i], VisionRange);
            for(; t < 1f; t += Time.deltaTime * TravelSpeed)
            {
                if (i == pathToTravel.Count - 1 && t > 0.6f)
                    Unit.anim.animator.SetTrigger("IdleTrigger");
                transform.localPosition = Bezier.GetPoint(a, b, c, t);
                Vector3 d = Bezier.GetDerivative(a, b, c, t);
                d.y = 0f;
                transform.localRotation = Quaternion.LookRotation(d);
                yield return null;
            }

            // Embark on water
            if(location.IsUnderWater && !Unit.embark && Unit.CanEmbark)
            {
                Unit.embark = true;
                Unit.SetGraphics(Grid.unitPrefab[5]);
            }
            // Embark on land
            else if(!location.IsUnderWater && Unit.embark)
            {
                Unit.embark = false;
                int prefabIndex = (int)Unit.Type;
                if(Unit.Level > 10)
                    prefabIndex += 4;
                Unit.SetGraphics(Grid.unitPrefab[prefabIndex]);
            }

            Grid.DecreaseVisibility(pathToTravel[i], VisionRange);
            t -= 1f;
        }
        currentTravelLocation = null;

        a = c;
        b = location.Position;
        c = b;
        Grid.IncreaseVisibility(location, VisionRange);
        for(; t < 1f; t += Time.deltaTime * TravelSpeed)
        {
            transform.localPosition = Bezier.GetPoint(a, b, c, t);
            Vector3 d = Bezier.GetDerivative(a, b, c, t);
            d.y = 0f;
            transform.localRotation = Quaternion.LookRotation(d);
            yield return null;
        }

        transform.localPosition = location.Position;
        orientation = transform.localRotation.eulerAngles.y;
    
        ListPool<HexCell>.Add(pathToTravel);
        pathToTravel = null;
    }

    IEnumerator LookAt(Vector3 point)
    {
        point.y = transform.localPosition.y;
        Quaternion fromRotation = transform.localRotation;
        Quaternion toRotation = Quaternion.LookRotation(point - transform.localPosition);
        float angle = Quaternion.Angle(fromRotation, toRotation);
        if(angle > 0f)
        {
            float speed = RotationSpeed / angle;
            for(float t = Time.deltaTime * speed; t < 1f; t += Time.deltaTime * speed)
            {
                transform.localRotation = Quaternion.Slerp(fromRotation, toRotation, t);
                yield return null;
            }

            transform.LookAt(point);
            orientation = transform.localRotation.eulerAngles.y;
        }
    }

    public void Rotation(HexCell neighbor)
    {
        if(location == neighbor)
            return;
        StopAllCoroutines();
        StartCoroutine(LookAt(neighbor.Position));
    }
}