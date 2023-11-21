﻿using System;
using System.Runtime.Serialization;
using UnityEngine;

namespace SombraStudios.Shared.DependencyInjection
{

    public static class DependencyFactory
    {
        public delegate object Delegate(DependenciesProvider dependencies);

        /// <summary>
        /// generic function to create an instance using a default 
        /// constructor and calling Inject right after that.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Delegate FromClass<T>() where T : class, new()
        {
            return (dependencies) =>
            {
                var type = typeof(T);
                var obj = FormatterServices.GetUninitializedObject(type);

                dependencies.Inject(obj);

                type.GetConstructor(Type.EmptyTypes).Invoke(obj, null);

                return (T)obj;
            };
        }

        /// <summary>
        /// Instantiate the prefab, find all the MonoBehaviours in it and 
        /// call Inject for each one of them.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="prefab"></param>
        /// <returns></returns>
        public static Delegate FromPrefab<T>(T prefab) where T : MonoBehaviour
        {
            return (dependencies) =>
            {
                bool wasActive = prefab.gameObject.activeSelf;
                prefab.gameObject.SetActive(false);
                var instance = GameObject.Instantiate(prefab);
                prefab.gameObject.SetActive(wasActive);
                var childs = 
                    instance.GetComponentsInChildren<MonoBehaviour>(true);
                foreach (var child in childs)
                {
                    dependencies.Inject(child);
                }
                instance.gameObject.SetActive(wasActive);
                return instance.GetComponent<T>();
            };
        }

        /// <summary>
        /// Inject an existing GameObject in the scene
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="instance"></param>
        /// <returns></returns>
        public static Delegate FromGameObject<T>(T instance) where T : MonoBehaviour
        {
            return (dependencies) =>
            {
                var childs = 
                    instance.GetComponentsInChildren<MonoBehaviour>(true);
                foreach (var child in childs)
                {
                    dependencies.Inject(child);
                }
                return instance;
            };
        }
    }
}
