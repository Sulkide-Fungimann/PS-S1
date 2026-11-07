using UnityEngine;

public class SuikaGameFonction : MonoBehaviour
{
    private SuikaManager currentSuikaManager;
    
    void Start()
    {
        currentSuikaManager = SuikaManager.Instance;
    }

    public void OnPlayerDamaged()
    {
        currentSuikaManager.OnPlayerDamaged();
    }
    
}
