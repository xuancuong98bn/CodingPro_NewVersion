using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemController : MonoBehaviour {

    public GameObject particalSys;

    private void OnDestroy()
    {
        GameObject par = Instantiate(particalSys);
        par.transform.position = transform.position;
        par.GetComponent<ParticleSystem>().Play();
    }
}
