﻿using UnityEngine;

/// <summary>
/// Util Class for creating Singleton classes derived from it
/// </summary>
public class Singleton<T> : MonoBehaviour where T : Singleton<T>
{
    private static T instance;
    public static T Instance
    {
        get { return instance; }
    }

    public static bool IsInitialized
    {
        get { return instance != null; }
    }

    protected virtual void Awake()
    {
        if (instance != null)
        {
			Destroy(gameObject);      
        }
        else
        {
            instance = (T) this;
        }
    }

    protected virtual void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
