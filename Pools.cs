using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AeternalEverwatcher;

public static class Pools
{
    public static GameObject pooledObjectsParent = new("Pooled Objects Parent");
    private static List<GameObject> spears = [];
    private static List<GameObject> windSlashes = [];
    private static List<GameObject> sandWaves = [];
    private static List<GameObject> groundWaves = [];
    private static List<GameObject> sandBurstSmalls = [];
    private static List<GameObject> pcrBursts = [];

    public static GameObject GetSpear() => GetPooledObject(spears, CustomBehaviour.khannUcSpearSetup);
    public static GameObject GetWindSlash() => GetPooledObject(windSlashes, CustomBehaviour.skProjectileSetup);
    public static GameObject GetSandWave() => GetPooledObject(sandWaves, CustomBehaviour.sandburst);
    public static GameObject GetGroundWave() => GetPooledObject(groundWaves, CustomBehaviour.groundWave);
    public static GameObject GetSandBurstSmall() => GetPooledObject(sandBurstSmalls, CustomBehaviour.sandburstSmall);
    public static GameObject GetPcrBurst() => GetPooledObject(pcrBursts, CustomBehaviour.pcrBurst);

    public static void PrewarmSpears()
    {
        for (var i = 0; i < 15; i++) AddToPool(spears, CustomBehaviour.khannUcSpearSetup);
    }
    private static GameObject GetPooledObject(List<GameObject> pool, GameObject setup)
    {
        GameObject clone = null!;
        var found = false;
        foreach (var obj in pool.Where(obj => !setup.name.Equals("spear") ? !obj.activeInHierarchy : !obj.transform.GetChild(0).gameObject.activeInHierarchy))
        {
            clone = obj;
            found = true;
            break;
        }
        return found ? clone! : AddToPool(pool, setup);
    }
    private static GameObject AddToPool(List<GameObject> pool, GameObject setup)
    {
        var clone = Object.Instantiate(setup, pooledObjectsParent.transform);
        clone.name += "_POOLED";
        pool.Add(clone);
        return clone;
    }

    public static void Clear()
    {
        pooledObjectsParent = new GameObject("Pooled Objects Parent");
        Object.DontDestroyOnLoad(pooledObjectsParent);
        spears.Clear();
        windSlashes.Clear();
        sandWaves.Clear();
        groundWaves.Clear();
        sandBurstSmalls.Clear();
        pcrBursts.Clear();
    }
}