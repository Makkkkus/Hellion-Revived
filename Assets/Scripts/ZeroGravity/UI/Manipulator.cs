using UnityEngine;
using UnityEngine.InputSystem;

namespace ZeroGravity.UI
{
	public class Manipulator : MonoBehaviour
	{
		public Camera MainCamera;

		public float ManipulationSpeed = 2f;

		public Transform theOne;

		private float outDistance = 4.5f;

		private float inDistance = 1.5f;

		public GameObject testKocka;

		private void Start()
		{
		}

		private void Update()
		{
			if (Mouse.current.leftButton.wasPressedThisFrame)
			{
				RaycastHit hitInfo = default;
				if (Physics.Raycast(MainCamera.ScreenPointToRay(Mouse.current.position.ReadValue()), out hitInfo,
					    float.PositiveInfinity))
				{
					if (hitInfo.collider.gameObject.name == "Collider")
					{
						theOne = hitInfo.collider.transform.parent.transform.Find("Sphere").transform;
					}
					else
					{
						theOne = hitInfo.collider.transform;
					}
				}
				else
				{
					theOne = null;
				}
			}
			else if (Mouse.current.leftButton.isPressed && theOne != null)
			{
				Vector3 vector = MainCamera.WorldToScreenPoint(theOne.transform.position) -
				                 MainCamera.WorldToScreenPoint(transform.position);
				Vector3 vector2 = (Vector3)Mouse.current.position.ReadValue() -
				                  MainCamera.WorldToScreenPoint(transform.position);
				float num = Vector2.Angle(vector2, vector);
				if ((num > 80f && num < 110f) || (vector.magnitude.IsEpsilonEqual(vector2.magnitude, 8f) && num < 90f))
				{
					return;
				}

				if (vector2.magnitude > vector.magnitude)
				{
					if (num < 90f)
					{
						MoveOut(true);
					}
					else
					{
						MoveOut(false);
					}
				}
				else if (vector2.magnitude < vector.magnitude)
				{
					MoveOut(false);
				}
			}
			else if (Mouse.current.leftButton.wasReleasedThisFrame && theOne != null)
			{
				theOne.localPosition = new Vector3(0f, 0f, 3f);
			}

			testKocka.transform.position = MainCamera.transform.position;
			Vector3 dir = MainCamera.transform.position - theOne.position;
			Debug.DrawRay(theOne.position, dir);
		}

		private void MoveOut(bool value)
		{
			Vector3 localPosition = theOne.localPosition;
			if (value && localPosition.z <= outDistance)
			{
				theOne.localPosition = new Vector3(localPosition.x, localPosition.y,
					localPosition.z + ManipulationSpeed * Time.deltaTime);
			}
			else if (!value && localPosition.z >= inDistance)
			{
				theOne.localPosition = new Vector3(localPosition.x, localPosition.y,
					localPosition.z - ManipulationSpeed * Time.deltaTime);
			}
		}
	}
}
