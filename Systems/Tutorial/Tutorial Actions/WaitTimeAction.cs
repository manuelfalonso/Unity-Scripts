using System.Collections;
using UnityEngine;

namespace SombraStudios.TutorialSystem
{
    /// <summary>
    /// Action for waiting time in seconds
    /// </summary>
    [CreateAssetMenu(fileName = "New Tutorial Action", menuName = "SombraStudios/Tutorial/Wait Time Action", order = 1)]
    public class WaitTimeAction : TutorialAction
    {
        [Header("Data")]
        [SerializeField]
        private float _timeToWaitInSeconds;


        public override IEnumerator ExecuteAction()
        {
            if (!_active)
                yield break;

            yield return new WaitForSeconds(_timeToWaitInSeconds);

            IsCompleted = true;
        }
    }
}
