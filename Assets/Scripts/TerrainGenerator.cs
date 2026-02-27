using GeoJSON.Net.Feature;
using GeoJSON.Net.Geometry;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using TriangleNet.Geometry;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using JsonPolygon = GeoJSON.Net.Geometry.Polygon;
using Polygon = TriangleNet.Geometry.Polygon;


public class TerrainGenerator : MonoBehaviour
{

    [Space]
    public GameObject countryPrefab;
    [Space]
    [Header("Countries")]
    public TextAsset countryDataJson;
    public CountryDatabase countryDatabase;
    public Texture2D heightmap;
    [Range(0.001f, 0.5f)]
    public float countryResolution = 0.056f;
    [Range(0.001f, 0.1f)]
    public float countryHeightFactor = 0.02f;
    [Space]
    [Header("Oceans")]
    public TextAsset oceanDataJson;
    [Range(0.001f, 10f)]
    public float oceanResolution = 10f;
    [Space]
    [Space]
    public bool createEverythingAtRuntime = false;
    public bool saveMesh = false;
    public bool loadFromSavedMesh = true;
    [Space]
    [Space]

    private string path = "Assets/Meshes/";
    [HideInInspector]
    public CountryData selectedCountry;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (createEverythingAtRuntime) CreateEverything();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void CreateEverything()
    {
        CreateAllCountries();
        CreateOceans();
    }
    public void CreateOceans()
    {
        selectedCountry = CountryData.Oceans;
        if (loadFromSavedMesh)
        {
            LoadSelectedCountryMesh();
        }
        else
        {
            Assert.IsNotNull(oceanDataJson, "No GeoJSON data for oceans linked");
            GenerateSelectedCountryMesh();
        }
    }
    public void CreateCountry()
    {
        Assert.IsNotNull(selectedCountry, "No selected country");
        if (loadFromSavedMesh)
        {
            LoadSelectedCountryMesh();
        }
        else
        {
            Assert.IsNotNull(countryDataJson, "No GeoJSON data for countries linked");
            GenerateSelectedCountryMesh();
        }
    }
    public void CreateAllCountries()
    {
        Assert.IsNotNull(countryDatabase, "No CountryDatabase linked");
        foreach (CountryData country in countryDatabase.countries)
        {
            selectedCountry = country;
            CreateCountry();
        }
    }

    private Feature GetSelectedCountryFeature()
    {
        if (selectedCountry.index == CountryData.Oceans.index)
        {
            return JsonConvert.DeserializeObject<FeatureCollection>(oceanDataJson.text).Features[0];
        }
        else
        {
            return JsonConvert.DeserializeObject<FeatureCollection>(countryDataJson.text).Features[selectedCountry.index];
        }
    }
    private JsonPolygon[] GetPolygonsFromFeature(Feature feature)
    {
        JsonPolygon[] res = null;
        if (feature.Geometry is JsonPolygon polygon)
        {
            res = new JsonPolygon[1] { polygon };
        } 
        else if (feature.Geometry is MultiPolygon multiPolygon)
        {
            res = new JsonPolygon[multiPolygon.Coordinates.Count];
            for (int i = 0; i < multiPolygon.Coordinates.Count; i++)
            {
                res[i] = multiPolygon.Coordinates[i];
            }
            return res;
        }
        return res;
    }
    private Polygon JsonPolygonToPolygon(JsonPolygon jsonPolygon)
    {
        var polygon = new Polygon();
        int boundaryMarker = 0;
        foreach (var ring in jsonPolygon.Coordinates)
        {
            Vertex[] v = new Vertex[ring.Coordinates.Count];
            for (int i=0; i<ring.Coordinates.Count; i++)
            {
                v[i] = new Vertex(ring.Coordinates[i].Latitude, ring.Coordinates[i].Longitude);
            }
            Contour contour = new Contour(v);
            polygon.Add(contour, boundaryMarker!=0);
            boundaryMarker++;
        }
        return polygon;
    }

    public void GenerateSelectedCountryMesh()
    {
        Feature feature = GetSelectedCountryFeature();
        JsonPolygon[] jsonPolygons = GetPolygonsFromFeature(feature);
        Polygon[] polygons = new Polygon[jsonPolygons.Length];
        for(int i=0; i<polygons.Length; i++)
        {
            polygons[i] = JsonPolygonToPolygon(jsonPolygons[i]);
        }

        GameObject countryInstance = Instantiate(countryPrefab, transform);

        ComputeCoastPoints();
        if (selectedCountry.nameLong.Equals(CountryData.Oceans.nameLong))
        {
            countryInstance.GetComponent<GeoMesh>().CreateMesh(polygons, coastPoints, oceanResolution, saveMesh ? path + CountryNameToFilename(selectedCountry.nameLong) : null);
        } else
        {
            countryInstance.GetComponent<GeoMesh>().CreateMesh(polygons, coastPoints, countryResolution, countryHeightFactor, heightmap, saveMesh ? path + CountryNameToFilename(selectedCountry.nameLong) : null);
        }
        countryInstance.name = selectedCountry.nameLong;
    }
    public void LoadSelectedCountryMesh()
    {
        string fullPath = path + CountryNameToFilename(selectedCountry.nameLong) + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(fullPath);
        Assert.IsNotNull(mesh, fullPath + " n'exsite pas");
        GameObject countryInstance = Instantiate(countryPrefab, transform);
        countryInstance.GetComponent<GeoMesh>().CreateMesh(mesh);
        countryInstance.name = selectedCountry.nameLong;
    }

    public string CountryNameToFilename(string name)
    {
        return name.Replace(" ", string.Empty).Replace(".", string.Empty);
    }
    public void SelectCountry(int index)
    {
        selectedCountry = countryDatabase.countries[index];
    }

    private static HashSet<ulong> coastPoints = null;
    private static double SCALE = 1000000; 
    public static ulong MakeKey(TriangleNet.Geometry.Point p)
    {
        ulong x = (ulong)(uint)(long)Mathf.Round((float)(p.X * SCALE));
        ulong y = (ulong)(uint)(long)Mathf.Round((float)(p.Y * SCALE));
        return (x << 32) | y;
    }
    private void ComputeCoastPoints()
    {
        if (coastPoints != null) return;
        coastPoints = new HashSet<ulong>();
        Feature feature = JsonConvert.DeserializeObject<FeatureCollection>(oceanDataJson.text).Features[0];
        if (feature.Geometry is MultiPolygon multiPolygon)
        {
            foreach (JsonPolygon polygon in multiPolygon.Coordinates)
            {
                foreach (var ring in polygon.Coordinates)
                {
                    foreach (var p in ring.Coordinates)
                    {
                        coastPoints.Add(MakeKey(new TriangleNet.Geometry.Point(p.Latitude, p.Longitude)));
                    }
                }
            }
        }
        //Debug.Log("COUNT " + coastPoints.Count);
    }
}

#if UNITY_EDITOR

[CustomEditor(typeof(TerrainGenerator))]
public class CountryGeneratorCustomInspector : Editor
{
    private SerializedProperty countryDatabase;

    private string filter = "";
    private Vector2 scrollPos;

    private void OnEnable()
    {
        countryDatabase = serializedObject.FindProperty("countryDatabase");
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        TerrainGenerator terrainGenerator = (TerrainGenerator)target;

        if (terrainGenerator.selectedCountry != null)
        {
            EditorGUILayout.LabelField("Selected country :", terrainGenerator.selectedCountry.nameLong);
        }else
        {
            EditorGUILayout.LabelField("No selected country");
        }

        CountryDatabase db = countryDatabase.objectReferenceValue as CountryDatabase;

        if (db == null || db.countries.Count == 0)
        {
            EditorGUILayout.HelpBox("No countries available", MessageType.Warning);
        }

        DisplayCountrySelectionList(db);
        serializedObject.ApplyModifiedProperties();

        if (GUILayout.Button("Create Country"))
        {
            terrainGenerator.CreateCountry();
        }
        if (GUILayout.Button("Create Oceans"))
        {
            terrainGenerator.CreateOceans();
        }
        if (GUILayout.Button("Create Everything"))
        {
            terrainGenerator.CreateEverything();
        }
    }

    private void DisplayCountrySelectionList(CountryDatabase db)
    {
        string[] countryNames = db.countries.Select(c => c.nameLong).ToArray();

        filter = EditorGUILayout.TextField(" ", filter);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(200), GUILayout.Width(250));
        string filterLowerCase = filter.ToLower();
        for (int i = 0; i < db.countries.Count; i++)
        {
            if (db.countries[i].nameLong.ToLower().Contains(filterLowerCase))
            {
                if (GUILayout.Button(db.countries[i].nameLong, GUILayout.Width(200)))
                {
                    ((TerrainGenerator)target).SelectCountry(i);
                }
            }
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndHorizontal();
    }
}

#endif