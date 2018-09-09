using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

public class CollisionFindObject : MonoBehaviour {

    public GameObject text;
    public GameObject secretMessage;
    public List<GameObject> listItem = new List<GameObject>();
    public List<GameObject> listText = new List<GameObject>();
    private int count = 0;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
        if (count == listItem.Capacity)
        {
            secretMessage.SetActive(true);
            count++;
        }
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
                other.gameObject.SetActive(false);
                text.SetActive(false);
                for(int i = 0; i < listItem.Capacity; i++)
                {
                    if (other.gameObject.name == listItem[i].name)
                    {
                        listText[i].GetComponent<Text>().text = listItem[i].name;
                        count++;
                    }
                }
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
