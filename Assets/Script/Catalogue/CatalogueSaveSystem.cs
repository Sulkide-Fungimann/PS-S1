using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class CatalogueSaveSystem
{
    [Serializable]
    private sealed class SaveData
    {
        public int Version;
        public List<ItemState> Items;
        public string[] PodiumSlots;
        public int currentPodiumToModifiy;
    }

    [Serializable]
    private sealed class ItemState
    {
        public string Id;
        public bool IsLocked = true;
        public bool IsFavorit;
    }

    private static readonly Dictionary<string, ItemState> states =
        new Dictionary<string, ItemState>(StringComparer.Ordinal);

    private static SaveData data;
    private static bool loaded;
    private static bool canWrite = true;

    public static event Action Changed;
    public static string SaveFilePath => Path.Combine(Application.persistentDataPath, "catalogue.json");

    public static int CurrentPodiumToModifiy
    {
        get
        {
            EnsureLoaded();
            return data.currentPodiumToModifiy;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntime()
    {
        states.Clear();
        data = null;
        loaded = false;
        canWrite = true;
        Changed = null;
    }

    public static void EnsureLoaded()
    {
        if (loaded)
            return;

        loaded = true;
        data = CreateDefaultData();
        bool needsSave = false;

        try
        {
            if (File.Exists(SaveFilePath))
            {
                SaveData existing = JsonUtility.FromJson<SaveData>(File.ReadAllText(SaveFilePath));

                if (existing == null || (existing.Version != 1 && existing.Version != 2)
                    || existing.Items == null)
                    throw new FormatException("Format ou version de sauvegarde invalide.");

                foreach (ItemState state in existing.Items)
                {
                    if (state == null || string.IsNullOrWhiteSpace(state.Id)
                        || states.ContainsKey(state.Id))
                        throw new FormatException("Identifiant de sauvegarde absent ou duplique.");

                    if (state.IsLocked && state.IsFavorit)
                    {
                        state.IsFavorit = false;
                        needsSave = true;
                    }

                    states.Add(state.Id, state);
                }

                if (existing.Version == 1)
                {
                    existing.PodiumSlots = new[] { "", "", "" };
                    existing.currentPodiumToModifiy = 1;
                    needsSave = true;
                }
                else if (existing.PodiumSlots == null || existing.PodiumSlots.Length != 3
                    || existing.currentPodiumToModifiy < 1 || existing.currentPodiumToModifiy > 3)
                {
                    throw new FormatException("Etat du podium invalide.");
                }

                for (int i = 0; i < 3; i++)
                {
                    if (existing.PodiumSlots[i] == null)
                    {
                        existing.PodiumSlots[i] = "";
                        needsSave = true;
                    }
                }

                existing.Version = 2;
                data = existing;
            }
            else
            {
                needsSave = true;
            }
        }
        catch (Exception exception) when (IsFileError(exception) || exception is FormatException)
        {
            states.Clear();
            data = CreateDefaultData();
            canWrite = false;
            Debug.LogError($"CatalogueSaveSystem : lecture impossible, fichier conserve sans modification. {exception.Message}");
        }

        if (needsSave && canWrite)
            Save();
    }

    public static void EnsureItems(IEnumerable<string> ids)
    {
        EnsureLoaded();
        bool added = false;

        foreach (string id in ids)
        {
            if (string.IsNullOrWhiteSpace(id) || states.ContainsKey(id))
                continue;

            states.Add(id, new ItemState { Id = id });
            added = true;
        }

        if (added)
            Save();
    }

    public static bool TryGetState(string id, out bool locked, out bool favorit)
    {
        EnsureLoaded();
        locked = true;
        favorit = false;

        if (string.IsNullOrWhiteSpace(id) || !states.TryGetValue(id, out ItemState state))
            return false;

        locked = state.IsLocked;
        favorit = state.IsFavorit;
        return true;
    }

    public static void SetState(string id, bool locked, bool favorit)
    {
        EnsureLoaded();

        if (string.IsNullOrWhiteSpace(id) || !states.TryGetValue(id, out ItemState state))
            return;

        state.IsLocked = locked;
        state.IsFavorit = !locked && favorit;
        Commit();
    }

    public static string GetPodiumSquishyId(int podiumNumber)
    {
        EnsureLoaded();
        return IsValidPodium(podiumNumber) ? data.PodiumSlots[podiumNumber - 1] : "";
    }

    public static void SetPodiumToModify(int podiumNumber)
    {
        EnsureLoaded();

        if (!IsValidPodium(podiumNumber))
        {
            Debug.LogWarning("CatalogueSaveSystem : le numero de podium doit etre 1, 2 ou 3.");
            return;
        }

        data.currentPodiumToModifiy = podiumNumber;
        Commit();
    }

    public static void SetCurrentPodiumToSquishy(string id)
    {
        EnsureLoaded();

        if (!TryGetState(id, out bool locked, out _) || locked)
            return;

        data.PodiumSlots[data.currentPodiumToModifiy - 1] = id;
        Commit();
    }

    public static void ResetPodiumState()
    {
        EnsureLoaded();
        data.PodiumSlots = new[] { "", "", "" };
        data.currentPodiumToModifiy = 1;
        Commit();
    }

    public static void ResetCatalogue(IEnumerable<string> ids)
    {
        EnsureLoaded();
        states.Clear();

        foreach (string id in ids)
        {
            if (!string.IsNullOrWhiteSpace(id) && !states.ContainsKey(id))
                states.Add(id, new ItemState { Id = id });
        }

        canWrite = true;
        Commit();
    }

    public static bool Save()
    {
        EnsureLoaded();

        if (!canWrite)
        {
            Debug.LogError("CatalogueSaveSystem : sauvegarde bloquee. Corrige le fichier puis relance le jeu, ou utilise ResetCatalogueSave pour le remplacer.");
            return false;
        }

        try
        {
            data.Version = 2;
            data.Items = new List<ItemState>(states.Values);
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
        catch (Exception exception) when (IsFileError(exception))
        {
            Debug.LogError($"CatalogueSaveSystem : sauvegarde impossible : {exception.Message}");
            return false;
        }
    }

    private static void Commit()
    {
        Save();
        Changed?.Invoke();
    }

    private static SaveData CreateDefaultData()
    {
        return new SaveData
        {
            Version = 2,
            Items = new List<ItemState>(),
            PodiumSlots = new[] { "", "", "" },
            currentPodiumToModifiy = 1
        };
    }

    private static bool IsValidPodium(int number)
    {
        return number >= 1 && number <= 3;
    }

    private static bool IsFileError(Exception exception)
    {
        return exception is IOException || exception is UnauthorizedAccessException
            || exception is ArgumentException || exception is NotSupportedException
            || exception is System.Security.SecurityException;
    }
}
