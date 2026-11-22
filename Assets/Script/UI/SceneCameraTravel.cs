using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class SceneCameraTravel : MonoBehaviour
{
    public static SceneCameraTravel Instance { get; private set; }
    public bool IsTransitioning { get; private set; }
    public float LoadProgress { get; private set; }

    [Header("Destination par defaut")]
    public string arrivalPointName = "CameraPoint";

    [Header("Travelling")]
    [Min(0f)] [SerializeField] private float travelDuration = 2f;
    [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Chargement mobile")]
    [SerializeField] private bool lowLoadingPriority = true;

    [Header("Evenements")]
    public UnityEvent onTransitionStarted = new UnityEvent();
    public UnityEvent onTravelStarted = new UnityEvent();
    public UnityEvent onArrived = new UnityEvent();
    public UnityEvent onTransitionFinished = new UnityEvent();
    public UnityEvent onTransitionFailed = new UnityEvent();

    private Scene currentScene;
    private bool priorityChanged;
    private UnityEngine.ThreadPriority previousPriority;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        if (transform.parent != null)
        {
            enabled = false;
            return;
        }

        Instance = this;
        currentScene = gameObject.scene;
        DontDestroyOnLoad(gameObject);
    }
    

    public void TravelTo(string sceneNameOrPath, string arrivalPoint = null)
    {
        if (Instance != this || !isActiveAndEnabled || IsTransitioning)
            return;

        if (arrivalPoint != null)
        {
            arrivalPointName = arrivalPoint;
        }
        
        
        
        if (string.IsNullOrWhiteSpace(sceneNameOrPath) || string.IsNullOrWhiteSpace(arrivalPointName))
        {
            Fail("Renseigner la scene et le nom du point d'arrive");
            return;
        }

        string target = sceneNameOrPath.Trim();
        Scene loadedScene = FindLoadedScene(target);
        if (loadedScene.IsValid() && loadedScene == currentScene) return;

        if (!loadedScene.IsValid() && !Application.CanStreamedLevelBeLoaded(target))
        {
            Fail("Scene introuvable : " + target);
            return;
        }

        IsTransitioning = true;
        StartCoroutine(Transition(target, loadedScene));
    }

    private IEnumerator Transition(string target, Scene destination)
    {
        Scene source = currentScene;
        bool loadedByThisTransition = !destination.IsValid();
        bool succeeded = false;
        LoadProgress = 0f;

        if (lowLoadingPriority)
        {
            previousPriority = Application.backgroundLoadingPriority;
            Application.backgroundLoadingPriority = UnityEngine.ThreadPriority.Low;
            priorityChanged = true;
        }

        try
        {
            InvokeSafely(onTransitionStarted);

            if (loadedByThisTransition)
            {
                AsyncOperation loading = BeginLoad(target);
                if (loading == null)
                    yield break;
                
                while (!loading.isDone)
                {
                    LoadProgress = Mathf.Clamp01(loading.progress / 0.9f);
                    yield return null;
                }
                destination = FindLoadedScene(target);
            }

            if (!destination.IsValid() || !destination.isLoaded)
            {
                Debug.LogError("SceneCameraTravel : chargement non abouti : " + target, this);
                yield break;
            }

            LoadProgress = 1f;
            yield return null;
            Transform arrival = FindArrival(destination);
            if (arrival == null)
            {
                Debug.LogError("SceneCameraTravel : il faut exactement un objet nomme '" + arrivalPointName + "' dans " + destination.name + ".", this);

                if (loadedByThisTransition)
                {
                    AsyncOperation rollback = BeginUnload(destination);
                    if (rollback != null)
                        yield return rollback;
                }
                yield break;
            }

            Vector3 startPosition = transform.position;
            Quaternion startRotation = transform.rotation;
            Vector3 endPosition = arrival.position;
            Quaternion endRotation = arrival.rotation;
            float duration = Mathf.Max(0f, travelDuration);
            float elapsed = 0f;

            InvokeSafely(onTravelStarted);
            while (elapsed < duration)
            {
                float t = elapsed / duration;
                float curvedT = movementCurve != null && movementCurve.length > 0
                    ? Mathf.Clamp01(movementCurve.Evaluate(t)) : t;
                transform.SetPositionAndRotation(
                    Vector3.Lerp(startPosition, endPosition, curvedT),
                    Quaternion.Slerp(startRotation, endRotation, curvedT));
                yield return null;
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            }

            transform.SetPositionAndRotation(endPosition, endRotation);
            if (!SceneManager.SetActiveScene(destination))
            {
                Debug.LogError("SceneCameraTravel : impossible d'activer la destination.", this);
                yield break;
            }

            currentScene = destination;
            InvokeSafely(onArrived);
            if (source.IsValid() && source.isLoaded && source != destination)
            {
                AsyncOperation unloading = BeginUnload(source);
                if (unloading == null)
                    yield break;
                yield return unloading;
                if (source.isLoaded)
                    yield break;
            }

            succeeded = true;
        }
        finally
        {
            RestoreLoadingPriority();
            IsTransitioning = false;
            InvokeSafely(succeeded ? onTransitionFinished : onTransitionFailed);
        }
    }

    private static Scene FindLoadedScene(string sceneNameOrPath)
    {
        Scene scene = SceneManager.GetSceneByPath(sceneNameOrPath);
        if (!scene.IsValid())
            scene = SceneManager.GetSceneByName(sceneNameOrPath);
        return scene.IsValid() && scene.isLoaded ? scene : default;
    }

    private Transform FindArrival(Scene scene)
    {
        Transform result = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name != arrivalPointName)
                    continue;
                if (result != null)
                    return null;
                result = candidate;
            }
        }
        return result;
    }

    private AsyncOperation BeginLoad(string target)
    {
        try { return SceneManager.LoadSceneAsync(target, LoadSceneMode.Additive); }
        catch (Exception exception) { Debug.LogException(exception, this); return null; }
    }

    private AsyncOperation BeginUnload(Scene scene)
    {
        try { return SceneManager.UnloadSceneAsync(scene); }
        catch (Exception exception) { Debug.LogException(exception, this); return null; }
    }

    private void Fail(string message)
    {
        Debug.LogError("SceneCameraTravel : " + message, this);
        InvokeSafely(onTransitionFailed);
    }

    private void InvokeSafely(UnityEvent callback)
    {
        try { callback?.Invoke(); }
        catch (Exception exception) { Debug.LogException(exception, this); }
    }

    private void RestoreLoadingPriority()
    {
        if (!priorityChanged)
            return;
        Application.backgroundLoadingPriority = previousPriority;
        priorityChanged = false;
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;
        RestoreLoadingPriority();
        Instance = null;
    }
}
