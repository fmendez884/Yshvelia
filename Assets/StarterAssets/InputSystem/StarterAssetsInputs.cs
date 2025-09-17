using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
	public class StarterAssetsInputs : MonoBehaviour
	{
		[Header("Character Input Values")]
		public Vector2 move;
		public Vector2 look;
		public bool jump;
		public bool sprint;
		public bool punch;
		public bool a;
		public bool b;
		public bool c;
		public bool d;
		public bool modA;
		public bool modB;
		public bool modC;
		public bool modD;

		[Header("Movement Settings")]
		public bool analogMovement;

		[Header("Mouse Cursor Settings")]
		public bool cursorLocked = true;
		public bool cursorInputForLook = true;

#if ENABLE_INPUT_SYSTEM
		public void OnMove(InputValue value)
		{
			MoveInput(value.Get<Vector2>());
		}

		public void OnLook(InputValue value)
		{
			if(cursorInputForLook)
			{
				LookInput(value.Get<Vector2>());
			}
		}

		public void OnJump(InputValue value)
		{
			JumpInput(value.isPressed);
		}
		
		public void OnPunch(InputValue value)
		{
			PunchInput(value.isPressed);
			Debug.Log("Punch value is pressed");
		}

		public void OnA(InputValue value)
		{
			A(value.isPressed);
			Debug.Log("A_ value is pressed");
		}public void OnB(InputValue value)
		{
			B(value.isPressed);
			Debug.Log("B_ value is pressed");
		}public void OnC(InputValue value)
		{
			C(value.isPressed);
			Debug.Log("C_ value is pressed");
		}public void OnD(InputValue value)
		{
			D(value.isPressed);
			Debug.Log("D_ value is pressed");
		}

		public void OnModA(InputValue value)
		{
			ModA(value.isPressed);
			Debug.Log("modA_ value is pressed");
		}public void OnModB(InputValue value)
		{
			ModB(value.isPressed);
			Debug.Log("modB_ value is pressed");
		}public void OnModC(InputValue value)
		{
			ModC(value.isPressed);
			Debug.Log("modC_ value is pressed");
		}public void OnModD(InputValue value)
		{
			ModD(value.isPressed);
			Debug.Log("modD_ value is pressed");
		}

		public void OnSprint(InputValue value)
		{
			SprintInput(value.isPressed);
		}
#endif


		public void MoveInput(Vector2 newMoveDirection)
		{
			move = newMoveDirection;
		} 

		public void LookInput(Vector2 newLookDirection)
		{
			look = newLookDirection;
		}

		public void JumpInput(bool newJumpState)
		{
			jump = newJumpState;
		}

		public void SprintInput(bool newSprintState)
		{
			sprint = newSprintState;
		}

		public void PunchInput(bool newPunchState)
		{
			Debug.Log("Punch Input Pressed");
			punch = newPunchState;
		}

		public void A(bool new_A_State)
		{
			Debug.Log("A Pressed");
			a = new_A_State;
		}
		
		public void B(bool new_B_State)
		{
			Debug.Log("B Pressed");
			b = new_B_State;
		}

		public void C(bool new_C_State)
		{
			Debug.Log("C Pressed");
			c = new_C_State;
		}

		public void D(bool new_D_State)
		{
			Debug.Log("D Pressed");
			d = new_D_State;
		}

		public void ModA(bool new_modA_State)
		{
			Debug.Log("modA Pressed");
			modA = new_modA_State;
		}

		public void ModB(bool new_modB_State)
		{
			Debug.Log("modB Pressed");
			modB = new_modB_State;
		}

		public void ModC(bool new_modC_State)
		{
			Debug.Log("modC Pressed");
			modC = new_modC_State;
		}

		public void ModD(bool new_modD_State)
		{
			Debug.Log("modD Pressed");
			modD = new_modD_State;
		}

		private void OnApplicationFocus(bool hasFocus)
		{
			SetCursorState(cursorLocked);
		}

		private void SetCursorState(bool newState)
		{
			Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
		}
	}
	
}