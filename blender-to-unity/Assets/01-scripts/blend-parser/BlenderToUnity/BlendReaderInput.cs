using UnityEditor;
using UnityEngine;
using Sirenix.OdinInspector;
using BlenderToUnity;

[ExecuteInEditMode]
public class BlendReaderInput : MonoBehaviour
{
    
    private string blendFilePath = @"E:\repos\blender-to-unity\blender-to-unity\Assets\01-scripts\blend-parser\BlenderToUnity\example.blend";
    [ReadOnly]
    public BlenderFile blendFile;

    [Button]
    private void ReadBlend()
    {
        blendFile = new BlenderToUnity.BlenderFile(blendFilePath);
    }
}