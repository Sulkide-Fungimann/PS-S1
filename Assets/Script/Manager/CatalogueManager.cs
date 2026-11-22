using System;
using System.Collections.Generic;
using System.IO;
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

    [Serializable]
    private sealed class CatalogueSaveData
    {
        public int Version;
        public List<SquishySaveState> Items;
    }

    [Serializable]
    private sealed class SquishySaveState
    {
        public string Id;
        public bool IsLocked = true;
        public bool IsFavorit;
    }

    [SerializeField] private RectTransform content;
    [SerializeField] private HorizontalLayoutGroup linePrefab;
    [SerializeField] private SquishySlot squishySlotPrefab;
    [SerializeField] private List<CatalogueEntry> squishys = new List<CatalogueEntry>();
    [SerializeField] private string lockedName = "???";
    [SerializeField] private Sprite favorit;
    [SerializeField] private Sprite notFavorit;

    private readonly Dictionary<Squishy, SquishySlot> slotsBySquishy =
        new Dictionary<Squishy, SquishySlot>();

    private readonly Dictionary<string, SquishySaveState> savedStates =
        new Dictionary<string, SquishySaveState>(StringComparer.Ordinal);

    private readonly Dictionary<Squishy, SquishySaveState> catalogueStates =
        new Dictionary<Squishy, SquishySaveState>();

    private bool initialized;
    private bool saveLoaded;
    private bool canWriteSave = true;

    public string SaveFilePath => Path.Combine(Application.persistentDataPath, "catalogue.json");

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

        EnsureSaveLoaded();

        if (content == null || linePrefab == null || squishySlotPrefab == null)
        {
            Debug.LogError("CatalogueManager : renseigne Content, Line Prefab et Squishy Slot Prefab.", this);
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
        HorizontalLayoutGroup line = null;
        SquishySlot lastSlot = null;
        int count = 0;

        foreach (CatalogueEntry entry in squishys)
        {
            if (entry == null || entry.Squishy == null)
            {
                Debug.LogWarning("CatalogueManager : Squishy manquant, entree ignoree.", this);
                continue;
            }

            if (!catalogueStates.TryGetValue(entry.Squishy, out SquishySaveState state))
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
                line.gameObject.SetActive(true);
            }

            SquishySlot slot = Instantiate(squishySlotPrefab, line.transform, false);
            slot.name = $"Slot - {entry.Squishy.SquishyName}";
            slotsBySquishy.Add(entry.Squishy, slot);
            slot.Initialize(this, entry.Squishy, state.IsLocked, state.IsFavorit);
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
        SquishySaveState state = GetState(squishy);

        if (state == null)
            return;

        ApplyAndSave(squishy, state, locked, state.IsFavorit);
    }

    public void UnlockSquishy(Squishy squishy)
    {
        SetLocked(squishy, false);
    }

    public void SetFavorit(Squishy squishy, bool value)
    {
        SquishySaveState state = GetState(squishy);

        if (state == null)
            return;

        ApplyAndSave(squishy, state, state.IsLocked, value);
    }

    public void ReverseFavoritState(Squishy squishy)
    {
        SquishySaveState state = GetState(squishy);

        if (state != null)
            ApplyAndSave(squishy, state, state.IsLocked, !state.IsFavorit);
    }

    public void SetSquishyState(Squishy squishy, bool locked, bool isFavorit)
    {
        SquishySaveState state = GetState(squishy);

        if (state != null)
            ApplyAndSave(squishy, state, locked, isFavorit);
    }

    public void RefreshCatalogue()
    {
        InitializeCatalogue();

        foreach (KeyValuePair<Squishy, SquishySaveState> entry in catalogueStates)
            RefreshSlot(entry.Key, entry.Value);
    }

    [ContextMenu("Reset Catalogue Save")]
    public void ResetCatalogueSave()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("CatalogueManager : lance le Play Mode pour reinitialiser la sauvegarde.", this);
            return;
        }

        EnsureSaveLoaded();
        savedStates.Clear();

        foreach (KeyValuePair<Squishy, SquishySaveState> entry in catalogueStates)
        {
            SquishySaveState state = entry.Value;
            state.IsLocked = true;
            state.IsFavorit = false;
            savedStates.Add(state.Id, state);
            RefreshSlot(entry.Key, state);
        }

        canWriteSave = true;
        SaveCatalogue();
    }

    [ContextMenu("Log Save File Path")]
    public void LogSaveFilePath()
    {
        Debug.Log(SaveFilePath, this);
    }

    public bool SaveCatalogue()
    {
        if (!saveLoaded)
            EnsureSaveLoaded();

        if (!canWriteSave)
        {
            Debug.LogError("CatalogueManager : sauvegarde bloquee pour proteger le fichier illisible. Corrige le fichier ou appelle ResetCatalogueSave pour le remplacer.", this);
            return false;
        }

        try
        {
            CatalogueSaveData data = new CatalogueSaveData
            {
                Version = 1,
                Items = new List<SquishySaveState>(savedStates.Values)
            };

            data.Items.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            string path = SaveFilePath;
            string temporaryPath = path + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(data, true));

            if (File.Exists(path))
                File.Replace(temporaryPath, path, null);
            else
                File.Move(temporaryPath, path);

            return true;
        }
        catch (Exception exception) when (exception is IOException
            || exception is UnauthorizedAccessException
            || exception is ArgumentException
            || exception is NotSupportedException
            || exception is System.Security.SecurityException)
        {
            Debug.LogError($"CatalogueManager : sauvegarde impossible : {exception.Message}", this);
            return false;
        }
    }

    private void EnsureSaveLoaded()
    {
        if (saveLoaded)
            return;

        saveLoaded = true;
        bool saveNeeded = false;

        try
        {
            if (File.Exists(SaveFilePath))
            {
                CatalogueSaveData data = JsonUtility.FromJson<CatalogueSaveData>(File.ReadAllText(SaveFilePath));

                if (data == null || data.Version != 1 || data.Items == null)
                    throw new FormatException("Format ou version de sauvegarde invalide.");

                foreach (SquishySaveState state in data.Items)
                {
                    if (state == null || string.IsNullOrWhiteSpace(state.Id)
                        || savedStates.ContainsKey(state.Id))
                        throw new FormatException("Identifiant de sauvegarde absent ou duplique.");

                    if (state.IsLocked && state.IsFavorit)
                    {
                        state.IsFavorit = false;
                        saveNeeded = true;
                    }

                    savedStates.Add(state.Id, state);
                }
            }
            else
            {
                saveNeeded = true;
            }
        }
        catch (Exception exception) when (exception is IOException
            || exception is UnauthorizedAccessException
            || exception is ArgumentException
            || exception is FormatException
            || exception is NotSupportedException
            || exception is System.Security.SecurityException)
        {
            savedStates.Clear();
            canWriteSave = false;
            Debug.LogError($"CatalogueManager : lecture impossible, etats par defaut utilises sans ecraser le fichier. {exception.Message}", this);
        }

        HashSet<string> catalogueIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (CatalogueEntry entry in squishys)
        {
            if (entry == null || entry.Squishy == null || catalogueStates.ContainsKey(entry.Squishy))
                continue;

            string id = entry.Squishy.SaveId;

            if (string.IsNullOrWhiteSpace(id) || !catalogueIds.Add(id))
            {
                Debug.LogError($"CatalogueManager : identifiant absent ou duplique pour {entry.Squishy.SquishyName}. Ouvre cet asset dans l'Editor avant de construire le jeu.", this);
                continue;
            }

            if (!savedStates.TryGetValue(id, out SquishySaveState state))
            {
                state = new SquishySaveState { Id = id, IsLocked = true, IsFavorit = false };
                savedStates.Add(id, state);
                saveNeeded = true;
            }

            catalogueStates.Add(entry.Squishy, state);
        }

        if (saveNeeded && canWriteSave)
            SaveCatalogue();
    }

    private SquishySaveState GetState(Squishy squishy)
    {
        EnsureSaveLoaded();

        if (squishy != null && catalogueStates.TryGetValue(squishy, out SquishySaveState state))
            return state;

        Debug.LogWarning("CatalogueManager : Squishy absent du catalogue ou identifiant invalide.", this);
        return null;
    }

    private void ApplyAndSave(Squishy squishy, SquishySaveState state, bool locked, bool isFavorit)
    {
        state.IsLocked = locked;
        state.IsFavorit = !locked && isFavorit;
        RefreshSlot(squishy, state);
        SaveCatalogue();
    }

    private void RefreshSlot(Squishy squishy, SquishySaveState state)
    {
        if (slotsBySquishy.TryGetValue(squishy, out SquishySlot slot) && slot != null)
            slot.ApplyState(state.IsLocked, state.IsFavorit);
    }

    public void OnSquishyIconTouched(SquishySlot slot)
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
