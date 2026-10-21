using UnityEngine;

[CreateAssetMenu(fileName = "NouvelleAction", menuName = "NBSS/Action Data")]
public class SwaleActionData : ScriptableObject
{
    [Header("Identifiant")]
    public string actionName; // Ex: "Tondre", "Paillage", "Panneau"

    [Header("Paramètres Gameplay")]
    public float scoreDelta;          // Ex: -15 ou +15
    public string gamaCommandSuffix;  // Ex: "-15" ou "5"

    [Header("Feedback Joueur")]
    [TextArea(2, 5)]
    public string messageTexte;
    public float messageDuration = 7f;
    public Color messageColor = Color.green;
} 