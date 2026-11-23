using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CatalogueManager : MonoBehaviour
{
    [System.Serializable]
    public sealed class CatalogueEntry
    {
        public Squishy Squishy;
    }

    [SerializeField] private RectTransform content;
    [SerializeField] private GameObject linePrefab;
    [SerializeField] private GameObject squishySlotPrefab;
    [SerializeField] private List<CatalogueEntry> squishys = new List<CatalogueEntry>();
    [SerializeField] private string lockedName = "???";
    [SerializeField] private Sprite favorit;
    [SerializeField] private Sprite notFavorit;

    private readonly Dictionary<Squishy, SquishySlot> slotsBySquishy =
        new Dictionary<Squishy, SquishySlot>();

    private readonly Dictionary<Squishy, string> catalogueIds = new Dictionary<Squishy, string>();
    private bool initialized;
    private bool registered;

    public string SaveFilePath => CatalogueSaveSystem.SaveFilePath;

    private void OnEnable()
    {
        CatalogueSaveSystem.Changed += RefreshExistingSlots;
        RefreshExistingSlots();
    }

    private void OnDisable()
    {
        CatalogueSaveSystem.Changed -= RefreshExistingSlots;
    }

    public string LockedName => lockedName;
    public Sprite Favorit => favorit;
    public Sprite NotFavorit => notFavorit;

    private void Start()
    {
        InitializeCatalogue();
    }

    public void InitializeCatalogue()
    {
        if (initialized)
            return;

        RegisterCatalogue();

        if (content == null || linePrefab == null || squishySlotPrefab == null)
        {
            Debug.LogError("CatalogueManager : renseigne Content, Line Prefab et Squishy Slot Prefab.", this);
            return;
        }

        if (linePrefab.GetComponent<RectTransform>() == null
            || linePrefab.GetComponent<HorizontalLayoutGroup>() == null)
        {
            Debug.LogError("CatalogueManager : Line Prefab doit avoir un RectTransform et un HorizontalLayoutGroup sur sa racine.", this);
            return;
        }

        if (squishySlotPrefab.GetComponent<RectTransform>() == null
            || squishySlotPrefab.GetComponent<SquishySlot>() == null)
        {
            Debug.LogError("CatalogueManager : Squishy Slot Prefab doit avoir un RectTransform et un SquishySlot sur sa racine.", this);
            return;
        }

        if (linePrefab.transform.childCount != 0)
        {
            Debug.LogError("CatalogueManager : Line Prefab doit etre vide, sans slots enfants.", this);
            return;
        }

        if (content.childCount != 0)
        {
            Debug.LogError("CatalogueManager : Content doit etre vide avant la generation.", this);
            return;
        }

        initialized = true;
        GameObject line = null;
        SquishySlot lastSlot = null;
        int count = 0;

        foreach (CatalogueEntry entry in squishys)
        {
            if (entry == null || entry.Squishy == null)
            {
                Debug.LogWarning("CatalogueManager : Squishy manquant, entree ignoree.", this);
                continue;
            }

            if (!catalogueIds.TryGetValue(entry.Squishy, out string id))
                continue;

            if (slotsBySquishy.ContainsKey(entry.Squishy))
            {
                Debug.LogWarning($"CatalogueManager : Squishy deja present : {entry.Squishy.SquishyName}.", this);
                continue;
            }

            if (count % 2 == 0)
            {
                line = Instantiate(linePrefab, content, false);
                line.name = $"Line {count / 2 + 1}";
                line.SetActive(true);
            }

            GameObject slotInstance = Instantiate(squishySlotPrefab, line.transform, false);
            SquishySlot slot = slotInstance.GetComponent<SquishySlot>();
            slot.name = $"Slot - {entry.Squishy.SquishyName}";
            slotsBySquishy.Add(entry.Squishy, slot);
            CatalogueSaveSystem.TryGetState(id, out bool locked, out bool isFavorit);
            slot.Initialize(this, entry.Squishy, locked, isFavorit);
            slot.gameObject.SetActive(true);
            lastSlot = slot;
            count++;
        }

        if (count % 2 != 0)
            AddEmptyCell(line.transform, (RectTransform)lastSlot.transform);

        LayoutRebuilder.MarkLayoutForRebuild(content);
    }

    public SquishySlot GetSlot(Squishy squishy)
    {
        InitializeCatalogue();

        if (squishy != null && slotsBySquishy.TryGetValue(squishy, out SquishySlot slot))
            return slot;

        return null;
    }

    public void SetLocked(Squishy squishy, bool locked)
    {
        if (TryGetId(squishy, out string id))
        {
            CatalogueSaveSystem.TryGetState(id, out _, out bool favorit);
            CatalogueSaveSystem.SetState(id, locked, favorit);
        }
    }

    public void UnlockSquishy(Squishy squishy)
    {
        SetLocked(squishy, false);
    }

    public void SetFavorit(Squishy squishy, bool value)
    {
        if (TryGetId(squishy, out string id))
        {
            CatalogueSaveSystem.TryGetState(id, out bool locked, out _);
            CatalogueSaveSystem.SetState(id, locked, value);
        }
    }

    public void ReverseFavoritState(Squishy squishy)
    {
        if (TryGetId(squishy, out string id))
        {
            CatalogueSaveSystem.TryGetState(id, out bool locked, out bool favorit);
            CatalogueSaveSystem.SetState(id, locked, !favorit);
        }
    }

    public void SetSquishyState(Squishy squishy, bool locked, bool isFavorit)
    {
        if (TryGetId(squishy, out string id))
            CatalogueSaveSystem.SetState(id, locked, isFavorit);
    }

    public void SetCurrentPodiumToSquishy(Squishy squishy)
    {
        if (!TryGetId(squishy, out string id))
            return;

        CatalogueSaveSystem.TryGetState(id, out bool locked, out _);

        if (locked)
        {
            Debug.LogWarning("CatalogueManager : ce Squishy est verrouille.", this);
            return;
        }

        if (squishy.SquishyPrefab == null)
        {
            Debug.LogWarning($"CatalogueManager : prefab manquant pour {squishy.SquishyName}.", this);
            return;
        }

        CatalogueSaveSystem.SetCurrentPodiumToSquishy(id);
    }

    public void RefreshCatalogue()
    {
        InitializeCatalogue();
        RefreshExistingSlots();
    }

    [ContextMenu("Reset Catalogue Save")]
    public void ResetCatalogueSave()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("CatalogueManager : lance le Play Mode pour reinitialiser la sauvegarde.", this);
            return;
        }

        RegisterCatalogue();
        CatalogueSaveSystem.ResetCatalogue(catalogueIds.Values);
        RefreshExistingSlots();
    }

    [ContextMenu("Log Save File Path")]
    public void LogSaveFilePath()
    {
        Debug.Log(SaveFilePath, this);
    }

    public bool SaveCatalogue()
    {
        RegisterCatalogue();
        return CatalogueSaveSystem.Save();
    }

    private void RegisterCatalogue()
    {
        if (registered)
            return;

        registered = true;
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (CatalogueEntry entry in squishys)
        {
            if (entry == null || entry.Squishy == null || catalogueIds.ContainsKey(entry.Squishy))
                continue;

            string id = entry.Squishy.SaveId;

            if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
            {
                Debug.LogError($"CatalogueManager : identifiant absent ou duplique pour {entry.Squishy.SquishyName}.", this);
                continue;
            }

            catalogueIds.Add(entry.Squishy, id);
        }

        CatalogueSaveSystem.EnsureItems(catalogueIds.Values);
    }

    private bool TryGetId(Squishy squishy, out string id)
    {
        RegisterCatalogue();
        id = null;

        if (squishy != null && catalogueIds.TryGetValue(squishy, out id))
            return true;

        Debug.LogWarning("CatalogueManager : Squishy absent du catalogue ou identifiant invalide.", this);
        return false;
    }

    private void RefreshExistingSlots()
    {
        foreach (KeyValuePair<Squishy, SquishySlot> entry in slotsBySquishy)
        {
            if (entry.Value == null || !catalogueIds.TryGetValue(entry.Key, out string id))
                continue;

            CatalogueSaveSystem.TryGetState(id, out bool locked, out bool favorit);
            entry.Value.ApplyState(locked, favorit);
        }
    }

    public void OnSquishyIconTouchedInCollection(SquishySlot slot)
    {
        if (slot == null || slot.IsLocked || slot.Squishy == null)
            return;

        if (!slotsBySquishy.TryGetValue(slot.Squishy, out SquishySlot registeredSlot)
            || registeredSlot != slot)
            return;
        

        Squishy data = slot.Squishy;
        string prefabName = data.SquishyPrefab != null ? data.SquishyPrefab.name : "Aucun prefab";
        Debug.Log($"Squishy : {data.SquishyName} | RarityLevel : {data.RarityLevel} | Prefab : {prefabName}", this);
    }
    
    public void OnSquishyIconTouchedInPodium(SquishySlot slot)
    {
        if (slot == null || slot.IsLocked || slot.Squishy == null)
            return;

        if (!slotsBySquishy.TryGetValue(slot.Squishy, out SquishySlot registeredSlot)
            || registeredSlot != slot)
            return;

        SetCurrentPodiumToSquishy(slot.Squishy);

        Squishy data = slot.Squishy;
        string prefabName = data.SquishyPrefab != null ? data.SquishyPrefab.name : "Aucun prefab";
        Debug.Log($"Squishy : {data.SquishyName} | RarityLevel : {data.RarityLevel} | Prefab : {prefabName}", this);
    }

    private static void AddEmptyCell(Transform parent, RectTransform reference)
    {
        GameObject cell = new GameObject("Empty Slot", typeof(RectTransform), typeof(LayoutElement));
        cell.layer = reference.gameObject.layer;
        RectTransform rect = (RectTransform)cell.transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = reference.sizeDelta;

        LayoutElement layout = cell.GetComponent<LayoutElement>();
        layout.minWidth = LayoutUtility.GetMinWidth(reference);
        layout.preferredWidth = LayoutUtility.GetPreferredWidth(reference);
        layout.flexibleWidth = LayoutUtility.GetFlexibleWidth(reference);
        layout.minHeight = LayoutUtility.GetMinHeight(reference);
        layout.preferredHeight = LayoutUtility.GetPreferredHeight(reference);
        layout.flexibleHeight = LayoutUtility.GetFlexibleHeight(reference);
    }
}
