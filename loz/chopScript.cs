//----------------------------------------------------------------------------------------
// --- Code for chopping up skinned rag dolls in Unity ---
// Written by: Laurence Shann
// www.idlecreations.com/loz
//
// This work is licensed under the Creative Commons Attribution 4.0 International License. 
// To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/ or 
// send a letter to Creative Commons, 444 Castro Street, Suite 900, Mountain View, 
// California, 94041, USA.
//----------------------------------------------------------------------------------------

// How to use:
// -put this component on an empty gameObject
// -set the chopTarget parameter to the rag doll creature (the GameObject containing the SkinnedMeshRenderer should be one of the children of this)
// -find the bone you want to cut off in the rag doll hierarchy and set it as the chopBone parameter
// -set a fill material with chopFill
// -press play
// -tick the chopMe tick box

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class chopScript : MonoBehaviour{

    // - this bit is for testing purposes -
    public bool chopMe = false;
    private bool hasChopped = false;
    public GameObject chopTarget;
    public GameObject chopBone;
    public Material chopfill;
    public float chopThreshold=0.5f;
    void Update()
    {
        if (chopMe && !hasChopped)
        {
            hasChopped = true;
            chopRagDoll(chopTarget, chopBone, chopThreshold, chopfill);
        }
    }

    // - this function is static, so you can call it from elsewhere in your code -
    public static void chopRagDoll(GameObject target, GameObject bone, float threshold, Material fill)
    {
        SkinnedMeshRenderer mySkinnedMeshRenderer = target.GetComponentInChildren<SkinnedMeshRenderer>();
        GameObject clonedObject = GameObject.Instantiate(target, target.transform.position, target.transform.rotation) as GameObject;

        Mesh myMesh = mySkinnedMeshRenderer.sharedMesh;

        List<int> boneNumbers = new List<int>();
        foreach (Transform t in bone.GetComponentsInChildren<Transform>())
        {
            for (int i = 0; i < mySkinnedMeshRenderer.bones.Length; i++)
            {
                if (mySkinnedMeshRenderer.bones[i] == t) { boneNumbers.Add(i); }
            }
        }

        BoneWeight[] weights = myMesh.boneWeights;
        Mesh outerMesh = Object.Instantiate(myMesh) as Mesh;
        Mesh innerMesh = Object.Instantiate(myMesh) as Mesh;
        List<int> edges = new List<int>();

        for (int subMesh = 0; subMesh < myMesh.subMeshCount; subMesh++)
        {
            int[] tris = myMesh.GetTriangles(subMesh);
            List<int> outerTris = new List<int>();
            List<int> innerTris = new List<int>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                bool bVert1 = isPartOf(weights[tris[t]], boneNumbers, threshold);
                bool bVert2 = isPartOf(weights[tris[t + 1]], boneNumbers, threshold);
                bool bVert3 = isPartOf(weights[tris[t + 2]], boneNumbers, threshold);

                if (bVert1 || bVert2 || bVert3)
                {
                    if (bVert1 && !bVert2 && !bVert3) { edges.Add(tris[t + 1]); edges.Add(tris[t + 2]); }
                    if (!bVert1 && bVert2 && !bVert3) { edges.Add(tris[t + 2]); edges.Add(tris[t + 0]); }
                    if (!bVert1 && !bVert2 && bVert3) { edges.Add(tris[t + 0]); edges.Add(tris[t + 1]); }
                        
                    innerTris.Add(tris[t]);
                    innerTris.Add(tris[t + 1]);
                    innerTris.Add(tris[t + 2]);
                }
                else
                {
                    outerTris.Add(tris[t]);
                    outerTris.Add(tris[t + 1]);
                    outerTris.Add(tris[t + 2]);
                }
            }
            outerMesh.SetTriangles(outerTris.ToArray(), subMesh);
            innerMesh.SetTriangles(innerTris.ToArray(), subMesh);
        }

        SkinnedMeshRenderer outerMeshRenderer = target.GetComponentInChildren<SkinnedMeshRenderer>();
        SkinnedMeshRenderer innerMeshRenderer = clonedObject.GetComponentInChildren<SkinnedMeshRenderer>();
            
        outerMeshRenderer.sharedMesh = outerMesh;
        innerMeshRenderer.sharedMesh = innerMesh;

        GameObject capInner = GameObject.Instantiate(innerMeshRenderer.gameObject) as GameObject;
        GameObject capOuter = GameObject.Instantiate(outerMeshRenderer.gameObject) as GameObject;

        capInner.transform.parent = innerMeshRenderer.transform.parent;
        capOuter.transform.parent = outerMeshRenderer.transform.parent;

        capInner.GetComponent<SkinnedMeshRenderer>().sharedMesh = CapMesh(myMesh, edges, false);
        capOuter.GetComponent<SkinnedMeshRenderer>().sharedMesh = CapMesh(myMesh, edges, true);
        capInner.GetComponent<SkinnedMeshRenderer>().materials = new Material[1];
        capInner.GetComponent<SkinnedMeshRenderer>().material = fill;
        capOuter.GetComponent<SkinnedMeshRenderer>().materials = new Material[1];
        capOuter.GetComponent<SkinnedMeshRenderer>().material = fill;

        removeStuffFromChild(bone);

        GameObject boneInClone = findGameObjectIn(bone.name, clonedObject);
        removeStuffFromAllButChild(clonedObject, boneInClone);
        boneInClone.transform.parent = clonedObject.transform.parent;
        clonedObject.transform.parent = boneInClone.transform;

    }

    private static Quaternion capOrientation(Vector3[] verts, List<int> edges)
    {
        // rough guess as to the orientation of the vertices
        int third = Mathf.FloorToInt(edges.Count / 3);
        int twothird = Mathf.FloorToInt(edges.Count * 2 / 3);
        Vector3 v1 = verts[edges[0]];
        Vector3 v2 = verts[edges[third]];
        Vector3 v3 = verts[edges[twothird]];
        return Quaternion.LookRotation(Vector3.Cross(v1 - v2, v3 - v2));
    }

    public static Mesh CapMesh(Mesh parent, List<int> edges, bool facing = true)
    {
        if (edges.Count < 2) return null;

        int[] triangles = new int[(edges.Count - 1) * 3];
        Vector2[] uvs = new Vector2[parent.uv.Length];
        Vector3[] normals = new Vector3[parent.normals.Length];

        // calculate uv map limits
        Vector2 UVLimits_x = Vector2.zero;
        Vector2 UVLimits_y = Vector2.zero;
        Quaternion plane = capOrientation(parent.vertices, edges);
        for (int a = 0; a < edges.Count-1; a+=2)
        {
            Vector3 v1 = plane * (parent.vertices[edges[a]]);
            if ((a == 0) || v1.x < UVLimits_x[0]) UVLimits_x[0] = v1.x;
            if ((a == 0) || v1.x > UVLimits_x[1]) UVLimits_x[1] = v1.x;
            if ((a == 0) || v1.y < UVLimits_y[0]) UVLimits_y[0] = v1.y;
            if ((a == 0) || v1.y > UVLimits_y[1]) UVLimits_y[1] = v1.y;
            Vector3 v2 = plane * (parent.vertices[edges[a + 1]]);
            if ((a == 0) || v2.x < UVLimits_x[0]) UVLimits_x[0] = v2.x;
            if ((a == 0) || v2.x > UVLimits_x[1]) UVLimits_x[1] = v2.x;
            if ((a == 0) || v2.y < UVLimits_y[0]) UVLimits_y[0] = v2.y;
            if ((a == 0) || v2.y > UVLimits_y[1]) UVLimits_y[1] = v2.y;
        }

        // generate fan of polys to cap the edges
        for (int a = 0; a < edges.Count - 1; a+=2)
        {
            triangles[a * 3 + 0] = edges[0];
            triangles[a * 3 + 1] = facing ? edges[a] : edges[a + 1];
            triangles[a * 3 + 2] = facing ? edges[a + 1] : edges[a];

            for (int i = 0; i < 3; i++)
            {
                Vector3 v = plane * (parent.vertices[triangles[a * 3 + i]]);
                uvs[triangles[a * 3 + i]] = new Vector2((v.x - UVLimits_x[0]) / (UVLimits_x[1] - UVLimits_x[0]), (v.y - UVLimits_y[0]) / (UVLimits_y[1] - UVLimits_y[0]));
                normals[triangles[a * 3 + i]] = facing ? plane * Vector3.back : plane * Vector3.forward;
            }

        }
        Mesh m = new Mesh();
        m.vertices = parent.vertices;
        m.bindposes = parent.bindposes;
        m.boneWeights = parent.boneWeights;
        m.triangles = triangles;
        m.uv = uvs;
        m.RecalculateNormals();
        return m;
    }

    private static bool isPartOf(BoneWeight b, List<int> indices, float threshold)
    {
        float weight = 0;
        foreach (int i in indices)
        {
            if (b.boneIndex0 == i) weight += b.weight0;
            if (b.boneIndex1 == i) weight += b.weight1;
            if (b.boneIndex2 == i) weight += b.weight2;
            if (b.boneIndex3 == i) weight += b.weight3;
        }
        return (weight > threshold);
    }

    public static GameObject findGameObjectIn(string name, GameObject container)
    {
        if (container.name.Contains(name)) return container;
        else
        {
            foreach (Transform t in container.transform)
            {
                GameObject find = findGameObjectIn(name, t.gameObject);
                if (find) return find;
            }
            return null;
        }
    }

    public static void removeStuffFromChild(GameObject part)
    {
        foreach (Transform t in part.transform)
        {
            removeStuffFromChild(t.gameObject);
        }

        if (part.GetComponent<CharacterJoint>()) 
            GameObject.Destroy(part.GetComponent<CharacterJoint>());
        if (part.collider) GameObject.Destroy(part.collider);
        if (part.rigidbody) GameObject.Destroy(part.rigidbody);
    }

    public static void removeStuffFromAllButChild(GameObject part, GameObject child)
    {
        foreach (Transform t in part.transform)
        {
            if (part != child) removeStuffFromAllButChild(t.gameObject, child);
        }
        
        if (part.GetComponent<CharacterJoint>()) 
            GameObject.Destroy(part.GetComponent<CharacterJoint>());
        if (part != child)
        {
            if (part.collider) GameObject.Destroy(part.collider);
            if (part.rigidbody) GameObject.Destroy(part.rigidbody);
        }
    }


}
