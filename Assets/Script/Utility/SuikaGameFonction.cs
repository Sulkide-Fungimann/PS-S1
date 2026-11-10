using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SuikaGameFonction : MonoBehaviour
{
    private SuikaManager currentSuikaManager;
    private string _currentScene;

    private bool _unclock;

    
    void Start()
    {
        currentSuikaManager = SuikaManager.Instance;
        _currentScene = SceneManager.GetActiveScene().name;

        if (currentSuikaManager != null) _unclock = true;
    }

    private void OnEnable()
    {
        currentSuikaManager = SuikaManager.Instance;
        _currentScene = SceneManager.GetActiveScene().name;

        if (currentSuikaManager != null) _unclock = true;
    }

    private void OnDisable()
    {
        _unclock = false;
    }

    public void ChangeScene(string sceneName)
    {
        if (!_unclock) return;
        SceneManager.LoadScene(sceneName);
    }

    public void ResetScene()
    {
        if (!_unclock) return;
        SceneManager.LoadScene(_currentScene);
    }

    public void OnPlayerDamaged()
    {
        if (!_unclock) return;
        currentSuikaManager.OnPlayerDamaged();
    }
    
    [UnityEngine.Scripting.Preserve]
    public string GetCurrentScoreText()
    {
        if (!_unclock) return "Score : 00000";
        return currentSuikaManager != null ? currentSuikaManager.GetCurrentScoreText() : "Score : 00000";
    }

    [UnityEngine.Scripting.Preserve]
    public string GetNextTokenText()
    {
        if (!_unclock) return "Token : 0";
        return "Token : " + currentSuikaManager.Token;
    }
    
}