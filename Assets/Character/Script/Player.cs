using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

public class Player : MonoBehaviour {
    private Animator anim;
    private float inputH;
    private float inputV;

    private Rigidbody rbody;
    private bool run;
    private bool isStandWin = false;

    public float velocity;
    public float veloRun;
    public float jump;
    CapsuleCollider cap;
    public LayerMask layerCollision;

    public GameObject bag;
    public GameObject tutorial;

    public GameObject winObject;
    public GameObject respawnObject;
    public Button nextStage;

    // Use this for initialization
    void Start () {
        anim = GetComponent<Animator>();
        rbody = GetComponent<Rigidbody>();
        run = false;
        cap = GetComponent<CapsuleCollider>();
    }
	
	// Update is called once per frame
	void Update () {
		if(Input.GetKeyDown(KeyCode.J))
        {
            anim.Play("WAIT01", -1, 0f);
        }
        if (Input.GetKeyDown(KeyCode.K))
        {
            anim.Play("WAIT02", -1, 0f);
        }
        if (Input.GetKeyDown(KeyCode.L))
        {
            anim.Play("WAIT03", -1, 0f);
        }
        if (Input.GetKeyDown(KeyCode.H))
        {
            anim.Play("WAIT04", -1, 0f);
        }
        if (Input.GetKeyDown(KeyCode.B))
        {
            if (tutorial.activeSelf) tutorial.SetActive(false);
            bag.SetActive(true);

        } else if (Input.GetKeyUp(KeyCode.B))
        {
            bag.SetActive(false);
        }
        if (Input.GetKeyDown(KeyCode.T))
        {
            if (bag.activeSelf) bag.SetActive(false);
            tutorial.SetActive(true);

        }
        else if (Input.GetKeyUp(KeyCode.T))
        {
            tutorial.SetActive(false);
        }
        if (Input.GetKeyDown(KeyCode.J) && isStandWin)
        {
            StartCoroutine(Waiting());
        }
    }

    private void FixedUpdate()
    {
        if (Input.GetKey(KeyCode.LeftShift))
        {
            run = true;
        }
        else
        {
            run = false;
        }

        if (Input.GetKeyDown(KeyCode.Space) && IsGrounded())
        {
            anim.SetTrigger("jump");
            rbody.AddForce(new Vector3(0f, jump, 0f), ForceMode.Impulse);
        }

        inputH = Input.GetAxis("Horizontal");
        inputV = Input.GetAxis("Vertical");

        anim.SetFloat("inputH", inputH);
        anim.SetFloat("inputV", inputV);
        anim.SetBool("run", run);

        float moveX = inputH * velocity * Time.deltaTime;
        float moveZ = inputV * velocity * Time.deltaTime;

        if (moveZ <= 0f)
        {
            moveX = 0f;
        }
        else if (run)
        {
            moveX *= veloRun;
            moveZ *= veloRun;
        }

        transform.Translate(new Vector3(moveX, 0f, moveZ));
    }
    private bool IsGrounded()
    {
        Debug.DrawRay(transform.position - new Vector3(0, cap.height / 2 - 1f, 0), Vector3.down * 0.3f, Color.red, 10);
        RaycastHit hit;
        if (Physics.Raycast(transform.position - new Vector3(0, cap.height / 2- 1f, 0),Vector3.down, out hit, 0.3f, layerCollision))
        {
            Debug.Log(hit.collider.gameObject.name);
            return true;
        }
            
        else return false;
    }

    IEnumerator Waiting()
    {
        yield return new WaitForSeconds(0.5f);
        winObject.SetActive(true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Win"))
        {
            isStandWin = true;
        }
        if (other.gameObject.name == "Cylinder")
        {
            GameObject.FindGameObjectWithTag("Fire").GetComponent<ParticleSystem>().Play();
            if (GameObject.FindGameObjectWithTag("Sun").transform.rotation.x > 0)
            {
                GameObject.FindGameObjectWithTag("Sun").transform.Rotate(new Vector3(-140, 0, 0));
                StartCoroutine(WaitingWin());
            }
        }
        Debug.Log(other.gameObject.layer + " va "+ LayerMask.NameToLayer("Dead"));
        if (other.gameObject.layer == LayerMask.NameToLayer("Dead"))
        {
            gameObject.transform.position = respawnObject.transform.position;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Win"))
        {
            isStandWin = false;
        }
    }

    IEnumerator WaitingWin()
    {
        yield return new WaitForSeconds(2f);
        nextStage.gameObject.SetActive(true);
    }
}
