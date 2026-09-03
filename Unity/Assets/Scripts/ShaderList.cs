using UnityEngine;
using System.Collections.Generic;
using System;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

class ShaderListProcessor : IPreprocessBuildWithReport
{
	public int callbackOrder { get { return 0; } }


	public void OnPreprocessBuild(BuildReport report)
	{
		ShaderList shaderList = Resources.Load<ShaderList>("ShaderList");
		shaderList.ReloadAssetList();
	}
}
#endif

[CreateAssetMenu(fileName = "ShaderList", menuName = "ScriptableObjects/ShaderList", order = 1)]
public class ShaderList : ScriptableObject
{
	[Serializable]
	public class ShaderInfo
	{
		public string fileName;
		public string internalName;
		public Shader shader;
	}

	public List<ShaderInfo> shaderList = new List<ShaderInfo>();

#if UNITY_EDITOR
	[MenuItem("CONTEXT/UMO/Regenerate")]
	public void ReloadAssetList()
	{
		shaderList.Clear();
		string[] shadersList = AssetDatabase.FindAssets(" t:Shader");
		foreach (string s in shadersList)
		{
			ShaderList.ShaderInfo info = new ShaderList.ShaderInfo();
			Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(s));
			info.internalName = shader.name;
			info.fileName = System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(s)).ToLower();
			info.shader = shader;
			shaderList.Add(info);
		}
		if(EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64 ||
			EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows)
		{
			// Built-in shaders aren't returned by FindAssets. Explicit references
			// keep their Windows variants and avoid Shader.Find selecting an
			// identically named Android shader after an asset bundle is loaded.
			foreach(string name in new[] { "Unlit/Texture", "Unlit/Transparent", "UI/Default",
				"Mobile/Particles/Additive",
				"Legacy Shaders/Particles/VertexLit Blended",
				"Legacy Shaders/Particles/Alpha Blended Premultiply" })
			{
				Shader builtin = Shader.Find(name);
				if(builtin == null)
					throw new InvalidOperationException("Windows built-in shader not found: " + name);
				shaderList.Add(new ShaderInfo { internalName = name, fileName = name, shader = builtin });
			}
		}
		EditorUtility.SetDirty(this);
		AssetDatabase.SaveAssets();
	}


	[MenuItem("Assets/UMO/Regenerate List", true)]
	private static bool CheckScriptable()
	{
		return Selection.activeObject is ShaderList;
	}

	[MenuItem("Assets/UMO/Regenerate List")]
	private static void Refresh()
	{
		(Selection.activeObject as ShaderList).ReloadAssetList();
	}
#endif
}
