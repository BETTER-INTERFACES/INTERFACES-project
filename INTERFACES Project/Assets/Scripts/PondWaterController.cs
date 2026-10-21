using UnityEngine;

public class PondWaterController : MonoBehaviour
{
    [SerializeField] private Renderer waterRenderer;
    [SerializeField] private float baseHeightY = -1.7f;
    
    // Appelé via UnityEvent quand un tuyau est débouché ou curé
    public void SetFlowDirection(Vector2 direction)
    {
        if (waterRenderer != null)
        {
            waterRenderer.material.SetVector("_Direction_1", direction);
        }
    }

    // Appelé quand GAMA envoie un nouveau niveau d'eau
    public void UpdateWaterLevel(float waterLevel)
    {
        if (waterLevel <= 0f)
        {
            gameObject.SetActive(false);
            return;
        }

        if (!gameObject.activeSelf) gameObject.SetActive(true);

        transform.position = new Vector3(transform.position.x, baseHeightY, transform.position.z);
        transform.localScale = new Vector3(transform.localScale.x, waterLevel, transform.localScale.z);
    }
}