using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

public class ProjectCleaner : ScriptableObject {
	[HideInInspector]
	public static string version = "1.1";

	int i, j;
	bool foundType;
	string[] allAssetsArray;
	List<string> allAssets, candidateAssets, referencedAssets;
	List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();

	[System.Serializable]
	public struct AssetsToClear {
		public string name;
		public bool selected;
	}

	[SerializeField]
	List<string> fileTypes;
	[SerializeField]
	List<string> foldersToIgnore;
	[SerializeField]
	public List<AssetsToClear> assetsToClear = new List<AssetsToClear>();

	public void ScanAndSelect () {
		Debug.Log ("[ProjectCleaner]: Scanning...");

		if (fileTypes == null) {
			return;
		}

		assetsToClear = new List<AssetsToClear> ();
		allAssets = new List<string> ();
		candidateAssets = new List<string> ();
		referencedAssets = new List<string> ();
		scenes = EditorBuildSettings.scenes.ToList();
		scenes = scenes.Distinct ().ToList ();
		allAssetsArray = AssetDatabase.GetAllAssetPaths ();
		allAssets = allAssetsArray.ToList ();
		allAssetsArray = null;
		//Debug.Log ("[ProjectCleaner]: Total assets: " + allAssets.Count);

		for (i = 0; i < allAssets.Count; i++) {
			// Filter by "in" Assets
			if (i % 250 == 0) {
				if (EditorUtility.DisplayCancelableProgressBar("Scanning all Assets", (i + " / " + allAssets.Count), (float)i / (float)allAssets.Count)) {
					Debug.Log ("[ProjectCleaner]: Canceled by user!");
					EditorUtility.ClearProgressBar ();
					return;
				}
			}

			if (!allAssets [i].StartsWith ("Assets/")) {
				//Debug.Log ("Removing: " + allAssets [i]);
				allAssets.RemoveAt (i);
				i--;
				continue;
			} else {
				// Filter by "out" of folder in foldersToIgnore list
				for (j = 0; j < foldersToIgnore.Count; j++) {
					if (allAssets [i].Contains (foldersToIgnore[j])) {
						//Debug.Log ("Removing: " + allAssets [i]);
						allAssets.RemoveAt (i);
						i--;
						break;
					}
				}
			}
		}

		EditorUtility.ClearProgressBar ();
		//Debug.Log ("[ProjectCleaner]: Found " + allAssets.Count + " total assets");
		candidateAssets = new List<string> (allAssets);

		for (i = 0; i < candidateAssets.Count; i++) {
			if (i % 25 == 0) {
				if (EditorUtility.DisplayCancelableProgressBar("Analysing candidates", (i.ToString()), (float)i / (float)(candidateAssets.Count - i + 1))) {
					Debug.Log ("[ProjectCleaner]: Canceled by user!");
					EditorUtility.ClearProgressBar ();
					return;
				}
			}

			// Filter by file extension
			foundType = false;

			for (j = 0; j < fileTypes.Count; j++) {
				if (Path.GetExtension(candidateAssets [i]).ToLower ().EndsWith (fileTypes [j].ToLower ())) {
					foundType = true;
					break;
				}
			}

			if (!foundType) {
				//Debug.Log ("\tRemoving: " + candidateAssets [i]);
				candidateAssets.RemoveAt (i);
				i--;
			}
		}

		EditorUtility.ClearProgressBar ();
		//Debug.Log ("[ProjectCleaner]: Found " + candidateAssets.Count + " candidates");

		string[] dependenciesArray;
		List<string> dependenciesList = new List<string>();
		List<List<string>> dependenciesLists = new List<List<string>>();

		for (i = 0; i < scenes.Count; i++) {
			if (EditorUtility.DisplayCancelableProgressBar("Searching for dependencies on Scenes", Path.GetFileName(scenes[i].path), ((float)i / (float)scenes.Count))) {
				Debug.Log ("[ProjectCleaner]: Canceled by user!");
				EditorUtility.ClearProgressBar ();
				return;
			}

			dependenciesArray = AssetDatabase.GetDependencies (scenes [i].path, true);
			dependenciesList = dependenciesArray.ToList ();
			dependenciesLists.Add(dependenciesList);
			referencedAssets = referencedAssets.Concat<string>(dependenciesList).ToList();
		}

		referencedAssets = referencedAssets.Distinct ().ToList ();

		EditorUtility.ClearProgressBar ();

		foreach (string s in candidateAssets) {
			AssetsToClear a;
			a.name = s;
			a.selected = false;

			if (!referencedAssets.Contains (s)) {
				a.selected = true;
				assetsToClear.Add (a);
			}
		}

		assetsToClear = assetsToClear.OrderBy (a => a.name).ToList();
		Debug.Log ("[ProjectCleaner]: Found  " + assetsToClear.Count + " referenced to clear");
	}

	public void ClearAssets () {
		Debug.Log ("[ProjectCleaner]: Removing " + assetsToClear.Count + " assets...");

		for (i = 0; i < assetsToClear.Count; i++) {
			if (assetsToClear[i].selected) {
				AssetDatabase.DeleteAsset (assetsToClear[i].name);
			}
		}

		Debug.Log ("[ProjectCleaner]: Project clean!");
	}
}

[CustomEditor(typeof(ProjectCleaner))]
public class ProjectCleaner_Editor : Editor {
	const string path = "Assets/Editor/ProjectCleaner.asset";
	ProjectCleaner asset;

	void OnEnable () {
		asset = AssetDatabase.LoadAssetAtPath<ProjectCleaner>(path);
	}

	public override void OnInspectorGUI () {
		if (asset == null) {
			Create_AssetsPreProcessor_Settings ();
		}

		GUILayout.BeginHorizontal (); {
			GUILayout.FlexibleSpace ();
			GUILayout.Label ("Project Cleaner", EditorStyles.boldLabel);
			GUILayout.FlexibleSpace ();
		} GUILayout.EndHorizontal ();

		GUILayout.BeginHorizontal (); {
			GUILayout.FlexibleSpace ();
			GUILayout.Label ("(v" + ProjectCleaner.version + ")");
			GUILayout.FlexibleSpace ();
		} GUILayout.EndHorizontal ();

		GUILayout.Label ("Found: " + asset.assetsToClear.Count);
		GUILayout.Space (20);

		if (GUILayout.Button ("SCAN and SELECT unreferenced assets")) {
			asset.ScanAndSelect ();
		}

		if (asset.assetsToClear.Count == 0) {
			GUI.enabled = false;
		}

		if (GUILayout.Button ("REMOVE unreferenced Assets")) {
			asset.ClearAssets ();
			asset.ScanAndSelect ();
		}

		GUI.enabled = true;
		GUILayout.Space (20);
		DrawDefaultInspector ();
	}

	[MenuItem("Tools/Project Cleaner")]
	public static ProjectCleaner Create_AssetsPreProcessor_Settings () {
		ProjectCleaner asset;

		if (File.Exists (path)) {
			asset = AssetDatabase.LoadAssetAtPath<ProjectCleaner>(path);
		} else {
			asset = ScriptableObject.CreateInstance<ProjectCleaner> ();
			AssetDatabase.CreateAsset (asset, path);
			AssetDatabase.SaveAssets ();
		}

		EditorGUIUtility.PingObject (asset);
		Selection.activeObject = asset;
		return asset;
	}
}
