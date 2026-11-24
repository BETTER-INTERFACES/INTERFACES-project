using UnityEngine;

/// <summary>
/// Logique de placement commune aux panneaux World Space (TooltipManager, RadialMenuAnchor).
/// Trois etapes :
/// 1. ComputeAnchor : point d'ancrage sur l'objet cible.
/// 2. OffsetFromAnchor : decalage et poussee vers l'interieur de l'objet (cote oppose a la camera).
/// 3. KeepComfortable : si le panneau est trop pres du joueur, ou trop haut par rapport a son regard,
///    il recule horizontalement au lieu de monter dans le champ de vision.
/// </summary>
public static class WorldPanelPlacement
{
    /// <summary>
    /// Point d'ancrage sur la cible : point le plus proche de la camera, ou dessus de l'objet.
    /// </summary>
    public static Vector3 ComputeAnchor(Transform target, Collider col, Renderer rend, Vector3 camPos, bool topOfBounds)
    {
        if (topOfBounds)
        {
            if (TryGetBounds(col, rend, out Bounds b))
                return new Vector3(b.center.x, b.max.y, b.center.z);
        }
        else
        {
            if (col != null)
            {
                // ClosestPoint ne supporte que certains colliders, sinon Unity renvoie une erreur
                return SupportsClosestPoint(col)
                    ? col.ClosestPoint(camPos)
                    : col.bounds.ClosestPoint(camPos);
            }
            if (rend != null) return rend.bounds.ClosestPoint(camPos);
        }

        return target.position;
    }

    /// <summary>
    /// Ajoute le decalage et pousse le point horizontalement, dans la direction opposee a la camera.
    /// pushAway positif = vers l'interieur de l'objet.
    /// </summary>
    public static Vector3 OffsetFromAnchor(Vector3 anchor, Vector3 offset, float pushAway, Vector3 camPos, Vector3 camForward)
    {
        Vector3 dir = FlatDirection(anchor - camPos, camForward);
        return anchor + offset + dir * pushAway;
    }

    /// <summary>
    /// Fait reculer le point (horizontalement, depuis la camera) s'il est :
    /// - plus pres que minDistance du joueur ;
    /// - au dessus de l'angle maxElevationDeg par rapport a l'horizontale du regard.
    /// La hauteur ne change jamais : le panneau recule, il ne monte pas.
    /// maxDistance (0 = illimite) plafonne le recul, sans jamais rapprocher le point.
    /// </summary>
    public static Vector3 KeepComfortable(Vector3 point, Vector3 camPos, Vector3 camForward,
        float minDistance, float maxElevationDeg, float maxDistance)
    {
        Vector3 flat = point - camPos;
        flat.y = 0f;
        float dist = flat.magnitude;

        float required = Mathf.Max(0f, minDistance);

        // Plus le panneau est haut au dessus des yeux, plus il doit etre loin pour rester dans un angle confortable
        float rise = point.y - camPos.y;
        if (maxElevationDeg > 0f && rise > 0f)
        {
            float tan = Mathf.Tan(Mathf.Min(maxElevationDeg, 85f) * Mathf.Deg2Rad);
            required = Mathf.Max(required, rise / tan);
        }

        if (maxDistance > 0f)
        {
            required = Mathf.Min(required, Mathf.Max(maxDistance, minDistance));
        }

        if (dist >= required) return point;

        Vector3 dir = FlatDirection(flat, camForward);
        return new Vector3(camPos.x + dir.x * required, point.y, camPos.z + dir.z * required);
    }

    // ---------------------------------------------------------------
    // Utilitaires
    // ---------------------------------------------------------------

    /// <summary>Direction horizontale normalisee (repli : direction du regard, puis Vector3.forward).</summary>
    private static Vector3 FlatDirection(Vector3 v, Vector3 fallbackForward)
    {
        v.y = 0f;
        if (v.sqrMagnitude < 0.0001f)
        {
            v = fallbackForward;
            v.y = 0f;
        }
        return v.sqrMagnitude < 0.0001f ? Vector3.forward : v.normalized;
    }

    private static bool TryGetBounds(Collider col, Renderer rend, out Bounds bounds)
    {
        if (col != null) { bounds = col.bounds; return true; }
        if (rend != null) { bounds = rend.bounds; return true; }
        bounds = default;
        return false;
    }

    private static bool SupportsClosestPoint(Collider c)
    {
        return c is BoxCollider
            || c is SphereCollider
            || c is CapsuleCollider
            || (c is MeshCollider mc && mc.convex);
    }
}
