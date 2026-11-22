using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SuikaGameFonction : MonoBehaviour
{
    private SuikaManager currentSuikaManager;
    private SceneCameraTravel currentCameraTravel;
    private CatalogueManager currentCatalogueManager;
    private Squishy currentSquishy;
    private string _currentScene;

    private bool _unclockSuika;
    private bool _unclockCamera;

    [Header("Scene Camera")]
    public string sceneName;
    public string cameraPointName;
    
    void Start()
    {
        currentSuikaManager = SuikaManager.Instance;
        currentCameraTravel = SceneCameraTravel.Instance;
        _currentScene = SceneManager.GetActiveScene().name;

        if (currentSuikaManager != null) _unclockSuika = true;
        if (currentCameraTravel != null) _unclockCamera = true;
    }

    private void OnEnable()
    {
        currentSuikaManager = SuikaManager.Instance;
        currentCameraTravel = SceneCameraTravel.Instance;
        _currentScene = SceneManager.GetActiveScene().name;

        if (currentSuikaManager != null) _unclockSuika = true;
        if (currentCameraTravel != null) _unclockCamera = true;
    }

    private void OnDisable()
    {
        _unclockSuika = false;
    }

    private bool TryToFindCatalogueManager()
    {
        CatalogueManager catalogue = null;
        
        foreach (Transform currentParent in transform)
        {
            Debug.Log(currentParent.name);
            catalogue = currentParent.parent.GetComponent<CatalogueManager>();

            if (catalogue != null)
            {
                currentCatalogueManager = catalogue;
                return true;
            }

            return false;
        }
        
        return false;
    }

    private bool TryToFindSquishy()
    {
        Squishy squishy = null;

        foreach (Transform currentParent in transform)
        {
            Debug.Log(currentParent.name);
            
            squishy = currentParent.parent.GetComponent<SquishySlot>().Squishy;
            
            if (squishy != null)
            {
                currentSquishy = squishy;
                return true;
            }
            return false;
        }
        return false;
    }

    public void ChangeScene(string sceneName)
    {
        if (!_unclockSuika) return;
        SceneManager.LoadScene(sceneName);
    }

    public void ResetScene()
    {
        if (!_unclockSuika) return;
        SceneManager.LoadScene(_currentScene);
    }

    public void SwitchSceneTo()
    {
        if (!_unclockCamera)
        {
            return;
        }   

        if (string.IsNullOrEmpty(sceneName))
        {
            return;
        }
        
        
        currentCameraTravel.TravelTo(sceneName, cameraPointName);
    }

    public void SetFavoritStateByButton()
    {
        if (currentCatalogueManager == null)
        {
            if (TryToFindCatalogueManager() ==  false)
            {
                Debug.Log("No CatalogueManager found");
                return;
            }
        }
        
        if (currentSquishy == null)
        {
            if (TryToFindSquishy()==  false)
            {
                Debug.Log("No Squishy found");
                return;
            }
        }
        
        currentCatalogueManager.ReverseFavoritState(currentSquishy);
        
    }
    
    public void OnPlayerDamaged()
    {
        if (!_unclockSuika) return;
        currentSuikaManager.OnPlayerDamaged();
    }
    
    [UnityEngine.Scripting.Preserve]
    public string GetCurrentScoreText()
    {
        if (!_unclockSuika) return "Score : 00000";
        return currentSuikaManager != null ? currentSuikaManager.GetCurrentScoreText() : "Score : 00000";
    }

    [UnityEngine.Scripting.Preserve]
    public string GetNextTokenText()
    {
        if (!_unclockSuika) return "Token : 0";
        return "Token : " + currentSuikaManager.Token;
    }
    

}