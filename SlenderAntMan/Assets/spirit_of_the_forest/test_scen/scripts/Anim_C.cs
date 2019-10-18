using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Anim_C : MonoBehaviour {

	Animator anim;
	AnimatorStateInfo cur_state;

	void Start () {
		anim = gameObject.GetComponent<Animator> ();
	}
	

	void Update () {
		cur_state = anim.GetCurrentAnimatorStateInfo(0);

		if ((cur_state.IsName("impact") == true) || (cur_state.IsName("attack_01") == true) || (cur_state.IsName("attack_02") == true) || (cur_state.IsName("attack_03") == true) || (cur_state.IsName("blocking_impact") == true) || (cur_state.IsName("idle_02") == true)) {
			anim.SetInteger ("spirit", 0);
		}

	}

	public void idle_01() {
		anim.SetInteger ("spirit", 0);
	}

	public void idle_02() {
		anim.SetInteger ("spirit", 1);
	}

	public void fly() {
		anim.SetInteger ("spirit", 2);
	}
	public void impact() {
		anim.SetInteger ("spirit", 3);
	}

	public void attack_01() {
		anim.SetInteger ("spirit", 4);
	}

	public void attack_02() {
		anim.SetInteger ("spirit", 5);
	}

	public void attack_03() {
		anim.SetInteger ("spirit", 6);
	}

	public void blocking_enter() {
		anim.SetInteger ("spirit", 7);
	}

	public void blocking_impact() {
		anim.SetInteger ("spirit", 8);
	}

	public void blocking_exit() {
		anim.SetInteger ("spirit", 9);
	}

	public void death_01() {
		anim.SetInteger ("spirit", 10);
	}

	public void death_02() {
		anim.SetInteger ("spirit", 11);
	}

	public void resurrection_01() {
		anim.SetInteger ("spirit", 12);
	}

	public void resurrection_02() {
		anim.SetInteger ("spirit", 13);
	}
}
