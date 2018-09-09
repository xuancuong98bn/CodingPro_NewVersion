using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadGame : MonoBehaviour {

    public GameObject loadingGame;

    public Slider loadProcessSlider;

    public void OpenGame(int scenceIndex)
    {
        loadingGame.SetActive(true);
        if (scenceIndex > 1)
        {
            GameObject.FindGameObjectWithTag("MainCamera").transform.SetPositionAndRotation(loadingGame.transform.position, loadingGame.transform.rotation);
            GameObject.FindGameObjectWithTag("MainCamera").transform.localRotation = loadingGame.transform.rotation;
            //GameObject.FindGameObjectWithTag("MainCamera").transform.position = loadingGame.transform.position - new Vector3(0,0,100f) ;
        }
        StartCoroutine(LoadAsynchronously(scenceIndex));
    }
	IEnumerator LoadAsynchronously (int scenceIndex)
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(scenceIndex);   
        while(!operation.isDone)
        {
            float progess = Mathf.Clamp01(operation.progress / .9f);
            loadProcessSlider.value = progess;
            yield return null;
        }
    }
}
