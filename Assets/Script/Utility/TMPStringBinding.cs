using System;
using System.Reflection;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class TMPStringBinding : MonoBehaviour
{
    [Header("Texte a mettre a jour")]
    public TMP_Text TargetText;

    [Header("Source de la valeur")]
    public MonoBehaviour SourceScript;
    
    public string VariableName;

    private MonoBehaviour _boundSource;
    private string _boundName;
    private FieldInfo _field;
    private Func<string> _getter;
    private bool _bindingResolved;

    private void Reset()
    {
        TargetText = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        _bindingResolved = false;
    }

    private void LateUpdate()
    {
        RefreshText();
    }

    public void RefreshText()
    {
        if (TargetText == null || SourceScript == null) return;

        if (!_bindingResolved || _boundSource != SourceScript || _boundName != VariableName) ResolveBinding();

        if (_field == null && _getter == null) return;

        string value = _field != null ? (string)_field.GetValue(SourceScript) : _getter();

        value = value ?? string.Empty;
        
        if (TargetText.text != value) TargetText.text = value;
    }

    private void ResolveBinding()
    {
        _boundSource = SourceScript;
        _boundName = VariableName;
        _bindingResolved = true;
        _field = null;
        _getter = null;

        if (!string.IsNullOrWhiteSpace(VariableName))
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
            Type sourceType = SourceScript.GetType();

            FieldInfo field = sourceType.GetField(VariableName, flags);
            if (field != null && field.FieldType == typeof(string))
            {
                _field = field;
                return;
            }

            PropertyInfo property = sourceType.GetProperty(VariableName, flags);
            if (property != null && property.PropertyType == typeof(string) && property.GetGetMethod() != null && property.GetIndexParameters().Length == 0)
            {
                _getter = (Func<string>)Delegate.CreateDelegate(
                    typeof(Func<string>), SourceScript, property.GetGetMethod());
                return;
            }

            MethodInfo method = sourceType.GetMethod(VariableName, flags, null, Type.EmptyTypes, null);
            if (method != null && method.ReturnType == typeof(string) && !method.ContainsGenericParameters)
            {
                _getter = (Func<string>)Delegate.CreateDelegate(typeof(Func<string>), SourceScript, method);
            }
        }
        
    }
}
