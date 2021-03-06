﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using usmooth.common;
using System;

public class DataCollector {
	public static DataCollector Instance = new DataCollector ();

	public FrameData CollectFrameData() {

		_visibleMaterials.Clear ();
		_visibleTextures.Clear ();

		//Debug.Log(string.Format("creating frame data. {0}", Time.frameCount));
		_currentFrame = new FrameData ();
		_currentFrame._frameCount = Time.frameCount;
		_currentFrame._frameDeltaTime = Time.deltaTime;
		_currentFrame._frameRealTime = Time.realtimeSinceStartup;
		_currentFrame._frameStartTime = Time.time;

		MeshRenderer[] meshRenderers = UnityEngine.Object.FindObjectsOfType(typeof(MeshRenderer)) as MeshRenderer[];
		foreach (MeshRenderer mr in meshRenderers) {
			if (mr.isVisible) {
				GameObject go = mr.gameObject;
				if (_meshLut.AddMesh(go)) {
					//Debug.Log(string.Format("CollectFrameData(): adding game object. {0}", go.GetInstanceID()));
					_currentFrame._frameMeshes.Add (go.GetInstanceID());
					_nameLut[go.GetInstanceID()] = go.name;

					foreach (var mat in mr.sharedMaterials) {
						AddVisibleMaterial(mat, mr.gameObject);
						
						if (mat != null) {
							if (Application.isEditor) {
								List<Texture> textures = UsEditorQuery.GetAllTexturesOfMaterial(mat);
								if (textures != null) {
									foreach (var texture in textures) {
										AddVisibleTexture(texture, mat);
									}
								}
							} else {
								AddVisibleTexture(mat.mainTexture, mat);
							}
						}
					}
				}
			}
		}

		_frames.Add (_currentFrame);
		return _currentFrame;
	}

	public void WriteName(int instID, UsCmd cmd) {
		string data;
		if (_nameLut.TryGetValue(instID, out data)) {
			cmd.WriteInt32 (instID);
			cmd.WriteStringStripped (data);
		}
	}

	private void AddVisibleMaterial(Material mat, GameObject gameobject)
	{
		if (mat != null) {
			if (!_visibleMaterials.ContainsKey(mat)) {
				_visibleMaterials.Add(mat, new HashSet<GameObject>());
			}
			_visibleMaterials[mat].Add(gameobject);
		}
	}
	
	private void AddVisibleTexture(Texture texture, Material ownerMat)
	{
		if (texture != null) {
			if (!_visibleTextures.ContainsKey(texture)) {
				_visibleTextures.Add(texture, new HashSet<Material>());
			}
			_visibleTextures[texture].Add(ownerMat);
			
			// refresh the size
			if (!_textureSizeLut.ContainsKey(texture)) {
				_textureSizeLut[texture] = UsTextureUtil.CalculateTextureSizeBytes(texture);
			}
		}
	}
	
	public void DumpAllInfo() {
		
		Debug.Log (string.Format ("{0} visible materials ({2}), visible textures ({3})",
		                          DateTime.Now.ToLongTimeString (),
		                          VisibleMaterials.Count,
		                          VisibleTextures.Count));
		
		string matInfo = "";
		foreach (KeyValuePair<Material, HashSet<GameObject>> kv in VisibleMaterials) {
			matInfo += string.Format ("{0} {1} {2}\n", kv.Key.name, kv.Key.shader.name, kv.Value.Count);
		}
		Debug.Log (matInfo);
		
		string texInfo = "";
		foreach (KeyValuePair<Texture, HashSet<Material>> kv in VisibleTextures) {
			Texture tex = kv.Key;
			texInfo += string.Format ("{0} {1} {2} {3} {4}\n", tex.name, tex.width, tex.height, kv.Value.Count, UsTextureUtil.FormatSizeString(_textureSizeLut[tex] / 1024));
		}
		Debug.Log (texInfo);
	}
	
	public UsCmd CreateMaterialCmd() {
		UsCmd cmd = new UsCmd();
		cmd.WriteNetCmd(eNetCmd.SV_FrameData_Material);
		cmd.WriteInt32 (VisibleMaterials.Count);
		
		foreach (KeyValuePair<Material, HashSet<GameObject>> kv in VisibleMaterials) {
			//Debug.Log (string.Format("current_material: {0} - {1} - {2}", kv.Key.GetInstanceID(), kv.Key.name.Length, kv.Key.name));
			cmd.WriteInt32 (kv.Key.GetInstanceID());
			cmd.WriteStringStripped (kv.Key.name);
			cmd.WriteStringStripped (kv.Key.shader.name);
			
			cmd.WriteInt32 (kv.Value.Count);
			foreach (var item in kv.Value) {
				cmd.WriteInt32 (item.GetInstanceID());
			}
		}
		return cmd;
	}
	
	public UsCmd CreateTextureCmd() {
		UsCmd cmd = new UsCmd();
		cmd.WriteNetCmd(eNetCmd.SV_FrameData_Texture);
		cmd.WriteInt32 (VisibleTextures.Count);
		
		foreach (KeyValuePair<Texture, HashSet<Material>> kv in VisibleTextures) {
			cmd.WriteInt32 (kv.Key.GetInstanceID());
			cmd.WriteStringStripped (kv.Key.name);
			cmd.WriteString (string.Format("{0}x{1}", kv.Key.width, kv.Key.height));
			cmd.WriteString (UsTextureUtil.FormatSizeString(_textureSizeLut[kv.Key] / 1024));
			
			cmd.WriteInt32 (kv.Value.Count);
			foreach (var item in kv.Value) {
				cmd.WriteInt32 (item.GetInstanceID());
			}
		}
		
		return cmd;
	}

	private FrameData _currentFrame;
	private List<FrameData> _frames = new List<FrameData>();

	#region Gathered Meshes/Materials/Textures
	
	public MeshLut MeshTable { get { return _meshLut; } }
	private MeshLut _meshLut = new MeshLut();
	
	public Dictionary<Material, HashSet<GameObject>> VisibleMaterials { get { return _visibleMaterials; } }
	private Dictionary<Material, HashSet<GameObject>> _visibleMaterials = new Dictionary<Material, HashSet<GameObject>>();
	
	public Dictionary<Texture, HashSet<Material>> VisibleTextures { get { return _visibleTextures; } }
	private Dictionary<Texture, HashSet<Material>> _visibleTextures = new Dictionary<Texture, HashSet<Material>>();
	
	private Dictionary<int, string> _nameLut = new Dictionary<int, string>();
	private Dictionary<Texture, int> _textureSizeLut = new Dictionary<Texture, int>();
	
	#endregion Gathered Meshes/Materials/Textures
}
