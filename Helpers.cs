using System;
using System.Collections;
using System.Linq;
using Silksong.FsmUtil;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AeternalEverwatcher;

public static class Helpers
{
    public static float GetPosDiffSpeed() => Mathf.Clamp(Math.Abs(HeroController.instance.transform.position.x - AeternalEverwatcherPlugin.transform.position.x) * 30, 230, 270);
    public static bool ObjLeftOfHornet(GameObject go) => go.transform.position.x < HeroController.instance.transform.position.x;
    public static void MakeProjectileIgnoreEnvironment(GameObject projectile)
    {
        var colliders = projectile.GetComponentsInChildren<Collider2D>(true);
        if (colliders == null || colliders.Length == 0) return;
        foreach (var col in colliders)
        {
            int environmentLayer = LayerMask.NameToLayer("Terrain");
            if (environmentLayer >= 0) Physics2D.IgnoreLayerCollision(projectile.layer, environmentLayer, true);
            col.isTrigger = true;
        }
    }
    public static void RemoveProjectileWallEvents(GameObject projectile)
    {
        var fsm = projectile.LocateMyFSM("Control");
        foreach (var state in fsm.FsmStates)
        {
            var newTransitions = state.Transitions
                .Where(t => !t.EventName.Equals("WALL", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (newTransitions.Length != state.Transitions.Length) state.Transitions = newTransitions;
        }
        foreach (var stateName in new[] { "Wall End", "Floor?" })
        {
            var s = fsm.GetState(stateName);
            if (s != null)
            {
                s.Transitions = [];
                s.Actions = [];
            }
        }
    }
    public static IEnumerator DestroyLater(GameObject go, float delay)
    {
        yield return new WaitForSeconds(delay);
        Object.Destroy(go);
    }
    public static void SandColorSetup(GameObject wave)
    {
        foreach (var pt in wave.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (pt.name is "sand_blown" or "particles_small") continue;
            var main = pt.main;
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white);
            var renderer = pt.GetComponent<ParticleSystemRenderer>();
            if (renderer && renderer.material)
            {
                renderer.material.color = Color.white;
                renderer.material.SetColor(Color1, new Color(2.5f, 2.5f, 2.5f, 1f));
            }
        }
    }
    public static void SandSpeedSetup(GameObject wave, float velLimit = 2.0f)
    {
        foreach (var pt in wave.GetComponentsInChildren<ParticleSystem>(true))
        {
            var limitVelocity = pt.limitVelocityOverLifetime;
            limitVelocity.enabled = true;
            limitVelocity.limit = velLimit;
            limitVelocity.dampen = 0.5f;
        }
    }
    public static IEnumerator FinishStateEarly(string eventName, float delay)
    {
        yield return new WaitForSeconds(delay);
        AeternalEverwatcherPlugin.controlFsm.SendEvent(eventName);
    }
    private static readonly int Color1 = Shader.PropertyToID("_Color");
}