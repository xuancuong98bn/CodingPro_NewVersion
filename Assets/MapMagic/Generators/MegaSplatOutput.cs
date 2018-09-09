using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MapMagic;
using System;


#if __MEGASPLAT__
using JBooth.MegaSplat;
#endif

// MegaSplat format for terrains is:
// Control Texture
//     Red    : Index of first Texture
//     Green  : Index of second Texture
//     Blue   : Blend between textures
//     Alpha  : Dampening of displacement (0 = no effect, 1 = no displacement)
//
// Param Texture
//     R/G    : Flow Direciton in UV space
//     Blue   : Wetness
//     Alpha  : Puddles
//
// Once the user assigns a texture list, the node just lists all the texture clusters as inputs (it's a HUGE node.
// Right now, the highest two weights win. I'm not sure if this is how weights work in MM, but it seems to do ok.
//
// I might split this so you can optionally have a second MegaSplat node, and paint the first and second layers
// independently instead of just choosing the top two textures. This would let you control the blends much nicer,
// but I'm not sure how many users would understand it, and if it would make sense with the rest of the MM node.
//
// Wetness, puddles, and displacement dampening are also available as inputs.
//
// Right now, all of these are also outputs - is that correct though?


// I really like the idea of direct control for only 2 layers. It gives awesome possibilities, but I should agree that it's way too complicated to understand.
// So it's better leave as it is.
// I could not get wetness, puddles and displacement work, maybe I just don't get it how they should be set up.
// Denis



namespace MapMagic
{
	[System.Serializable]
	[GeneratorMenu(menu = "Output", name = "MegaSplat", disengageable = true, priority = 10)]
	public class MegaSplatOutput : OutputGenerator
	{
		#if __MEGASPLAT__
		public MegaSplatTextureList textureList; //TODO: think about texture list shared for all megasplat outputs (including biomes). Non-static
		public float clusterNoiseScale = 0.05f; //what's that?
		public bool smoothFallof = false;
		#endif

		public class Layer
		{
			public Input input = new Input(InoutType.Map);
			public Output output = new Output(InoutType.Map);
			public int index = 0;
			public float opacity = 1;
		}

		public Layer[] baseLayers = new Layer[0];
		public int selected = 0;

		public Input wetnessIn = new Input(InoutType.Map);
		public Input puddlesIn = new Input(InoutType.Map);
		public Input displaceDampenIn = new Input(InoutType.Map);

		public static bool formatARGB = false;


		public override IEnumerable<Input> Inputs()
		{ 
			if (baseLayers == null)
				baseLayers = new Layer[0];

			for (int i = 1; i < baseLayers.Length; i++) //layer 0 is background
			{
				if (baseLayers[i] == null)
					baseLayers[i] = new Layer();
				if (baseLayers[i].input == null)
					baseLayers[i].input = new Input(InoutType.Map);
				
				yield return baseLayers[i].input; 
			}
		
			yield return wetnessIn;
			yield return puddlesIn;
			yield return displaceDampenIn;
		}

		public override IEnumerable<Output> Outputs()
		{ 
			if (baseLayers == null)
				baseLayers = new Layer[0];
			for (int i = 0; i < baseLayers.Length; i++)
			{
				if (baseLayers[i] == null) baseLayers[i] = new Layer(); 
				if (baseLayers[i].output == null)
					baseLayers[i].output = new Output(InoutType.Map);

				yield return baseLayers[i].output; 
			}
		}



		//get static actions using instance
		public override Action<CoordRect, Chunk.Results, GeneratorsAsset, Chunk.Size, Func<float,bool>> GetProces()
		{
			return Process;
		}

		public override Func<CoordRect, Terrain, object, Func<float,bool>, IEnumerator> GetApply()
		{
			return Apply;
		}

		public override Action<CoordRect, Terrain> GetPurge()
		{
			return Purge;
		}



		public override void Generate(CoordRect rect, Chunk.Results results, Chunk.Size terrainSize, int seed, Func<float,bool> stop = null)
		{
	 		#if __MEGASPLAT__
			if ((stop!=null && stop(0)) || !enabled || textureList == null)
				return;

			//loading inputs
			Matrix[] matrices = new Matrix[baseLayers.Length];
			for (int i = 0; i < baseLayers.Length; i++)
			{
				if (baseLayers[i] == null)
					baseLayers[i] = new Layer();

				if (baseLayers[i].input != null)
				{
				   matrices[i] = (Matrix)baseLayers[i].input.GetObject(results);
				   if (matrices[i] != null)
				   {
					  matrices[i] = matrices[i].Copy(null);
					  matrices[i].Clamp01();
					}
				}
				if (matrices[i] == null)
				   matrices[i] = new Matrix(rect);
			}
			if (matrices.Length == 0)
				return;

			//background matrix
			//matrices[0] = terrain.defaultMatrix; //already created
			matrices[0].Fill(1);

			//populating opacity array
			float[] opacities = new float[matrices.Length];
			for (int i = 0; i < baseLayers.Length; i++)
				opacities[i] = baseLayers[i].opacity;
			opacities[0] = 1;

			//blending layers
			Matrix.BlendLayers(matrices, opacities);

			//saving changed matrix results
			for (int i = 0; i < baseLayers.Length; i++)
			{
				if (stop!=null && stop(0))
				   return; //do not write object is generating is stopped
				baseLayers[i].output.SetObject(results, matrices[i]);
			}
			#endif
		}

		public class MegaSplatData
		{
			//public Material template;
			public Color[] control;
			public Color[] param;
		}

		public static void Process(CoordRect rect, Chunk.Results results, GeneratorsAsset gens, Chunk.Size terrainSize, Func<float,bool> stop = null)
		{
			#if __MEGASPLAT__
			if (stop!=null && stop(0)) return;

			//using the first texture list for all
			MegaSplatTextureList textureList = null;
			bool smoothFallof = false;
         float clusterScale = 0.05f;
			foreach (MegaSplatOutput gen in gens.GeneratorsOfType<MegaSplatOutput>(onlyEnabled: true, checkBiomes: true))
			{
				if (gen.textureList != null) textureList = gen.textureList;
				smoothFallof = gen.smoothFallof;
            clusterScale = gen.clusterNoiseScale;
			}

			//creating color arrays
			MegaSplatData result = new MegaSplatData();

			result.control = new Color[MapMagic.instance.resolution * MapMagic.instance.resolution];
			result.param = new Color[MapMagic.instance.resolution * MapMagic.instance.resolution];
			
			//creating all and special layers/biomes lists
			List<Layer> allLayers = new List<Layer>(); //all layers count = gen num * layers num in each gen (excluding empty biomes, matrices, etc)
			List<Matrix> allMatrices = new List<Matrix>();
			List<Matrix> allBiomeMasks = new List<Matrix>();

			List<Matrix> specialWetnessMatrices = new List<Matrix>(); //special count = number of generators (excluding empty biomes only)
			List<Matrix> specialPuddlesMatrices = new List<Matrix>();
			List<Matrix> specialDampeningMatrices = new List<Matrix>();
			List<Matrix> specialBiomeMasks = new List<Matrix>();

			//filling all layers/biomes
			foreach (MegaSplatOutput gen in gens.GeneratorsOfType<MegaSplatOutput>(onlyEnabled: true, checkBiomes: true))
			{
				gen.textureList = textureList;

				//loading biome matrix
				Matrix biomeMask = null;
				if (gen.biome != null)
				{
					object biomeMaskObj = gen.biome.mask.GetObject(results);
					if (biomeMaskObj == null) continue; //adding nothing if biome has no mask
					biomeMask = (Matrix)biomeMaskObj;
					if (biomeMask == null) continue;
					if (biomeMask.IsEmpty()) continue; //optimizing empty biomes
				}

				for (int i = 0; i < gen.baseLayers.Length; i++)
				{
					//reading output directly
					Output output = gen.baseLayers[i].output;
					if (stop!=null && stop(0)) return; //checking stop before reading output
					if (!results.results.ContainsKey(output)) continue;
					Matrix matrix = (Matrix)results.results[output];
					if (matrix.IsEmpty()) continue;

					if (i >= textureList.clusters.Length)
					{
						Debug.LogError("Cluster out of range");
						continue;
					}

					//adding to lists
					allLayers.Add(gen.baseLayers[i]);
					allMatrices.Add(matrix);
					allBiomeMasks.Add(gen.biome == null ? null : biomeMask);
				}

				//adding special
				object wetnessObj = gen.wetnessIn.GetObject(results);
				specialWetnessMatrices.Add( wetnessObj!=null? (Matrix)wetnessObj : null );

				object puddlesObj = gen.puddlesIn.GetObject(results);
				specialPuddlesMatrices.Add( puddlesObj!=null? (Matrix)puddlesObj : null );

				object dampeingObj = gen.displaceDampenIn.GetObject(results);
				specialDampeningMatrices.Add( dampeingObj!=null? (Matrix)dampeingObj : null );

				specialBiomeMasks.Add(gen.biome == null ? null : biomeMask);
			}

			//if no texture list found in any of generators - returning
			if (textureList == null || allLayers.Count==0) return;

			//processing
			int allLayersCount = allLayers.Count;
			int specialCount = specialWetnessMatrices.Count;
			for (int x = 0; x<rect.size.x; x++)
				for (int z = 0; z<rect.size.z; z++)
				{
					int pos = rect.GetPos(x + rect.offset.x, z + rect.offset.z);

					// doesn't use height, normal, but I'm not sure how to get that here..
					Vector3 worldPos = new Vector3(
						1f * (x+rect.offset.x) / MapMagic.instance.resolution * rect.size.x,
						0,
						1f * (z+rect.offset.z) / MapMagic.instance.resolution * rect.size.z);
					float heightRatio = results.heights!=null? results.heights.array[pos] : 0.5f; //0 is the bottom point, 1 is the maximum top
					Vector3 normal = new Vector3(0,1,0);

					// find highest two layers
					int botIdx = 0;
					int topIdx = 0;
					float botWeight = 0;
					float topWeight = 0;

					for (int i = 0; i<allLayersCount; i++)
					{
						float val = allMatrices[i].array[pos];
						if (allBiomeMasks[i] != null) val *= allBiomeMasks[i].array[pos];

						// really want world position, Normal, and height ratio for brushes, but for now, just use x/z..

						if (val > botWeight)
						{
							topWeight = botWeight;
							topIdx = botIdx;

							botWeight = val;
							botIdx = i;
						}
						else if (val > topWeight)
						{
							topIdx = i;
							topWeight = val;
						}
					}

					//converting layer index to texture index
               topIdx = textureList.clusters[ allLayers[topIdx].index ].GetIndex(worldPos *  clusterScale, normal, heightRatio);
               botIdx = textureList.clusters[ allLayers[botIdx].index ].GetIndex(worldPos * clusterScale, normal, heightRatio);

					//swapping indexes to make topIdx always on top
					if (botIdx > topIdx) 
					{
						int tempIdx = topIdx;
						topIdx = botIdx;
						botIdx = tempIdx;

						float tempWeight = topWeight;
						topWeight = botWeight;
						botWeight = tempWeight;
					}

					//finding blend
					float totalWeight = topWeight + botWeight;	if (totalWeight<0.01f) totalWeight = 0.01f; //Mathf.Max and Clamp are slow
					float blend = botWeight / totalWeight;		if (blend>1) blend = 1;

					//adjusting blend curve
					if (smoothFallof) blend = (Mathf.Sqrt(blend) * (1-blend)) + blend*blend*blend;  //Magic secret formula! Inverse to 3*x^2 - 2*x^3

					//setting color
					result.control[pos] = new Color(botIdx / 255.0f, topIdx / 255.0f, 1.0f - blend, 1.0f);

					//params
					for (int i = 0; i<specialCount; i++)
					{
						float biomeVal = specialBiomeMasks[i]!=null? specialBiomeMasks[i].array[pos] : 1;

						if (specialWetnessMatrices[i]!=null) result.param[pos].b = specialWetnessMatrices[i].array[pos] * biomeVal;
						if (specialPuddlesMatrices[i]!=null) 
						{
							result.param[pos].a = specialPuddlesMatrices[i].array[pos] * biomeVal;
							result.param[pos].r = 0.5f;
							result.param[pos].g = 0.5f;
						}
						if (specialDampeningMatrices[i]!=null) result.control[pos].a = specialDampeningMatrices[i].array[pos] * biomeVal;
					}
						
				}
			
			//pushing to apply
			if (stop!=null && stop(0))
				return;
			results.apply.CheckAdd(typeof(MegaSplatOutput), result, replace: true);
			#endif
		}

		public static IEnumerator Apply(CoordRect rect, Terrain terrain, object dataBox, Func<float,bool> stop= null)
		{
			#if __MEGASPLAT__
			//loading objects
			MegaSplatData tuple = (MegaSplatData)dataBox;
			if (tuple == null)
				yield break;

			//terrain.materialType = Terrain.MaterialType.Custom; //it's already done with MapMagic
			//terrain.materialTemplate = new Material(tuple.template);

			// TODO: We should pool these textures instead of creating and destroying them!

			int res = MapMagic.instance.resolution;


			//control texture
			var control = new Texture2D(res, res, MegaSplatOutput.formatARGB? TextureFormat.ARGB32 : TextureFormat.RGBA32, false, true);
			control.wrapMode = TextureWrapMode.Clamp;
			control.filterMode = FilterMode.Point;
 
			control.SetPixels(0, 0, control.width, control.height, tuple.control);
			control.Apply();
			yield return null;
			

			//param texture
			var paramTex = new Texture2D(res, res, MegaSplatOutput.formatARGB? TextureFormat.ARGB32 : TextureFormat.RGBA32, false, true);
			paramTex.wrapMode = TextureWrapMode.Clamp;
			paramTex.filterMode = FilterMode.Point;

			paramTex.SetPixels(0, 0, paramTex.width, paramTex.height, tuple.param);
			paramTex.Apply();
			yield return null;


			//welding
			if (MapMagic.instance != null && MapMagic.instance.splatsWeldMargins!=0)
			{
				Coord coord = Coord.PickCell(rect.offset, MapMagic.instance.resolution);
				//Chunk chunk = MapMagic.instance.chunks[coord.x, coord.z];
				
				Chunk neigPrevX = MapMagic.instance.chunks[coord.x-1, coord.z];
				if (neigPrevX!=null && neigPrevX.worker.ready && neigPrevX.terrain.materialTemplate.HasProperty("_SplatControl")) 
				{
					WeldTerrains.WeldTextureToPrevX(control, (Texture2D)neigPrevX.terrain.materialTemplate.GetTexture("_SplatControl"));
					control.Apply();
				}

				Chunk neigNextX = MapMagic.instance.chunks[coord.x+1, coord.z];
				if (neigNextX!=null && neigNextX.worker.ready && neigNextX.terrain.materialTemplate.HasProperty("_SplatControl")) 
				{
					WeldTerrains.WeldTextureToNextX(control, (Texture2D)neigNextX.terrain.materialTemplate.GetTexture("_SplatControl"));
					control.Apply();
				}
				
				Chunk neigPrevZ = MapMagic.instance.chunks[coord.x, coord.z-1];
				if (neigPrevZ!=null && neigPrevZ.worker.ready && neigPrevZ.terrain.materialTemplate.HasProperty("_SplatControl")) 
				{
					WeldTerrains.WeldTextureToPrevZ(control, (Texture2D)neigPrevZ.terrain.materialTemplate.GetTexture("_SplatControl"));
					control.Apply();
				}

				Chunk neigNextZ = MapMagic.instance.chunks[coord.x, coord.z+1];
				if (neigNextZ!=null && neigNextZ.worker.ready && neigNextZ.terrain.materialTemplate.HasProperty("_SplatControl")) 
				{
					WeldTerrains.WeldTextureToNextZ(control, (Texture2D)neigNextZ.terrain.materialTemplate.GetTexture("_SplatControl"));
					control.Apply();
				}
			}
			yield return null;




			//TODO: weld textures with 1-pixel margin

			//assign textures using material property (not saving for fixed terrains)
			//#if UNITY_5_5_OR_NEWER
			//MaterialPropertyBlock matProp = new MaterialPropertyBlock();
			//matProp.SetTexture("_SplatControl", control);
			//matProp.SetTexture("_SplatParams", paramTex);
			//matProp.SetFloat("_ControlSize", res);
			//terrain.SetSplatMaterialPropertyBlock(matProp);
			//#endif

			//duplicating material
			if (MapMagic.instance.customTerrainMaterial != null)
			{
				terrain.materialTemplate = new Material(MapMagic.instance.customTerrainMaterial);

				//assigning control textures
				if (terrain.materialTemplate.HasProperty("_SplatControl"))
					terrain.materialTemplate.SetTexture("_SplatControl", control);
				if (terrain.materialTemplate.HasProperty("_SplatParams"))
					terrain.materialTemplate.SetTexture("_SplatParams", paramTex);
			}

			#else
			yield return null;
			#endif
		}

		public static void Purge(CoordRect rect, Terrain terrain)
		{
			//switch back to standard material to purge
			//TODO: it's wrong, got to be filled with background layer

			// destroy created textures, etc..
			/*var mat = terrain.materialTemplate;
			if (mat != null)
			{
				Texture controlTex = mat.GetTexture("_SplatControl");
				Texture paramTex = mat.GetTexture("_SplatParams");
				if (controlTex != null)
				{
				   GameObject.Destroy(controlTex);
				}
				if (paramTex != null)
				{
				   GameObject.Destroy(paramTex);
				}
				GameObject.Destroy(mat);
			}*/
		}


		#if __MEGASPLAT__
		private string[] clusterNames = new string[0];
		#endif

		public override void OnGUI (GeneratorsAsset gens)
		{
			#if __MEGASPLAT__
			layout.fieldSize = 0.5f; 

			//finding texture list from other generators
			if (textureList == null) 
				foreach (MegaSplatOutput gen in gens.GeneratorsOfType<MegaSplatOutput>(onlyEnabled: true, checkBiomes: true))
					if (gen.textureList != null) textureList = gen.textureList;

			//wrong material and settings warnings
			if (MapMagic.instance.showBaseMap) 
			{
				layout.Par(30);
            layout.Label("Show Base Map is turned on in Settings.", rect:layout.Inset(0.8f), helpbox:true);
				if (layout.Button("Fix",rect:layout.Inset(0.2f))) MapMagic.instance.showBaseMap = false;
			}

			if (MapMagic.instance.terrainMaterialType != Terrain.MaterialType.Custom)
			{
				layout.Par(30);
            layout.Label("Material Type is not switched to Custom.", rect:layout.Inset(0.8f), helpbox:true);
				if (layout.Button("Fix",rect:layout.Inset(0.2f))) 
				{
					MapMagic.instance.terrainMaterialType = Terrain.MaterialType.Custom;
					foreach (Chunk tw in MapMagic.instance.chunks.All()) tw.SetSettings();
				}
			}

			if (MapMagic.instance.customTerrainMaterial == null || !MapMagic.instance.customTerrainMaterial.shader.name.Contains("MegaSplat"))
			{
				layout.Par(42);
            layout.Label("No MegaSplat material is assigned as Custom Material in Terrain Settings.", rect:layout.Inset(), helpbox:true);
			}

			if (MapMagic.instance.customTerrainMaterial != null)
			{
				if (!MapMagic.instance.customTerrainMaterial.IsKeywordEnabled("_TERRAIN") || !MapMagic.instance.customTerrainMaterial.HasProperty("_SplatControl"))
				{
					layout.Par(42);
               layout.Label("Material must use a MegaSplat shader set to Terrain.", rect:layout.Inset(), helpbox:true);
				}

				if (MapMagic.instance.customTerrainMaterial.GetTexture("_Diffuse") == null)
				{
					layout.Par(42);
					layout.Label("Material does not have texture arrays assigned, please assign them.", rect:layout.Inset(), helpbox:true);
				}
			}

			/*if (!MapMagic.instance.materialTemplateMode)
			{
				layout.Par(30);
				layout.Label("Material Template Mode is off.", rect:layout.Inset(0.8f), helpbox:true);
				if (layout.Button("Fix",rect:layout.Inset(0.2f))) MapMagic.instance.materialTemplateMode = true;
			}*/

			if (MapMagic.instance.assignCustomTerrainMaterial)
			{
				layout.Par(30);
				layout.Label("Assign Custom Material is turned on.", rect:layout.Inset(0.8f), helpbox:true);
				if (layout.Button("Fix",rect:layout.Inset(0.2f))) 
				{
					MapMagic.instance.assignCustomTerrainMaterial = false;
				}
			}

			if (textureList == null || textureList.clusters == null || textureList.clusters.Length <= 0)
			{
				layout.Par(30);
            layout.Label("Please assign textures and list with clusters below:", rect:layout.Inset(), helpbox:true);

				layout.Field<MegaSplatTextureList>(ref textureList, "TextureList");
				foreach(Input input in Inputs()) input.link = null;
				return;
			}

			//drawing texture list field
			layout.Field<MegaSplatTextureList>(ref textureList, "TextureList");
			
			//setting all of the generators list to this one
			if (layout.change)
				foreach (MegaSplatOutput gen in gens.GeneratorsOfType<MegaSplatOutput>(onlyEnabled: true, checkBiomes: true))
					gen.textureList = textureList;

			//noise field
			layout.Par(5);
			layout.Field<float>(ref clusterNoiseScale, "Noise Scale");
			layout.Toggle(ref smoothFallof, "Smooth Fallof");

			//texture format
			layout.Par(5);
			layout.Toggle(ref MegaSplatOutput.formatARGB, "ARGB (since MS 1.14)");

			//gathering cluster names
			if (clusterNames.Length != textureList.clusters.Length)
				clusterNames = new string[textureList.clusters.Length];
			for (int i=0; i<clusterNames.Length; i++)
				clusterNames[i] = textureList.clusters[i].name;

			//drawing layers
			layout.Par(5);
			layout.Label("Layers:"); //needed to reset label bold style
			layout.margin = 20;
			layout.rightMargin = 20; 

			for (int i=baseLayers.Length-1; i>=0; i--)
			{
				if (baseLayers[i] == null)
					baseLayers[i] = new Layer();
			
				if (layout.DrawWithBackground(OnLayerGUI, active:i==selected, num:i, frameDisabled:false)) selected = i;
			}

			layout.Par(3); layout.Par();
			layout.DrawArrayAdd(ref baseLayers, ref selected, layout.Inset(0.25f));
			layout.DrawArrayRemove(ref baseLayers, ref selected, layout.Inset(0.25f));
			layout.DrawArrayUp(ref baseLayers, ref selected, layout.Inset(0.25f), reverseOrder:true);
			layout.DrawArrayDown(ref baseLayers, ref selected, layout.Inset(0.25f), reverseOrder:true);

			 //drawing effect layers
			layout.Par(5);
		 
			layout.Par(20); 
			wetnessIn.DrawIcon(layout);
			layout.Label("Wetness", layout.Inset());

			layout.Par(20); 
			puddlesIn.DrawIcon(layout);
			layout.Label("Puddles", layout.Inset());

			layout.Par(20); 
			displaceDampenIn.DrawIcon(layout);
			layout.Label("Displace Dampen", layout.Inset());

			#else

			layout.margin = 5;
			layout.rightMargin = 5;

			layout.Par(65);
			layout.Label("MegaSplat is not installed. Please install it from the Asset Store, it's really amazing, you'll like it..\n\t   Jason Booth", rect:layout.Inset(), helpbox:true);

			//What about adding a link to MegaSplat asset store page? Denis

			layout.Par(30);
			layout.Label("Restart Unity if you have just installed it.", rect:layout.Inset(), helpbox:true);

			#endif
		}

		public void OnLayerGUI (Layout layout, bool selected, int num)
		{
			#if __MEGASPLAT__
			if (baseLayers[num].index >= textureList.clusters.Length) baseLayers[num].index = textureList.clusters.Length-1;

			//disconnecting background
			if (num == 0) baseLayers[num].input.link = null;

			layout.Par(); 

			if (num != 0) baseLayers[num].input.DrawIcon(layout);

			if (selected)
				baseLayers[num].index = layout.Popup(baseLayers[num].index, clusterNames,rect:layout.Inset());
			else 
				layout.Label(textureList.clusters[baseLayers[num].index].name + (num==0? " (Background)" : ""), rect:layout.Inset());

			baseLayers[num].output.DrawIcon(layout);
			#endif
		}





	}//class
}//namespace