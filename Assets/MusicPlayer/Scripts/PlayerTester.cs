using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerTester : MonoBehaviour {

    public string path = "";

    public bool TogglePlay = false;
    	
	// Update is called once per frame
	void Update () {
		if(TogglePlay)
        {
            TogglePlay = false;
            MusicPlayer.Play(path);
        }
	}
}
