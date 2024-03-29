﻿using UnityEngine;

namespace SombraStudios.Shared.Gameplay
{
    /// <summary>
    /// Rotate over a traget trying to reach its center + offset
    /// </summary>
    public class GravityScript : MonoBehaviour
    {
        [SerializeField]
        private Transform target;

        [SerializeField]
        private Vector3 targetOffSet = new Vector3(0, 1.5f, 0);
        [SerializeField]
        private float speed = 3;


        void Update()
        {
            Gravitate();
        }


        private void Gravitate()
        {
            Vector3 relativePos = (target.position + targetOffSet) - transform.position;
            Quaternion rotation = Quaternion.LookRotation(relativePos);

            Quaternion current = transform.localRotation;

            transform.localRotation = Quaternion.Slerp(current, rotation, Time.deltaTime);
            transform.Translate(0, 0, speed * Time.deltaTime);
        }
    }
}