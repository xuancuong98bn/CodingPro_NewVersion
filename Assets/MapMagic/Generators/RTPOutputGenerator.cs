using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

using MapMagic;

namespace MapMagic
{
	[GeneratorMenu (menu="Output", name ="RTP", disengageable = true, priority = 10)]
	public class RTPOutput  : OutputGenerator
	{
		#if RTP
		[System.NonSerialized] public static ReliefTerrain rtp;
		#endif
		[System.NonSerialized] public static MeshRenderer renderer; //for gui purpose only

		//layer
		public class Layer
		{
			public Input input = new Input(InoutType.Map);
			public Output output = new Output(InoutType.Map);
			public int index = 0;
			public string name = "Layer";
			public float opacity = 1;

			public void OnAdd (int n) { }
			public void OnRemove (int n) 
			{ 
				input.Link(null,null); 
				Input connectedInput = output.GetConnectedInput(MapMagic.instance.gens.list);
				if (connectedInput != null) connectedInput.Link(null, null);
			}
			public void OnSwitch (int o, int n) { }
		}
		public Layer[] baseLayers = new Layer[0];
		public int selected = 0;

		public static Texture2D _defaultTex;
		public static Texture2D defaultTex {get{ if (_defaultTex==null) _defaultTex=Extensions.ColorTexture(2,2,new Color(0.75f, 0.75f, 0.75f, 0f)); return _defaultTex; }}

		public class RTPTuple { public int layer; public Color[] colorsA; public Color[] colorsB; public float opacity; }

		//get static actions using instance
		public override Action<CoordRect, Chunk.Results, GeneratorsAsset, Chunk.Size, Func<float,bool>> GetProces() { return Process; }
		public override Func<CoordRect, Terrain, object, Func<float,bool>, IEnumerator> GetApply() { return Apply; }
		public override Action<CoordRect, Terrain> GetPurge() { return Purge; }


		//generator
		public override IEnumerable<Input> Inputs() 
		{ 
			if (baseLayers==null) baseLayers = new Layer[0];
			for (int i=1; i<baseLayers.Length; i++)  //layer 0 is background
				if (baseLayers[i] != null && baseLayers[i].input != null)
					yield return baseLayers[i].input; 
		}
		public override IEnumerable<Output> Outputs() 
		{ 
			if (baseLayers==null) baseLayers = new Layer[0];
			for (int i=0; i<baseLayers.Length; i++) 
				if (baseLayers[i] != null && baseLayers[i].output != null)
					yield return baseLayers[i].output; 
		}

		public override void Generate(CoordRect rect, Chunk.Results results, Chunk.Size terrainSize, int seed, Func<float,bool> stop= null)
		{
			#if RTP
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

		public static void Process (CoordRect rect, Chunk.Results results, GeneratorsAsset gens, Chunk.Size terrainSize, Func<float,bool> stop = null)
		{
			#if RTP
			if (stop!=null && stop(0)) return;

			//finding number of layers
			int layersCount = 0;
			foreach (RTPOutput gen in MapMagic.instance.gens.GeneratorsOfType<RTPOutput>(onlyEnabled:true, checkBiomes:true))
				{ layersCount = gen.baseLayers.Length; break; }
			
			//creating color arrays
			RTPTuple result = new RTPTuple();
			result.colorsA = new Color[MapMagic.instance.resolution * MapMagic.instance.resolution];
			if (layersCount > 4) result.colorsB = new Color[MapMagic.instance.resolution * MapMagic.instance.resolution];
			
			//filling color arrays
			foreach (RTPOutput gen in MapMagic.instance.gens.GeneratorsOfType<RTPOutput>(onlyEnabled:true, checkBiomes:true))
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

				for (int i=0; i<gen.baseLayers.Length; i++)
				{
					//reading output directly
					Output output = gen.baseLayers[i].output;
					if (stop!=null && stop(0)) return; //checking stop before reading output
					if (!results.results.ContainsKey(output)) continue;
					Matrix matrix = (Matrix)results.results[output];
					if (matrix.IsEmpty()) continue;

					for (int x=0; x<rect.size.x; x++)
						for (int z=0; z<rect.size.z; z++)
					{
						int pos = matrix.rect.GetPos(x+matrix.rect.offset.x, z+matrix.rect.offset.z); //pos should be the same for colors array and matrix array
						
						//get value and adjust with biome mask
						float val = matrix.array[pos];
						float biomeVal = biomeMask!=null? biomeMask.array[pos] : 1;
						val *= biomeVal;

						//save value to colors array
						switch (gen.baseLayers[i].index)
						{
							case 0: result.colorsA[pos].r += val; break;
							case 1: result.colorsA[pos].g += val; break;
							case 2: result.colorsA[pos].b += val; break;
							case 3: result.colorsA[pos].a += val; break;
							case 4: result.colorsB[pos].r += val; break;
							case 5: result.colorsB[pos].g += val; break;
							case 6: result.colorsB[pos].b += val; break;
							case 7: result.colorsB[pos].a += val; break;
						}
					}

					if (stop!=null && stop(0)) return;
				}
			}

			//TODO: normalizing color arrays (if needed)

			//pushing to apply
			if (stop!=null && stop(0)) return;
			results.apply.CheckAdd(typeof(RTPOutput), result, replace: true);
			#endif
		}

		public static IEnumerator Apply(CoordRect rect, Terrain terrain, object dataBox, Func<float,bool> stop= null)
		{
			#if RTP

			//guard if old-style rtp approach is used
			ReliefTerrain chunkRTP = terrain.gameObject.GetComponent<ReliefTerrain>();
			if (chunkRTP !=null && chunkRTP.enabled) 
			{
				Debug.Log("MapMagic: RTP component on terain chunk detected. RTP Output Generator works with one RTP script assigned to main MM object only. Make sure that Copy Components is turned off.");
				chunkRTP.enabled = false;
			}
			yield return null;

			//loading objects
			RTPTuple tuple = (RTPTuple)dataBox;
			if (tuple == null) yield break;

			//creating control textures
			Texture2D controlA = new Texture2D(MapMagic.instance.resolution, MapMagic.instance.resolution);
			controlA.wrapMode = TextureWrapMode.Clamp;
			controlA.SetPixels(0,0,controlA.width,controlA.height,tuple.colorsA);
			controlA.Apply();
			yield return null;

			Texture2D controlB = null;
			if (tuple.colorsB != null) 
			{
				controlB = new Texture2D(MapMagic.instance.resolution, MapMagic.instance.resolution);
				controlB.wrapMode = TextureWrapMode.Clamp;
				controlB.SetPixels(0,0,controlB.width,controlB.height,tuple.colorsB);
				controlB.Apply();
				yield return null;
			}

			//welding
			if (MapMagic.instance != null && MapMagic.instance.splatsWeldMargins!=0)
			{
				Coord coord = Coord.PickCell(rect.offset, MapMagic.instance.resolution);
				//Chunk chunk = MapMagic.instance.chunks[coord.x, coord.z];
				
				Chunk neigPrevX = MapMagic.instance.chunks[coord.x-1, coord.z];
				if (neigPrevX!=null && neigPrevX.worker.ready && neigPrevX.terrain.materialTemplate.HasProperty("_Control1")) 
				{
					WeldTerrains.WeldTextureToPrevX(controlA, (Texture2D)neigPrevX.terrain.materialTemplate.GetTexture("_Control1"));
					if (controlB != null && neigPrevX.terrain.materialTemplate.HasProperty("_Control2"))
						WeldTerrains.WeldTextureToPrevX(controlB, (Texture2D)neigPrevX.terrain.materialTemplate.GetTexture("_Control2"));
				}

				Chunk neigNextX = MapMagic.instance.chunks[coord.x+1, coord.z];
				if (neigNextX!=null && neigNextX.worker.ready && neigNextX.terrain.materialTemplate.HasProperty("_Control1")) 
				{
					WeldTerrains.WeldTextureToNextX(controlA, (Texture2D)neigNextX.terrain.materialTemplate.GetTexture("_Control1"));
					if (controlB != null && neigNextX.terrain.materialTemplate.HasProperty("_Control2"))
						WeldTerrains.WeldTextureToNextX(controlB, (Texture2D)neigNextX.terrain.materialTemplate.GetTexture("_Control2"));
				}
				
				Chunk neigPrevZ = MapMagic.instance.chunks[coord.x, coord.z-1];
				if (neigPrevZ!=null && neigPrevZ.worker.ready && neigPrevZ.terrain.materialTemplate.HasProperty("_Control1")) 
				{
					WeldTerrains.WeldTextureToPrevZ(controlA, (Texture2D)neigPrevZ.terrain.materialTemplate.GetTexture("_Control1"));
					if (controlB != null && neigPrevZ.terrain.materialTemplate.HasProperty("_Control2"))
						WeldTerrains.WeldTextureToPrevZ(controlB, (Texture2D)neigPrevZ.terrain.materialTemplate.GetTexture("_Control2"));
				}

				Chunk neigNextZ = MapMagic.instance.chunks[coord.x, coord.z+1];
				if (neigNextZ!=null && neigNextZ.worker.ready && neigNextZ.terrain.materialTemplate.HasProperty("_Control1")) 
				{
					WeldTerrains.WeldTextureToNextZ(controlA, (Texture2D)neigNextZ.terrain.materialTemplate.GetTexture("_Control1"));
					if (controlB != null && neigNextZ.terrain.materialTemplate.HasProperty("_Control2"))
						WeldTerrains.WeldTextureToNextZ(controlB, (Texture2D)neigNextZ.terrain.materialTemplate.GetTexture("_Control2"));
				}
			}
			yield return null;

			//assigning material propery block (not saving for fixed terrains)
			//#if UNITY_5_5_OR_NEWER
			//assign textures using material property
			//MaterialPropertyBlock matProp = new MaterialPropertyBlock();
			//matProp.SetTexture("_Control1", controlA);
			//if (controlB!=null) matProp.SetTexture("_Control2", controlB);
			//#endif	
			
			//duplicating material and assign it's values
			//if (MapMagic.instance.customTerrainMaterial != null)
			//{
			//	//duplicating material
			//	terrain.materialTemplate = new Material(MapMagic.instance.customTerrainMaterial);
			//
			//	//assigning control textures
			//	if (terrain.materialTemplate.HasProperty("_Control1"))
			//		terrain.materialTemplate.SetTexture("_Control1", controlA);
			//	if (controlB != null && terrain.materialTemplate.HasProperty("_Control2"))
			//		terrain.materialTemplate.SetTexture("_Control2", controlB);
			//}

			if (rtp == null) rtp = MapMagic.instance.gameObject.GetComponent<ReliefTerrain>();
			if (rtp==null || rtp.globalSettingsHolder==null) yield break;
			
			//getting rtp material
			Material mat = null;
			if (terrain.materialTemplate!=null && terrain.materialTemplate.shader.name=="Relief Pack/ReliefTerrain-FirstPas")  //if relief terrain material assigned to terrain
				mat = terrain.materialTemplate;
			//if (mat==null && chunk.previewBackupMaterial!=null && chunk.previewBackupMaterial.shader.name=="Relief Pack/ReliefTerrain-FirstPas") //if it is backed up for preview
			//	mat = chunk.previewBackupMaterial;
			if (mat == null) //if still could not find material - creating new
			{
				Shader shader = Shader.Find("Relief Pack/ReliefTerrain-FirstPass");
				mat = new Material(shader);

				if (Preview.previewOutput == null) terrain.materialTemplate = mat;
				//else chunk.previewBackupMaterial = mat;
			}
			terrain.materialType = Terrain.MaterialType.Custom;

			//setting
			rtp.RefreshTextures(mat);
			rtp.globalSettingsHolder.Refresh(mat, rtp);
			mat.SetTexture("_Control1", controlA);
			if (controlB!=null) { mat.SetTexture("_Control2", controlB); mat.SetTexture("_Control3", controlB); }

			#else
			yield return null;
			#endif
		}

		public static void Purge(CoordRect rect, Terrain terrain)
		{
			//purged on switching back to the standard shader
			//TODO: it's wrong, got to be filled with background layer
		}

		public override void OnGUI (GeneratorsAsset gens)
		{
			#if RTP
			if (rtp==null) rtp = MapMagic.instance.GetComponent<ReliefTerrain>();
			if (renderer==null) renderer = MapMagic.instance.GetComponent<MeshRenderer>();

			//wrong material and settings warnings
			if (MapMagic.instance.copyComponents)
			{
				layout.Par(42);
				layout.Label("Copy Component should be turned off to prevent copying RTP to chunks.", rect:layout.Inset(0.8f), helpbox:true);
				if (layout.Button("Fix",rect:layout.Inset(0.2f))) MapMagic.instance.copyComponents = false;
			}

			if (rtp==null) 
			{
				layout.Par(42);
				layout.Label("Could not find Relief Terrain component on MapMagic object.", rect:layout.Inset(0.8f), helpbox:true);
				if (layout.Button("Fix",rect:layout.Inset(0.2f))) 
				{
					renderer = MapMagic.instance.gameObject.GetComponent<MeshRenderer>();
					if (renderer==null) renderer = MapMagic.instance.gameObject.AddComponent<MeshRenderer>();
					renderer.enabled = false;
					rtp = MapMagic.instance.gameObject.AddComponent<ReliefTerrain>();

					//if (MapMagic.instance.gameObject.GetComponent<InstantUpdater>()==null) MapMagic.instance.gameObject.AddComponent<InstantUpdater>();

					//filling empty splats
					Texture2D emptyTex = Extensions.ColorTexture(4,4,new Color(0.5f, 0.5f, 0.5f, 1f));
					emptyTex.name = "Empty";
					rtp.globalSettingsHolder.splats = new Texture2D[] { emptyTex,emptyTex,emptyTex,emptyTex };
				}
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

			if (MapMagic.instance.assignCustomTerrainMaterial)
			{
				layout.Par(30);
				layout.Label("Assign Custom Material is turned on.", rect:layout.Inset(0.8f), helpbox:true);
				if (layout.Button("Fix",rect:layout.Inset(0.2f))) 
				{
					MapMagic.instance.assignCustomTerrainMaterial = false;
				}
			}

			if (MapMagic.instance.GetComponent<InstantUpdater>() == null)
			{
				layout.Par(52);
				layout.Label("Use Instant Updater component to apply RTP changes to all the terrains.", rect:layout.Inset(0.8f), helpbox:true);
				if (layout.Button("Fix",rect:layout.Inset(0.2f))) 
				{
					MapMagic.instance.gameObject.AddComponent<InstantUpdater>();
				}
			}

			/*if (!MapMagic.instance.materialTemplateMode)
			{
				layout.Par(30);
				layout.Label("Material Template Mode is off.", rect:layout.Inset(0.8f), helpbox:true);
				if (layout.Button("Fix",rect:layout.Inset(0.2f))) MapMagic.instance.materialTemplateMode = true;
			}*/

			/*if ((renderer != null) &&
				(renderer.sharedMaterial == null || !renderer.sharedMaterial.shader.name.Contains("ReliefTerrain")))
			{
				layout.Par(50);
				layout.Label("No Relief Terrain material is assigned as Custom Material in Terrain Settings.", rect:layout.Inset(0.8f), helpbox:true);
				if (layout.Button("Fix",rect:layout.Inset(0.2f)))
				{
					//if (renderer.sharedMaterial == null)
					//{
						Shader shader = Shader.Find("Relief Pack/ReliefTerrain-FirstPass");
						if (shader != null) renderer.sharedMaterial = new Material(shader);
						else Debug.Log ("MapMagic: Could not find Relief Pack/ReliefTerrain-FirstPass shader. Make sure RTP is installed or switch material type to Standard.");
					//}
					MapMagic.instance.customTerrainMaterial = renderer.sharedMaterial;
					foreach (Chunk tw in MapMagic.instance.chunks.All()) tw.SetSettings();
				}
			}*/

			if (rtp == null) return;

			bool doubleLayer = false;
			for (int i=0; i<baseLayers.Length; i++)
				for (int j=0; j<baseLayers.Length; j++)
			{
				if (i==j) continue;
				if (baseLayers[i].index == baseLayers[j].index) doubleLayer = true;
			}
			if (doubleLayer)
			{
				layout.Par(30);
				layout.Label("Seems that multiple layers use the same splat index.", rect:layout.Inset(0.8f), helpbox:true);
				if (layout.Button("Fix",rect:layout.Inset(0.2f))) ResetLayers(baseLayers.Length);
			}


			//refreshing layers from rtp
			Texture2D[] splats = rtp.globalSettingsHolder.splats;
			if (baseLayers.Length != splats.Length) ResetLayers(splats.Length);

			//drawing layers
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

			layout.margin = 3; layout.rightMargin = 3;   
			layout.Par(64); layout.Label("Use Relief Terrain component to set layer properties. \"Refresh All\" in RTP settings might be required.", rect:layout.Inset(), helpbox: true);


			#else
			layout.margin = 5;
			layout.rightMargin = 5;

			layout.Par(45);
			layout.Label("Cannot find Relief Terrain plugin. Restart Unity if you have just installed it.", rect:layout.Inset(), helpbox:true);
			#endif
		}

		public void OnLayerGUI (Layout layout, bool selected, int num)
		{
			#if RTP
				Layer layer = baseLayers[num];

				layout.Par(40); 

				if (num != 0) layer.input.DrawIcon(layout);
				else 
					if (layer.input.link != null) { layer.input.Link(null,null); } 
				
				//layout.Par(40); //not 65
				//layout.Field(ref rtp.globalSettingsHolder.splats[layer.index], rect:layout.Inset(40));
				layout.Icon(rtp.globalSettingsHolder.splats[layer.index], rect:layout.Inset(40), frame:true, alphaBlend:false);
				layout.Label(rtp.globalSettingsHolder.splats[layer.index].name + (num==0? "\n(Background)" : ""), rect:layout.Inset(layout.field.width-60));

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