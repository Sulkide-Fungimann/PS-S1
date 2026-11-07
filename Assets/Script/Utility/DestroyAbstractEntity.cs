using UnityEngine;

[DisallowMultipleComponent]
public class DestroyAbstractEntity : MonoBehaviour
{
    public void DestroyEntity(AbstractEntity entity)
    {
        if (entity == null) return;
        
        Destroy(entity.gameObject);
    }

    public void DestroyDetectedEntity(MonoBehaviour detectedEntity)
    {
        if (detectedEntity == null) return;
        if (detectedEntity is AbstractEntity entity) DestroyEntity(entity);
    }
}