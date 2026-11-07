using UnityEngine;
[DisallowMultipleComponent]
public class ChangeLayerAbstractEntity : MonoBehaviour
{
    [Header("New Layer")]
    public string newLayerName;
    
    public void ChangeLayerEntity(AbstractEntity entity)
    {
        if (entity == null) return;
        
        
        ChangeLayer(entity.gameObject, newLayerName);
    }

    public void ChangeLayerDetectedEntity(MonoBehaviour detectedEntity)
    {
        if (detectedEntity == null) return;
        if (detectedEntity is AbstractEntity entity) ChangeLayerEntity(entity);
    }

    private void ChangeLayer(GameObject entity, string newLayerMask)
    {
        if (entity == null) return;
        entity.layer = LayerMask.NameToLayer(newLayerMask);
    }
}
