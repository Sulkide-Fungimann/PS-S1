using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PodiumManager : MonoBehaviour
{

    [SerializeField] private Transform[] podiumPoint;
    
    [Serializable]
    public sealed class PodiumSlot
    {
        public Transform Position;
        public Button EmptyButton;

        [NonSerialized] internal GameObject Instance;
        [NonSerialized] internal GameObject DisplayedPrefab;
        [NonSerialized] internal string DisplayedId;
        [NonSerialized] internal UnityAction ClickAction;
    }

    [SerializeField] private PodiumSlot firstPlace = new PodiumSlot();
    [SerializeField] private PodiumSlot secondPlace = new PodiumSlot();
    [SerializeField] private PodiumSlot thirdPlace = new PodiumSlot();
    [SerializeField] private List<Squishy> squishys = new List<Squishy>();

    private readonly Dictionary<string, Squishy> squishysById =
        new Dictionary<string, Squishy>(StringComparer.Ordinal);

    private PodiumSlot[] slots;

    public int CurrentPodiumToModifiy => CatalogueSaveSystem.CurrentPodiumToModifiy;

    private void Awake()
    {
        InitializeSlots();
    }

    private void OnEnable()
    {
        InitializeSlots();
        RebuildLookup();
        CatalogueSaveSystem.EnsureLoaded();
        CatalogueSaveSystem.Changed += RefreshPodium;
        RefreshPodium();
    }

    private void OnDisable()
    {
        CatalogueSaveSystem.Changed -= RefreshPodium;
    }

    private void OnDestroy()
    {
        if (slots == null)
            return;

        foreach (PodiumSlot slot in slots)
        {
            if (slot == null)
                continue;

            if (slot.EmptyButton != null && slot.ClickAction != null)
                slot.EmptyButton.onClick.RemoveListener(slot.ClickAction);

            ClearInstance(slot);
        }
    }

    public void SetPodiumToModify(int podiumNumber)
    {
        CatalogueSaveSystem.SetPodiumToModify(podiumNumber);
    }

    [ContextMenu("Reset Podium State")]
    public void ResetPodiumState()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("PodiumManager : lance le Play Mode pour reinitialiser le podium.", this);
            return;
        }

        CatalogueSaveSystem.ResetPodiumState();
        RefreshPodium();
    }

    public void RefreshPodium()
    {
        InitializeSlots();

        for (int i = 0; i < slots.Length; i++)
        {
            PodiumSlot slot = slots[i];

            if (slot == null)
                continue;

            string id = CatalogueSaveSystem.GetPodiumSquishyId(i + 1);
            GameObject prefab = null;

            if (!string.IsNullOrEmpty(id))
            {
                if (squishysById.TryGetValue(id, out Squishy squishy) && squishy != null)
                    prefab = squishy.SquishyPrefab;

                if (prefab == null && slot.DisplayedId != id)
                    Debug.LogWarning($"PodiumManager : Squishy ou prefab introuvable pour la place {i + 1}. Verifie la liste Squishys.", this);
            }

            if (slot.Position == null)
            {
                ClearInstance(slot);
                SetButtonVisible(slot, true);
                continue;
            }

            if (prefab == null)
            {
                ClearInstance(slot);
                slot.DisplayedId = id;
                SetButtonVisible(slot, true);
                continue;
            }

            if (slot.Instance == null || slot.DisplayedId != id || slot.DisplayedPrefab != prefab)
            {
                Transform choosenTransform = podiumPoint[i];
                
                
                ClearInstance(slot);
                slot.Instance = Instantiate(prefab, choosenTransform.position, Quaternion.identity, choosenTransform);
                slot.Instance.SetActive(true);
                slot.DisplayedId = id;
                slot.DisplayedPrefab = prefab;
            }

            SetButtonVisible(slot, false);
        }
    }

    private void InitializeSlots()
    {
        if (slots != null)
            return;

        slots = new[] { firstPlace, secondPlace, thirdPlace };

        for (int i = 0; i < slots.Length; i++)
        {
            PodiumSlot slot = slots[i];

            if (slot == null)
                continue;

            int number = i + 1;
            slot.ClickAction = () => SetPodiumToModify(number);

            if (slot.EmptyButton != null)
                slot.EmptyButton.onClick.AddListener(slot.ClickAction);

            if (slot.Position == null || slot.EmptyButton == null)
                Debug.LogWarning($"PodiumManager : renseigne Position et Empty Button pour la place {number}.", this);
            else if (slot.Position == slot.EmptyButton.transform
                || slot.Position.IsChildOf(slot.EmptyButton.transform))
                Debug.LogError($"PodiumManager : pour la place {number}, Position ne doit pas etre le bouton ni un enfant du bouton.", this);
        }
    }

    private void RebuildLookup()
    {
        squishysById.Clear();

        foreach (Squishy squishy in squishys)
        {
            if (squishy == null)
                continue;

            string id = squishy.SaveId;

            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogWarning($"PodiumManager : identifiant absent pour {squishy.SquishyName}.", this);
                continue;
            }

            if (squishysById.TryGetValue(id, out Squishy existing) && existing != squishy)
            {
                Debug.LogError($"PodiumManager : identifiant duplique pour {squishy.SquishyName}.", this);
                continue;
            }

            squishysById[id] = squishy;
        }
    }

    private static void SetButtonVisible(PodiumSlot slot, bool visible)
    {
        if (slot.EmptyButton != null && slot.EmptyButton.gameObject.activeSelf != visible)
            slot.EmptyButton.gameObject.SetActive(visible);
    }

    private static void ClearInstance(PodiumSlot slot)
    {
        if (slot.Instance != null)
        {
            slot.Instance.SetActive(false);
            Destroy(slot.Instance);
        }

        slot.Instance = null;
        slot.DisplayedPrefab = null;
        slot.DisplayedId = null;
    }
}
