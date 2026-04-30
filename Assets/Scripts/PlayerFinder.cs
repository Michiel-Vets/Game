using UnityEngine;

/// <summary>
/// Statische hulpklasse om de speler op te zoeken via tag.
/// Centraliseert de herhaalde FindGameObjectWithTag("Player") logica.
/// </summary>
public static class PlayerFinder
{
    private const string PlayerTag = "Player";

    /// <summary>Geeft de Transform van de speler terug, of null als niet gevonden.</summary>
    public static Transform FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag(PlayerTag);
        return player != null ? player.transform : null;
    }

    /// <summary>Geeft het GameObject van de speler terug, of null als niet gevonden.</summary>
    public static GameObject FindPlayerObject()
    {
        return GameObject.FindGameObjectWithTag(PlayerTag);
    }

    /// <summary>
    /// Vult een Transform-referentie in als die nog null is.
    /// Geeft true terug als de referentie na de aanroep ingevuld is.
    /// </summary>
    public static bool TryAssignIfNull(ref Transform target)
    {
        if (target != null)
            return true;

        target = FindPlayer();
        return target != null;
    }

    /// <summary>
    /// Haalt een component op van de speler.
    /// Geeft null terug als de speler of het component niet gevonden wordt.
    /// </summary>
    public static T GetPlayerComponent<T>() where T : Component
    {
        GameObject player = FindPlayerObject();
        return player != null ? player.GetComponent<T>() : null;
    }
}