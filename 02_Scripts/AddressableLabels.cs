using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(fileName = "AddressableLabels", menuName = "ScriptableObjects/AddressableLabels")]
public class AddressableLabels : ScriptableObject
{
    [SerializeField]
    private List<AssetLabelReference> m_AssetLabelReferences;
    public List<AssetLabelReference> AssetLabelReferences => m_AssetLabelReferences;
}
