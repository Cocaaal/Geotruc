using GeoJSON.Net.Feature;
using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "CountryDatabase", menuName = "Scriptable Objects/CountryDatabase")]
public class CountryDatabase : ScriptableObject
{
    public TextAsset sourceJson;
    public List<CountryData> countries = new();
}

#if UNITY_EDITOR

[CustomEditor(typeof(CountryDatabase))]
public class CountryDatabaseCustomInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        CountryDatabase db = (CountryDatabase)target;

        if (db.sourceJson == null)
        {
            EditorGUILayout.HelpBox("Aucun fichier json lié", MessageType.Warning);
            return;
        }

        if (GUILayout.Button("Importer les données"))
        {
            ImportCountries(db);
        }
    }

    public void ImportCountries(CountryDatabase db)
    {
        db.countries.Clear();
        FeatureCollection root = JsonConvert.DeserializeObject<FeatureCollection>(db.sourceJson.text);
        for (int i=0; i<root.Features.Count; i++)
        {
            var feature = root.Features[i];
            CountryData countryData = new CountryData((string)feature.Properties["NAME_LONG"], (string)feature.Properties["ISO_A3_EH"], int.Parse((string)feature.Properties["ISO_N3"]), i);
            db.countries.Add(countryData);
        }
    }
}

#endif