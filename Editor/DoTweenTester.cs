using UnityEngine;

/// <summary>
/// Implement abstract method to be able to test the Tween on Editor
/// </summary>
public abstract class DoTweenTester : MonoBehaviour
{
    public abstract DG.Tweening.Tween GetTween();
}