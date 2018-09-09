using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MKController : MonoBehaviour {

    public Animator anim;
    public Rigidbody rigit;
    public float nextTime = 0;
    public float sumTime = 0;
    private float inputH;
    private float inputV;

    // Use this for initialization
    void Start () {
        anim = GetComponent <Animator>();
        rigit = GetComponent<Rigidbody>();
        rigit.velocity = new Vector3(-50, 0, -50);
    }
	
	// Update is called once per frame
	void Update () {
        if(sumTime >= nextTime)
        {
            int n = Random.Range(0, 2);
            if (n == 0)
            {
                anim.Play("MK_walk1", -1, 0f);
            }
            if (n == 1)
            {
                anim.Play("MK_runningForward", -1, 0f);
            }
            else
            {
                anim.Play("MK_stabJumpFward", -1, 0f);
            }
            inputH = Input.GetAxis("Horizontal");
            inputV = Input.GetAxis("Vertical");

            anim.SetFloat("inputH", inputH);
            anim.SetFloat("inputV", inputV);

            inputH = Input.GetAxis("Horizontal");
            inputV = Input.GetAxis("Vertical");

            anim.SetFloat("inputH", inputH);
            anim.SetFloat("inputV", inputV);

            float moveX = inputH * 50 * Time.deltaTime;
            float moveZ = inputV * -150 * Time.deltaTime;

            rigit.velocity = new Vector3(moveX, 0f, moveZ);

            nextTime += Random.Range(5, 10);
        }
        sumTime += Time.deltaTime;
    }
}
