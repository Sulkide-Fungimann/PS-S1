using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class SquishySlot : MonoBehaviour
{
    [SerializeField] private Squishy squishy;
    [SerializeField] private bool isLocked = true;
    [SerializeField] private bool isFavorit;
    [SerializeField] private Image squishyImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image favoritImage;
    [SerializeField] private Button favoritButton;
    [SerializeField] private Button currentSquishy;
    [SerializeField] private bool canBePlayed;
    [SerializeField] private bool canBeOnPodium;

    private CatalogueManager manager;

    public Squishy Squishy => squishy;
    public bool IsLocked => isLocked;
    public bool IsFavorit => isFavorit;

    internal void Initialize(CatalogueManager catalogueManager, Squishy data, bool locked, bool favorit)
    {
        manager = catalogueManager;
        squishy = data;

        if (favoritButton != null)
        {
            favoritButton.onClick.RemoveListener(SwitchFavValue);
            favoritButton.onClick.AddListener(SwitchFavValue);
        }

        if (currentSquishy != null)
        {
            currentSquishy.onClick.RemoveListener(OnIconTouched);
            currentSquishy.onClick.AddListener(OnIconTouched);
        }

        ApplyState(locked, favorit);
    }

    internal void ApplyState(bool locked, bool favorit)
    {
        isLocked = locked;
        isFavorit = !locked && favorit;
        RefreshVisuals();
    }

    public void RefreshVisuals()
    {
        if (manager == null || squishy == null)
            return;

        if (squishyImage != null)
            squishyImage.sprite = isLocked ? squishy.iconLocked : squishy.iconUnlocked;

        if (nameText != null)
            nameText.text = isLocked ? manager.LockedName : squishy.SquishyName;

        if (favoritImage != null)
            favoritImage.sprite = isFavorit ? manager.Favorit : manager.NotFavorit;

        if (favoritButton != null)
            favoritButton.interactable = !isLocked;

        if (currentSquishy != null)
            currentSquishy.interactable = !isLocked;
    }

    public void SwitchFavValue()
    {
        if (manager != null && !isLocked)
            manager.SetFavorit(squishy, !isFavorit);
    }

    public void OnIconTouched()
    {
        if (manager != null && !isLocked)
        {
            if (canBePlayed)
            {
                manager.OnSquishyIconTouchedInCollection(this);
            }

            if (canBeOnPodium)
            {
                manager.OnSquishyIconTouchedInPodium(this);
            }
            
        }
            
    }

    private void OnValidate()
    {
        if (isLocked)
            isFavorit = false;
    }

    private void OnDestroy()
    {
        if (favoritButton != null)
            favoritButton.onClick.RemoveListener(SwitchFavValue);

        if (currentSquishy != null)
            currentSquishy.onClick.RemoveListener(OnIconTouched);
    }
}
