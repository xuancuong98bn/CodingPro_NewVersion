using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

public class CollisionFindObject : MonoBehaviour {

    public GameObject text;

    public List<GameObject> listItem = new List<GameObject>();

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {

    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "item")
        {
            text.SetActive(true);
        }
        
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.tag == "item")
        {
            if (Input.GetKeyDown(KeyCode.F))
            {
                Debug.Log("Pressed F");
                Destroy(other.gameObject);
                text.SetActive(false);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.tag == "item")
        {
            text.SetActive(false);
        }
    }
}
