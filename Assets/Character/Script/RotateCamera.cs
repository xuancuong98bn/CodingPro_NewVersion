﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateCamera : MonoBehaviour {
    public float speedH = 2.0f;

    private float yaw = 0.0f;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
        yaw += speedH * Input.GetAxis("Mouse X");

        transform.eulerAngles = new Vector3(0.0f, yaw, 0.0f);
	}
}
