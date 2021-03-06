using UnityEngine;

/// <summary>
/// transform.LookAt: Simple instruction to rotate an object to look at another.
/// Quaternion.LookRotation to look at another Transform.
/// </summary>
public class LookAt : MonoBehaviour
{
    [SerializeField]
    private Transform _target;

    void Update()
    {
        //TransformLookAt();
        //QuaternionLookRotation();

        //LookAtMousePosition();
        //LookAtMouseClick();
    }

    private void TransformLookAt()
    {
        transform.LookAt(_target.position, Vector3.up);
    }

    private void QuaternionLookRotation()
    {
        Vector3 relativePos = _target.position - transform.position;
        transform.rotation = Quaternion.LookRotation(relativePos);
    }

    private void LookAtMousePosition()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, Mathf.Infinity))
        {
            transform.LookAt(hit.point);
        }
    }

    private void LookAtMouseClick()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, Mathf.Infinity))
            {
                transform.LookAt(hit.point);
            }
        }
    }
}
