using UnityEngine;

public static class PlayerFinder
{
    private const string PlayerTag = "Player";

    public static Transform FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag(PlayerTag);
        return player != null ? player.transform : null;
    }

    public static GameObject FindPlayerObject()
    {
        return GameObject.FindGameObjectWithTag(PlayerTag);
    }

    public static bool TryAssignIfNull(ref Transform target)
    {
        if (target != null)
            return true;

        target = FindPlayer();
        return target != null;
    }

    public static T GetPlayerComponent<T>() where T : Component
    {
        GameObject player = FindPlayerObject();
        return player != null ? player.GetComponent<T>() : null;
    }
}