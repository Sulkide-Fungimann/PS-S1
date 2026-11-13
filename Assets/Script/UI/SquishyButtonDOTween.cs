using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

public class JellyButtonDOTween : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private Vector3 originalScale;

    [Header("Jelly Settings")]
    [Tooltip("How much wider the button gets when pressed")]
    [SerializeField] private float pressScaleX = 1.15f;
    [Tooltip("How much flatter the button gets when pressed")]
    [SerializeField] private float pressScaleY = 0.85f;

    [Header("Timing & Wobble")]
    [SerializeField] private float duration = 0.3f;

    private void Start()
    {
        originalScale = transform.localScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        transform.DOKill();

        Vector3 squishScale = new Vector3(originalScale.x * pressScaleX, originalScale.y * pressScaleY, originalScale.z);

        transform.DOScale(squishScale, duration * 0.4f)
                 .SetUpdate(true)
                 .SetEase(Ease.OutQuad);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        transform.DOKill();

        transform.DOScale(originalScale, duration)
                 .SetUpdate(true)
                 .SetEase(Ease.OutElastic);
    }
}