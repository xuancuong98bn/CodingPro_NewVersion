using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

using MapMagic;

namespace MapMagic
{
	[GeneratorMenu (menu="Output", name ="CTS", disengageable = true, priority = 10)]
	public class CTSOutput  : SplatOutput
	{
		#if CTS_PRESENT
		public static CTS.CTSProfile ctsProfile;
		#endif

		//layer
		public new class Layer
		{
			public Input input = new Input(InoutType.Map);
			public Output output = new Output(InoutType.Map);
			public int index = 0;
			public float opacity = 1;
		}

		public new Layer[] baseLayers = new Layer[0];
		public new int selected = 0;

		//get static actions using instance
		public override Action<CoordRect, Chunk.Results, GeneratorsAsset, Chunk.Size, Func<float,bool>> GetProces() { return Process; }
		public override Func<CoordRect, Terrain, object, Func<float,bool>, IEnumerator> GetApply() { return Apply; }
		public override Action<CoordRect, Terrain> GetPurge() { return Purge; }


		//generator
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

		public override void Generate(CoordRect rect, Chunk.Results results, Chunk.Size terrainSize, int seed, Func<float,bool> stop= null)
		{
			#if CTS_PRESENT
			if ((stop!=null && stop(0)) || !enabled) return;

			//loading inputs
			Matrix[] matrices = new Matrix[baseLayers.Length];
			for (int i = 0; i < baseLayers.Length; i++)
			{
				if (baseLayers[i].input != null)
				{
				   matrices[i] = (Matrix)baseLayers[i].input.GetObject(results);
				   if (matrices[i] != null)
					  matrices[i] = matrices[i].Copy(null);
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

		public static new void Process (CoordRect rect, Chunk.Results results, GeneratorsAsset gens, Chunk.Size terrainSize, Func<float,bool> stop = null)
		{
			#if CTS_PRESENT
			if (stop!=null && stop(0)) return;

			//gathering prototypes and matrices lists
			List<int> indexesList = new List<int>();
			List<float> opacities = new List<float>();
			List<Matrix> matrices = new List<Matrix>();
			List<Matrix> biomeMasks = new List<Matrix>();

			foreach (CTSOutput gen in gens.GeneratorsOfType<CTSOutput>(onlyEnabled: true, checkBiomes: true))
			{
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
					matrix.Clamp01();

					//adding to lists
					matrices.Add(matrix);
					biomeMasks.Add(gen.biome == null ? null : biomeMask);
					indexesList.Add(gen.baseLayers[i].index);
					opacities.Add(gen.baseLayers[i].opacity);
				}
			}

			//optimizing matrices list if they are not used
			//CTS always use 16 channels, so this optimization is useless (and does not work when layer order changed)
			//for (int i = matrices.Count - 1; i >= 0; i--)
			//	if (opacities[i] < 0.001f || matrices[i].IsEmpty() || (biomeMasks[i] != null && biomeMasks[i].IsEmpty()))
			//	{ indexesList.RemoveAt(i); opacities.RemoveAt(i); matrices.RemoveAt(i); biomeMasks.RemoveAt(i); }

			//creating array
			float[,,] splats3D = new float[terrainSize.resolution, terrainSize.resolution, 16]; //TODO: use max index
			if (matrices.Count == 0) { results.apply.CheckAdd(typeof(CTSOutput), splats3D, replace: true); return; }

			//filling array
			if (stop!=null && stop(0)) return;

			int numLayers = matrices.Count;
			int numPrototypes = splats3D.GetLength(2);
			int maxX = splats3D.GetLength(0); int maxZ = splats3D.GetLength(1); //MapMagic.instance.resolution should not be used because of possible lods
																				//CoordRect rect =  matrices[0].rect;

			float[] values = new float[numPrototypes]; //row, to avoid reading/writing 3d array (it is too slow)

			for (int x = 0; x < maxX; x++)
				for (int z = 0; z < maxZ; z++)
				{
					int pos = rect.GetPos(x + rect.offset.x, z + rect.offset.z);
					float sum = 0;

					//clearing values
					for (int i = 0; i < numPrototypes; i++)
						values[i] = 0;

					//getting values
					for (int i = 0; i < numLayers; i++)
					{
						float val = matrices[i].array[pos];
						if (biomeMasks[i] != null) val *= biomeMasks[i].array[pos]; //if mask is not assigned biome was ignored, so only main outs with mask==null left here
						if (val < 0) val = 0; if (val > 1) val = 1;
						sum += val; //normalizing: calculating sum
						values[indexesList[i]] += val;
					}

					//setting color
					for (int i = 0; i < numLayers; i++) splats3D[z, x, i] = values[i] / sum;
				}

			//pushing to apply
			if (stop!=null && stop(0)) return;
			results.apply.CheckAdd(typeof(CTSOutput), splats3D, replace: true);
			#endif
		}

		public static new IEnumerator Apply(CoordRect rect, Terrain terrain, object dataBox, Func<float,bool> stop= null)
		{
			#if CTS_PRESENT

			float[,,] splats3D = (float[,,])dataBox;

			if (splats3D.GetLength(2) == 0) { Purge(rect,terrain); yield break; }

			TerrainData data = terrain.terrainData;

			//setting resolution
			int size = splats3D.GetLength(0);
			if (data.alphamapResolution != size) data.alphamapResolution = size;

			//welding
			if (MapMagic.instance != null && MapMagic.instance.splatsWeldMargins!=0)
			{
				Coord coord = Coord.PickCell(rect.offset, MapMagic.instance.resolution);
				//Chunk chunk = MapMagic.instance.chunks[coord.x, coord.z];
				
				Chunk neigPrevX = MapMagic.instance.chunks[coord.x-1, coord.z];
				if (neigPrevX!=null && neigPrevX.worker.ready) WeldTerrains.WeldSplatToPrevX(ref splats3D, neigPrevX.terrain, MapMagic.instance.splatsWeldMargins);

				Chunk neigNextX = MapMagic.instance.chunks[coord.x+1, coord.z];
				if (neigNextX!=null && neigNextX.worker.ready) WeldTerrains.WeldSplatToNextX(ref splats3D, neigNextX.terrain, MapMagic.instance.splatsWeldMargins);

				Chunk neigPrevZ = MapMagic.instance.chunks[coord.x, coord.z-1];
				if (neigPrevZ!=null && neigPrevZ.worker.ready) WeldTerrains.WeldSplatToPrevZ(ref splats3D, neigPrevZ.terrain, MapMagic.instance.splatsWeldMargins);

				Chunk neigNextZ = MapMagic.instance.chunks[coord.x, coord.z+1];
				if (neigNextZ!=null && neigNextZ.worker.ready) WeldTerrains.WeldSplatToNextZ(ref splats3D, neigNextZ.terrain, MapMagic.instance.splatsWeldMargins);
			}
			yield return null;

			//number of splat prototypes should match splats3D layers
			if (data.alphamapLayers != splats3D.GetLength(2))
			{
				SplatPrototype[] prototypes = new SplatPrototype[splats3D.GetLength(2)];
				for (int i=0; i<prototypes.Length; i++) prototypes[i] = new SplatPrototype() {texture = SplatOutput.defaultTex};
				data.splatPrototypes = prototypes;
			}
			
			//setting
			data.SetAlphamaps(0, 0, splats3D);

			//alphamap textures hide flag
			//data.alphamapTextures[0].hideFlags = HideFlags.None;
			

			//assigning CTS
			CTS.CompleteTerrainShader cts = terrain.gameObject.GetComponent<CTS.CompleteTerrainShader>();
			if (cts == null) cts = terrain.gameObject.AddComponent<CTS.CompleteTerrainShader>();
			cts.Profile = ctsProfile; 



			cts.UpdateShader();

			yield return null;

			#else
			yield return null;
			#endif

		}

		public static new void Purge(CoordRect rect, Terrain terrain)
		{
			//purged on switching back to the standard shader
			//TODO: it's wrong, got to be filled with background layer
		}

		public override void OnGUI (GeneratorsAsset gens)
		{
			#if CTS_PRESENT

			//wrong material and settings warnings
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
			if (MapMagic.instance.assignCustomTerrainMaterial)
			{
				layout.Par(30);
				layout.Label("Assign Custom Material is turned on.", rect:layout.Inset(0.8f), helpbox:true);
				if (layout.Button("Fix",rect:layout.Inset(0.2f))) 
				{
					MapMagic.instance.assignCustomTerrainMaterial = false;
				}
			}


			//profile
			layout.Par(5);
			layout.Field(ref ctsProfile, "Profile", fieldSize:0.7f);
			if (ctsProfile == null) { ResetLayers(0); return; }
			
			//refreshing layers from cts
			List<CTS.CTSTerrainTextureDetails> textureDetails = ctsProfile.TerrainTextures;
			if (baseLayers.Length != textureDetails.Count) ResetLayers(textureDetails.Count);

			//drawing layers
			layout.Par(5);
			layout.margin = 20; layout.rightMargin = 20; layout.fieldSize = 1f;
			for (int i=baseLayers.Length-1; i>=0; i--)
			{
				//if (baseLayers[i] == null)
				//baseLayers[i] = new Layer();
			
				if (layout.DrawWithBackground(OnLayerGUI, active:i==selected, num:i, frameDisabled:false)) selected = i;
			}

			layout.Par(3); layout.Par();
			//layout.DrawArrayAdd(ref baseLayers, ref selected, layout.Inset(0.25f));
			//layout.DrawArrayRemove(ref baseLayers, ref selected, layout.Inset(0.25f));
			layout.DrawArrayUp(ref baseLayers, ref selected, layout.Inset(0.25f), reverseOrder:true);
			layout.DrawArrayDown(ref baseLayers, ref selected, layout.Inset(0.25f), reverseOrder:true);

			#endif
		}

		public void OnLayerGUI (Layout layout, bool selected, int num)
		{

			#if CTS_PRESENT
				Layer layer = baseLayers[num];

				layout.Par(40); 

				if (num != 0) layer.input.DrawIcon(layout);
				else 
					if (layer.input.link != null) { layer.input.Link(null,null); } 
				
				//layout.Par(40); //not 65
				//layout.Field(ref rtp.globalSettingsHolder.splats[layer.index], rect:layout.Inset(40));
				layout.Inset(3);
				layout.Icon(ctsProfile.TerrainTextures[layer.index].Albedo, rect:layout.Inset(40), frame:true, alphaBlend:false);
				layout.Label(ctsProfile.TerrainTextures[layer.index].m_name, rect:layout.Inset(layout.field.width-80));
				if (num==0)
				{ 
					layout.cursor.y += layout.lineHeight;
					layout.cursor.height -= layout.lineHeight;
					layout.cursor.x -= layout.field.width-80;

					layout.Label("Background", rect:layout.Inset(layout.field.width-80), fontSize:9, fontStyle:FontStyle.Italic);
				}

				//layout.Label(rtp.globalSettingsHolder.splats[layer.index].name + (num==0? "\n(Background)" : ""), rect:layout.Inset(layout.field.width-60));

				baseLayers[num].output.DrawIcon(layout);
			#endif

		}

		public void ResetLayers (int newcount)
		{
			for (int i=0; i<baseLayers.Length; i++) 
			{
				baseLayers[i].input.Link(null,null); 

				Input connectedInput = baseLayers[i].output.GetConnectedInput(MapMagic.instance.gens.list);
				if (connectedInput != null) connectedInput.Link(null, null);
			}

			baseLayers = new Layer[newcount];
				
			for (int i=0; i<baseLayers.Length; i++) 
			{
				baseLayers[i] = new Layer();
				baseLayers[i].index = i;
			}
		}
	}

}