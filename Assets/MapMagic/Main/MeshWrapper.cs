using UnityEngine;
using System.Collections;

namespace MapMagic
{
	[System.Serializable]
	public class MeshWrapper
	{
		public Vector3[] verts;
		public Vector3[] normals;
		public Vector2[] uv; public Vector2[] uv2; public Vector2[] uv3; public Vector2[] uv4;
		public Color[] colors;
		public Vector4[] tangents;

		public int[] tris;

		[System.NonSerialized] public int vertCounter = 0; //for appending mesh manually
		[System.NonSerialized] public int triCounter = 0;


		public void SetChannels (byte[] channels)
		{
			int length = channels.Length / 8;
			
			uv = new Vector2[length]; 
			uv2 = new Vector2[length]; 
			uv3 = new Vector2[length]; 
			uv4 = new Vector2[length]; 

			for (int v=0; v<length; v++) 
			{
				uv[v] = new Vector2(channels[v*8 + 1]/32f, channels[v*8 + 2]/32f);
				uv2[v] = new Vector2(channels[v*8 + 3]/32f, channels[v*8 + 4]/32f);
				uv3[v] = new Vector2(channels[v*8 + 5]/32f, channels[v*8 + 6]/32f);
				uv4[v] = new Vector2(channels[v*8 + 7]/32f, channels[v*8 + 7]/32f);
			}
		}

		public void ApplyTo (Mesh mesh)
		{
			mesh.Clear();

			mesh.vertices = verts;
			mesh.normals = normals;
			if (uv != null) mesh.uv = uv;
			if (uv2 != null) mesh.uv2 = uv2;
			if (uv3 != null) mesh.uv3 = uv3;
			if (uv4 != null) mesh.uv4 = uv4;
			if (colors != null) mesh.colors = colors;
			if (tangents != null) mesh.tangents = tangents;

			mesh.triangles = tris;
		}

		public void ReadMesh (Mesh mesh)
		{
			verts = mesh.vertices;
			normals = mesh.normals;
			uv = mesh.uv; uv2 = mesh.uv2; uv3 = mesh.uv3; uv4 = mesh.uv4;
			tangents = mesh.tangents;
			colors = mesh.colors;

			tris = mesh.triangles;

			if (normals != null && normals.Length == 0) normals = null;
			if (tangents != null && tangents.Length == 0) tangents = null;
			if (colors != null && colors.Length == 0) colors = null;
			if (uv != null && uv.Length == 0) uv = null; 
			if (uv2 != null && uv2.Length == 0) uv2 = null; 
			if (uv3 != null && uv3.Length == 0) uv3 = null; 
			if (uv4 != null && uv4.Length == 0) uv4 = null;
		}

		public void Append (MeshWrapper addMesh, Vector3 offset=new Vector3()) //custom verts length can be larger than addMesh vert count
		{
			/*for (int v=0; v<addMesh.verts.Length; v++) //TODO test iteration inside "if"
			{
				if (customVerts==null) verts[vertCounter+v] = addMesh.verts[v];// + offset;
				else verts[vertCounter+v] = customVerts[v];

				if (normals!=null && addMesh.normals!=null)
				{
					if (customNormals==null) normals[vertCounter+v] = addMesh.normals[v];
					else normals[vertCounter+v] = customNormals[v];
				}

				if (uv!=null && addMesh.uv!=null) uv[vertCounter+v] = addMesh.uv[v];
				if (uv2!=null && addMesh.uv2!=null) uv2[vertCounter+v] = addMesh.uv2[v];
				if (uv3!=null && addMesh.uv3!=null) uv3[vertCounter+v] = addMesh.uv3[v];
				if (uv4!=null && addMesh.uv4!=null) uv4[vertCounter+v] = addMesh.uv4[v];
				if (colors!=null && addMesh.colors!=null) colors[vertCounter+v] = addMesh.colors[v];
				if (tangents!=null && addMesh.tangents!=null) tangents[vertCounter+v] = addMesh.tangents[v];
			}*/


			//this definately works faster:

			//standard case (verts+normals+uv)
			if (normals!=null && uv!=null && addMesh.normals!=null && normals.Length!=0 && addMesh.normals.Length!=0 && addMesh.uv!=null && uv.Length!=0 && addMesh.uv.Length!=0)
			{
				if (offset.sqrMagnitude < 0.0001f)
					for (int v=0; v<addMesh.verts.Length; v++)
					{
						int v2 = vertCounter+v;
						verts[v2] = addMesh.verts[v];
						normals[v2] = addMesh.normals[v];
						uv[v2] = addMesh.uv[v];
					}
				else
					for (int v=0; v<addMesh.verts.Length; v++)
					{
						int v2 = vertCounter+v;
						verts[v2] = addMesh.verts[v] + offset;
						normals[v2] = addMesh.normals[v];
						uv[v2] = addMesh.uv[v];
					}
			}

			//special cases when there's no normals or uvs
			else
			{
				if (offset.sqrMagnitude < 0.0001f)
					for (int v=0; v<addMesh.verts.Length; v++)
						verts[vertCounter+v] = addMesh.verts[v]*0.1f;
				else 
					for (int v=0; v<addMesh.verts.Length; v++)
						verts[vertCounter+v] = addMesh.verts[v] + offset;

				if (normals != null && addMesh.normals != null && normals.Length!=0 && addMesh.normals.Length!=0)
				{
					for (int v=0; v<addMesh.normals.Length; v++)
						normals[vertCounter+v] = addMesh.normals[v];
				}

				if (uv!=null && addMesh.uv!=null && uv.Length!=0 && addMesh.uv.Length!=0)
					for (int v=0; v<addMesh.verts.Length; v++)
						uv[vertCounter+v] = addMesh.uv[v];
			}
			
			//additional cases
			if (uv2!=null && addMesh.uv2!=null && uv2.Length!=0 && addMesh.uv2.Length!=0)
				for (int v=0; v<addMesh.verts.Length; v++)
					uv2[vertCounter+v] = addMesh.uv2[v];

			if (uv3!=null && addMesh.uv3!=null && uv3.Length!=0 && addMesh.uv3.Length!=0)
				for (int v=0; v<addMesh.verts.Length; v++)
					uv3[vertCounter+v] = addMesh.uv3[v];

			if (uv4!=null && addMesh.uv4!=null && uv4.Length!=0 && addMesh.uv4.Length!=0)
				for (int v=0; v<addMesh.verts.Length; v++)
					uv4[vertCounter+v] = addMesh.uv4[v];

			if (uv!=null && addMesh.uv!=null && uv.Length!=0 && addMesh.uv.Length!=0)
				for (int v=0; v<addMesh.verts.Length; v++)
					uv[vertCounter+v] = addMesh.uv[v];

			if (colors!=null && addMesh.colors!=null && colors.Length!=0 && addMesh.colors.Length!=0)
				for (int v=0; v<addMesh.verts.Length; v++)
					colors[vertCounter+v] = addMesh.colors[v];

			if (tangents!=null && addMesh.tangents!=null && tangents.Length!=0 && addMesh.tangents.Length!=0)
				for (int v=0; v<addMesh.verts.Length; v++)
					tangents[vertCounter+v] = addMesh.tangents[v];

			//tris
			for (int t=0; t<addMesh.tris.Length; t++)
				tris[triCounter+t] = addMesh.tris[t] + vertCounter;

			//counters
			vertCounter += addMesh.verts.Length;
			triCounter += addMesh.tris.Length;
		}

		public void RotateMirror (int rotation, bool mirror) //rotation in 90-degree
		{
			//setting mesh rot-mirror params
			bool mirrorX = false;
			bool mirrorZ = false;
			bool rotate = false;
			
			switch (rotation)
			{
				case 90: rotate = true; mirrorX = true; break;
				case 180: mirrorX = true; mirrorZ = true; break;
				case 270: rotate = true; mirrorZ = true; break;
			}
			
			if (mirror) mirrorX = !mirrorX;
			
			//rotating verts
			for (int v=0; v<verts.Length; v++)
			{ 
				Vector3 pos = verts[v];
				//Vector3 normal = ns.array[v];
				//Vector4 tangent = ts.array[v];
				
				if (rotate)
				{
					float temp;

					temp = pos.x;
					pos.x = pos.z;
					pos.z = temp;
					
					//temp = normal.x;
					//normal.x = normal.z;
					//normal.z = temp;

					//temp = tangent.x;
					//tangent.x = tangent.z;
					//tangent.z = temp;
				}
				
				if (mirrorX) { pos.x = -pos.x;  }
				if (mirrorZ) { pos.z = -pos.z; } 
				
				verts[v] = pos;
				//ns.array[v] = normal;
				//ts.array[v] = tangent;
			}
			
			//mirroring tris
			if (mirror) 
				for (int t=0; t<tris.Length; t++) 
					for (int i=0; i<tris.Length; i+=3) 
			{
				int temp = tris[i];
				tris[i] = tris[i+2];
				tris[i+2] = temp;
			}
		}


	}
}