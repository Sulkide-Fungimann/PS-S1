using UnityEngine;

[CreateAssetMenu(fileName = "Squishy", menuName = "Suika/Catalogue Item")]
public sealed class Squishy : ScriptableObject
{
    [SerializeField, HideInInspector] private string saveId;
    [InspectorName("Name")] public string SquishyName;
    public Sprite iconLocked;
    public Sprite iconUnlocked;
    [Range(1, 3)] public int RarityLevel = 1;
    public GameObject SquishyPrefab;

    public string SaveId
    {
        get
        {
#if UNITY_EDITOR
            SynchronizeSaveId();
#endif
            return saveId;
        }
    }

    private void OnValidate()
    {
        RarityLevel = Mathf.Clamp(RarityLevel, 1, 3);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall -= SynchronizeSaveId;
        UnityEditor.EditorApplication.delayCall += SynchronizeSaveId;
#endif
    }

#if UNITY_EDITOR
    private void SynchronizeSaveId()
    {
        if (this == null)
            return;

        string path = UnityEditor.AssetDatabase.GetAssetPath(this);

        if (string.IsNullOrEmpty(path))
            return;

        string id = UnityEditor.AssetDatabase.AssetPathToGUID(path);

        if (string.IsNullOrEmpty(id) || saveId == id)
            return;

        saveId = id;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}