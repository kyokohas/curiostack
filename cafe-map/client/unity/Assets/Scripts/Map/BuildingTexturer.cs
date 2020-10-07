﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CafeMap.Services;
using Google.Maps;
using Google.Maps.Feature;
using Google.Maps.Feature.Shape;
using Google.Maps.Feature.Style;
using ModestTree;
using UnityEngine;
using Zenject;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace CafeMap.Map
{
    public sealed class BuildingTexturer : MonoBehaviour
    {
        [Tooltip(
            "Materials to apply to buildings walls. The specific Materials chosen must use a " +
            "Shader that is set up to work with Nine Slicing. For more info on how this is achieved, " +
            "examine the Shader: " +
            "Assets/GoogleMaps/Examples/Materials/NineSlicing/BuildingWalls.shader.")]
        public Material[] WallMaterials;

        [Tooltip(
            "Materials to apply to roofs (must be the same number of Materials as Building Wall " +
            "Materials array, as we will try to match given Walls to given Roofs (e.g. if a building " +
            "is given Building Wall Material 2, then it will also be given Building Roof Material 2).")]
        public Material[] RoofMaterials;

        private readonly Dictionary<StructureMetadata.UsageType, Object> buildingModels 
            = new Dictionary<StructureMetadata.UsageType, Object>();

        private PlacesService _placesService;

        /// <summary>
        /// Verify given <see cref="Material"/> arrays are valid (not empty nor containing any null
        /// entries, and both arrays of the same length).
        /// </summary>
        private void Awake()
        {
            // Verify that at least one Wall Material and at least one Roof Material has been given.
            if (WallMaterials.Length == 0)
            {
                Debug.LogError(Errors.EmptyArray(this, WallMaterials, "Wall Materials"));

                return;
            }

            if (RoofMaterials.Length == 0)
            {
                Debug.LogError(Errors.EmptyArray(this, RoofMaterials, "Roof Materials"));

                return;
            }

            // Verify that the same number of Wall and Roof Materials have been given.
            if (WallMaterials.Length != RoofMaterials.Length)
            {
                Debug.LogErrorFormat(
                    "Incorrect number of Building Roof Materials defined for {0}.{1}: {2} " +
                    "Building Wall Materials were given, but {3} Building Roof Materials were given." +
                    "\n{1} needs the same number of Building Roof Materials as Building Wall " +
                    "Materials, i.e. {2} of each.",
                    name,
                    GetType(),
                    WallMaterials.Length,
                    RoofMaterials.Length);

                return;
            }

            // Verify that no null Materials have been given.
            for (int i = 0; i < WallMaterials.Length; i++)
            {
                if (WallMaterials[i] == null)
                {
                    Debug.LogError(Errors.NullArrayElement(this, WallMaterials, "Wall Materials", i));

                    return;
                }

                if (RoofMaterials[i] == null)
                {
                    Debug.LogError(Errors.NullArrayElement(this, RoofMaterials, "Roof Materials", i));

                    return;
                }
            }

            /*
            buildingModels.Add(StructureMetadata.UsageType.Bar, Resources.Load("Prefabs/Buildings/25_Building_Bar"));
            buildingModels.Add(StructureMetadata.UsageType.Cafe,
                Resources.Load("Prefabs/Buildings/21_Building_Coffee Shop"));
            buildingModels.Add(StructureMetadata.UsageType.Restaurant,
                Resources.Load("Prefabs/Buildings/21_Building_Coffee Shop"));
            buildingModels.Add(StructureMetadata.UsageType.School, Resources.Load("Prefabs/Buildings/19_Building_Factory"));
            buildingModels.Add(StructureMetadata.UsageType.Shopping,
                Resources.Load("Prefabs/Buildings/02_Building_Super Market"));
                */
        }

        [Inject]
        public void Init(MapsService mapsService, PlacesService placesService)
        {
            this._placesService = placesService;
            
            mapsService.Events.ExtrudedStructureEvents.WillCreate.AddListener(args =>
            {
                if (buildingModels.TryGetValue(args.MapFeature.Metadata.Usage, out var model))
                {
                    if (args.MapFeature.GetShape() is ExtrudedArea area && area.Extrusions.Length == 1)
                    {
                        var vertices = area.Extrusions[0].FootPrint.Vertices;
                        if (vertices.Length >= 2)
                        {
                            var edge = vertices[1] - vertices[0];
                            float angle = Vector2.Angle(edge, Vector2.right);
                            var rotation = Quaternion.AngleAxis(angle + 90, Vector3.up);
                            GameObject prefab = (GameObject) Instantiate(model, Vector3.zero, rotation);

                            Bounds bounds = prefab.GetComponent<Renderer>().bounds;

                            float minBoundsSize = Math.Min(bounds.size.x, bounds.size.z);
                            float minEdgeSize = float.MaxValue;
                            for (int i = 0; i < vertices.Length - 1; i++)
                            {
                                var edgeSize = (vertices[i + 1] - vertices[i]).magnitude;
                                if (edgeSize < minEdgeSize)
                                {
                                    minEdgeSize = edgeSize;
                                }
                            }

                            float scale = minEdgeSize / minBoundsSize;
                            
                            prefab.transform.localScale = new Vector3(scale, scale, scale);
                            ExtrudedStructureStyle style =
                                new ExtrudedStructureStyle.Builder {Prefab = prefab}.Build();
                            args.Style = style;
                        }
                    }
                }
            });
            
            mapsService.Events.ExtrudedStructureEvents.DidCreate.AddListener(args =>
            {
                var placeName = args.MapFeature.Metadata.Name;
                if (placeName.IsEmpty())
                {
                    return;
                }

                if (args.MapFeature.Metadata.Usage != StructureMetadata.UsageType.Unspecified)
                {
                    return;
                }
                var mapObject = args.GameObject;
                var nameObject = Instantiate(new GameObject(), mapObject.transform);
                nameObject.name = "Name: " + placeName;
                nameObject.transform.Translate(0, 0, 0);
                var name = nameObject.AddComponent<TextMesh>();
                name.text = placeName;
                name.fontSize = 300;
                name.fontStyle = FontStyle.Bold;
                name.color = Color.black;
                name.anchor = TextAnchor.LowerCenter;
            });
            
            // Sign up to event called after each new building is loaded, so can assign Materials to this
            // new building. Note that:
            // - DynamicMapsService.MapsService is auto-found on first access (so will not be null).
            // - This event must be set now during Awake, so that when Dynamic Maps Service starts loading
            //   the map during Start, this event will be triggered for all Extruded Structures.
            mapsService.Events.ExtrudedStructureEvents.DidCreate.AddListener(
                args =>
                {
                    
                    
                    if (!buildingModels.ContainsKey(args.MapFeature.Metadata.Usage))
                    {
                        AssignNineSlicedMaterials(args.GameObject);
                    }
                    
                });
        }

        /// <summary>
        /// Assign a randomly chosen Wall and Roof <see cref="Material"/> to a given building.
        /// </summary>
        /// <param name="building">Building to assign <see cref="Material"/>s to.</param>
        /// <param name="index">
        /// Optional index of Wall and Roof <see cref="Material"/> pair to apply. If this value is not
        /// set a random <see cref="Material"/> pair will be used.
        /// </param>
        internal void AssignNineSlicedMaterials(GameObject building, int? index = null)
        {
            // If a specific Material index was given, verify it is a valid index for a Wall and Roof
            // Material pair.
            if (index.HasValue)
            {
                if (index.Value < 0 || index.Value >= WallMaterials.Length)
                {
                    Debug.LogError(
                        Errors.InvalidArrayIndex(this, WallMaterials, "Wall Materials", index.Value));

                    return;
                }
            }
            else
            {
                // Pick a random Material index to use for both Wall and Roof Materials. Not that the same
                // index will work for both arrays of Materials, as we have already verified that the Wall
                // and Roof Material arrays are the same length.
                index = Random.Range(0, WallMaterials.Length);
            }

            // Replace building MeshRenderer's sharedMaterials array with chosen Materials. Note that this
            // must be done by creating a new array of Materials, rather than altering the entries of this
            // MeshRenderer's sharedMaterials array, as altering the existing array will not actually
            // change the MeshRenderer's Materials.
            MeshRenderer buildingMeshRenderer = building.GetComponent<MeshRenderer>();

            buildingMeshRenderer.sharedMaterials =
                new[] {WallMaterials[index.Value], RoofMaterials[index.Value]};
        }

        private static void addPrefabForType(StructureMetadata.UsageType type, string path,
            Dictionary<StructureMetadata.UsageType, ExtrudedStructureStyle> mapping)
        {
            Object prefabModel = Resources.Load(path);
            GameObject prefab = (GameObject) Instantiate(prefabModel, Vector3.zero, Quaternion.identity);
            ExtrudedStructureStyle extrudedPrefabStyle =
                new ExtrudedStructureStyle.Builder {Prefab = prefab}.Build();
            mapping.Add(type, extrudedPrefabStyle);
        }

        private async void logRefreshedPlaceId(string placeId)
        {
            string refreshed = await _placesService.refreshPlaceId(placeId);
            Debug.Log("old: " + placeId);
            Debug.Log("new: " + refreshed);
        }
    }
}