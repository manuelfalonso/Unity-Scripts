﻿using System;
using System.Collections.Generic;
using UnityEngine;
using SombraStudios.Shared.Utility.ScriptableObjects;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
#endif

namespace SombraStudios.Shared.Editor.ScriptableObjects
{
#if UNITY_EDITOR
    /// <summary>
    /// Checks for duplicate ScriptableObject IDs before building.
    /// </summary>
    public class ScriptableObjectChecker : BuildPlayerProcessor
    {
        public override int callbackOrder => -1;

        public override void PrepareForBuild(BuildPlayerContext buildPlayerContext)
        {
            var ids = new HashSet<string>();

            var guids = AssetDatabase.FindAssets("t:" + typeof(ScriptableObjectWithId));
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<ScriptableObjectWithId>(path);
                if (string.IsNullOrEmpty(so.Id))
                {
                    Debug.LogError("ScriptableObject doesn't have ID", so);
                    throw new Exception();
                }
                if (!ids.Add(so.Id))
                {
                    Debug.LogError("ScriptableObject has the same ID as some other SO", so);
                    throw new Exception();
                }
            }
        }
    }
#endif
}
