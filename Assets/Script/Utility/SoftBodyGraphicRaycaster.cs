using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[AddComponentMenu("Event/Soft Body Graphic Raycaster")]
public class SoftBodyGraphicRaycaster : GraphicRaycaster
{
    private Canvas _ownerCanvas;

    public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList)
    {
        int firstResult = resultAppendList.Count;
        base.Raycast(eventData, resultAppendList);
        if (resultAppendList.Count == firstResult) return;

        if (_ownerCanvas == null) _ownerCanvas = GetComponent<Canvas>();
        if (_ownerCanvas.renderMode != RenderMode.WorldSpace) return;
        
        if (!SoftBodyGrabber.TryGetUIBlockingDistance(eventCamera, eventData.position, out float bodyDistance)) return;

        for (int i = resultAppendList.Count - 1; i >= firstResult; i--)
        {
            if (resultAppendList[i].distance > bodyDistance + SoftBodyGrabber.UIDepthTolerance)
                resultAppendList.RemoveAt(i);
        }
    }
}
