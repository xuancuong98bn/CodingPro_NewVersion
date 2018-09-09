using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimalControler : MonoBehaviour {

    const float esp = 0.1f;


    #region variables
    [Tooltip("speed object")]
    public float speed = 0.1f;

    [Tooltip("min X-axis")]
    private float minX = 250f;

    [Tooltip("max X-axis")]
    private float maxX = 750f;

    [Tooltip("min Y-axis")]
    private float minY = 295f;

    [Tooltip("max Y-axis")]
    private float maxY = 305f;

    [Tooltip("min Z-axis")]
    private float minZ = 650f;

    [Tooltip("max Z-axis")]
    private float maxZ = 1150f;

    public int pos = 0;
    private List<Vector3> positions;
    #endregion

    // Use this for initialization
    void Start () {
        positions = randomPosition();
        StartCoroutine(IEMove());
    }
	
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    IEnumerator IEMove()
    {
        while(true)
        {
            
            gameObject.transform.LookAt(positions[pos]);
            Vector3 dis = (positions[pos] - transform.position).normalized;
            
            transform.position += dis * speed;
            if (Vector3.Distance(positions[pos], transform.position) <= esp)
            {
                pos++;
                if(pos >= positions.Count)
                {
                    positions = randomPosition();
                    pos = 0;
                }
            }
            yield return null;
        }
    }

    List<Vector3> randomPosition()
    {
        List<Vector3> positions = new List<Vector3>();
        int n = Random.Range(50, 100);
        for(int i = 0; i < n; i++)
        {
            Vector3 position = new Vector3(Random.Range(minX, maxX), Random.Range(minY, maxY), Random.Range(minZ, maxZ));
            positions.Add(position);
        }
        return positions;
    }
}
