using System.Collections.Generic;
using TriangleNet.Geometry;
using TriangleNet.Meshing;
using TriangleNet.Topology;
using UnityEditor;
using UnityEngine;
using Mesh = UnityEngine.Mesh;
public struct TerrainVertex
{
    public Vector3 latLongAlt;
    public Vector3 pos;
    public Vector2 uv;
    public int onBorder;
    public int isCoastOrOcean;
    public int accepted;
}

[RequireComponent(typeof(MeshFilter))]
[ExecuteInEditMode]
public class GeoMesh : MonoBehaviour
{

    private Mesh mesh;

    private List<Vector3> positions;
    private List<Vector2> uvs;
    private List<int> triangles;

    private TerrainVertex[] verticesData;

    private Texture2D heightmap;

    private float heightFactor;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    private void Awake()
    {
        //mesh = new Mesh();
        //GetComponent<MeshFilter>().mesh = mesh;

        //Debug.Log("RESET");
        //vertices = new List<Vector3>();
        //triangles = new List<int>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void CreateMesh(Mesh mesh)
    {
        GetComponent<MeshFilter>().mesh = mesh;
        //UpdateMesh(mesh);
    }

    public void CreateMesh(Polygon[] polygons, HashSet<ulong> coastPoints, float resolution, string path)
    {
        var dummy = new Texture2D(1, 1, TextureFormat.RFloat, false);
        dummy.SetPixel(0, 0, Color.black);
        dummy.Apply();
        CreateMesh(polygons, coastPoints, resolution, 0, dummy, path);
    }

    public void CreateMesh(Polygon[] polygons, HashSet<ulong> coastPoints, float resolution, float heightFactor, Texture2D heightmap, string path)
    {
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;
        positions = new List<Vector3>();
        triangles = new List<int>();
        uvs = new List<Vector2>();
        bool isOcean = heightFactor == 0;
        this.heightmap = heightmap;
        this.heightFactor = heightFactor;
        foreach(var polygon in polygons)
        {
            Rectangle bounds = polygon.Bounds();
            int nbGridPoints = (int) ( Mathf.Ceil((float)((bounds.Top - bounds.Bottom) / resolution)) * Mathf.Ceil( (float) ( (bounds.Right - bounds.Left) / resolution) ) );
            int nbBorderPoints = polygon.Points.Count;

            verticesData = new TerrainVertex[nbBorderPoints + nbGridPoints];

            for (int i = 0; i < nbBorderPoints; i++)
            {
                Point p = polygon.Points[i];
                bool isCoast = coastPoints.Contains(TerrainGenerator.MakeKey(p));
                verticesData[i] = new TerrainVertex() {
                    latLongAlt = new Vector3((float)p.X, (float)p.Y, 0),
                    pos = new Vector3(10, 10, 10),
                    uv = new Vector2(0,0), 
                    accepted=1, 
                    onBorder=1 , 
                    isCoastOrOcean=(isCoast||isOcean)?1:0
                };
            }

            int idx = 0;
            for (float i = (float)bounds.Bottom + resolution; i < bounds.Top; i += resolution)
            {
                for (float j = (float)bounds.Left + resolution; j < bounds.Right; j += resolution)
                {
                    verticesData[nbBorderPoints + idx] = new TerrainVertex() {
                        latLongAlt = new Vector3(j, i, 0),
                        pos = new Vector3(0, 0, 0),
                        uv = new Vector2(0, 0),
                        accepted = 0,
                        onBorder = 0,
                        isCoastOrOcean = isOcean?1:0 
                    };
                    idx++;
                }
            }
            
            ComputeTerrainShader(nbBorderPoints, resolution, bounds);

            List<TerrainVertex> gridVertices = GetGridVertices(verticesData);
            List<TerrainVertex> borderVertices = GetBorderVertices(verticesData);

            IMesh newMesh = TriangulatePolygon(polygon, gridVertices);

            MergeMesh(newMesh.Triangles, borderVertices, gridVertices);
        }
        UpdateMesh();
        if (path != null) SaveMesh(path);
    }

    private List<TerrainVertex> GetGridVertices(TerrainVertex[] terrainVertices)
    {
        List<TerrainVertex> res = new List<TerrainVertex> ();
        foreach (TerrainVertex v in terrainVertices) { 
            if (v.onBorder==0 && v.accepted==1) res.Add(v);
        }
        return res;
    }
    private List<TerrainVertex> GetBorderVertices(TerrainVertex[] terrainVertices)
    {
        List<TerrainVertex> res = new List<TerrainVertex>();
        foreach (TerrainVertex v in terrainVertices)
        {
            if (v.onBorder==1) res.Add(v);
        }
        return res;
    }

    private void MergeMesh(ICollection<Triangle> newTriangles, List<TerrainVertex> newBorderVertices, List<TerrainVertex> newGridVertices)
    {
        int offset = positions.Count;
        foreach(TerrainVertex v in newBorderVertices) { 
            positions.Add(v.pos);
            uvs.Add(v.uv);
        }
        foreach(TerrainVertex v in newGridVertices) { 
            positions.Add(v.pos);
            uvs.Add(v.uv);
        }
        foreach (var triangle in newTriangles)
        {
            triangles.Add(triangle.GetVertexID(0) + offset);
            triangles.Add(triangle.GetVertexID(1) + offset);
            triangles.Add(triangle.GetVertexID(2) + offset);
        }
    }
    
    public ComputeShader computeShader;
    private void ComputeTerrainShader(int nbBorderPoints, float resolution, Rectangle bounds)
    {
        int kernelHandle = computeShader.FindKernel("CSMain");

        ComputeBuffer verticesBuffer = new ComputeBuffer(verticesData.Length, sizeof(float) * 8 + sizeof(int) * 3);
        verticesBuffer.SetData(verticesData);
        computeShader.SetBuffer(kernelHandle, "verticesData", verticesBuffer);

        computeShader.SetTexture(kernelHandle, "Heightmap", heightmap);
        computeShader.SetInt("nbBorderPoints", nbBorderPoints);
        computeShader.SetInt("nbTotalPoints", verticesData.Length);
        computeShader.SetFloat("resolution", resolution);
        computeShader.SetFloat("heightFactor", heightFactor);
        computeShader.SetFloats("bounds", (float)bounds.Bottom, (float)bounds.Left);

        int groupsX = Mathf.Max((verticesData.Length + 64 - 1)/64, 1);
        computeShader.Dispatch(kernelHandle, groupsX, 1, 1);

        verticesBuffer.GetData(verticesData);

        verticesBuffer.Dispose();
    }

    private IMesh TriangulatePolygon(Polygon polygon, List<TerrainVertex> gridVertices)
    {
        foreach (TerrainVertex v in gridVertices)
        {
            polygon.Add(new Vertex(v.latLongAlt.x, v.latLongAlt.y));
        }
        IMesh res = polygon.Triangulate();
        return res;
    }


    private void UpdateMesh()
    {
        mesh.Clear();
        mesh.vertices = positions.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        Debug.Log(uvs.Count + " " + positions.Count);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        //if (mesh.vertices.Length > 50000) Debug.Log(mesh.vertices.Length + "POINTS");
    }
    public void UpdateMesh(Mesh m)
    {
        positions.AddRange(m.vertices);
        triangles.AddRange(m.triangles);
        uvs.AddRange(m.uv);
        UpdateMesh();
    }

    public void SaveMesh(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null)
        {
            Debug.Log("Mesh déjà existant : " + path);
            return;
        }
        AssetDatabase.CreateAsset(Object.Instantiate(mesh), path + ".asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SAVED : " + path);
    }
}
