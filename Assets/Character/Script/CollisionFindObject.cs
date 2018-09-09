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

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F12))
        {
            for (int i = 0; i < listItem.Capacity; i++)
            {
                listItem[i].SetActive(false);
                listText[i].GetComponent<Text>().text = listItem[i].name;
                count++;
                WinSolve();
            }
        }
    }

    void WinSolve()
    {
        if (count == listItem.Capacity)
        {
            secretMessage.SetActive(true);
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
                        WinSolve();
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
