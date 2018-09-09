using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GenerateAnimal : MonoBehaviour {
    public GameObject animalPrefab;
    public int quantity;

    public float x;
    public float y;
    public float z;

    // Use this for initialization
    void Start () {
       for(int i = 0; i < quantity; i++)
        {
            float posiX = Random.Range(transform.position.x - x/2, transform.position.x + x/2);
            float posiY = Random.Range(transform.position.y - y / 2, transform.position.y + y / 2);
            float posiZ = Random.Range(transform.position.z - z / 2, transform.position.z + z / 2);
            GameObject animal = Instantiate(animalPrefab);
            animal.transform.position = new Vector3(posiX, posiY, posiZ);
            animal.transform.SetParent(gameObject.transform);
        }
	}

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, 0, 0, 0.5F);
        Gizmos.DrawCube(transform.position, new Vector3(x, y, z));
    }



}
