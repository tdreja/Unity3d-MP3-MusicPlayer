using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NAudio;
using System.IO;
using NAudio.Wave;
using System;
using System.Threading;

/// <summary>
/// This class provides a single music player instance to a Unity scene.
/// It can open OGG, MP3 and WAV files from anywhere (harddrive or network) and play them as clips in Unity
/// It is based upon the NAudio Library and WavUtility for MP3 playback.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class MusicPlayer : Singleton<MusicPlayer> {

    #region Enums and Constants

    /// <summary>
    /// Prefix for local files
    /// </summary>
    private const string FILE_PREFIX = "file://";

    /// <summary>
    /// Prefix for http connections
    /// </summary>
    private const string HTTP_PREFIX = "http://";

    /// <summary>
    /// Prefix for https connections
    /// </summary>
    private const string HTTPS_PREFIX = "https://";

    /// <summary>
    /// All allowed file types for the MusicPlayer (OGG, MP3 and WAV)
    /// </summary>
    public enum FileType
    {
        /// <summary>
        /// OGG Audio File
        /// </summary>
        OGG,

        /// <summary>
        /// MP3 Audio File
        /// </summary>
        MP3,

        /// <summary>
        /// WAV Audio File
        /// </summary>
        WAV,
    }

    #endregion

    #region Variables
    
    /// <summary>
    /// Time the song will fadeout after stopping
    /// </summary>
    [Range(0, 5)]
    public float FadeOutSeconds = 1f;

    /// <summary>
    /// Is the programming currently loading the next audio file or not
    /// </summary>
    public bool IsLoading { get; private set; }

    /// <summary>
    /// URL of the file that is currently being loaded/opened
    /// </summary>
    private string fileURL;

    /// <summary>
    /// Reference to the attached AudioSource for playback
    /// </summary>
    private AudioSource AudioSource;

    /// <summary>
    /// FadeOut End Time
    /// </summary>
    private float mFadeEndTime = 0;

    /// <summary>
    /// Next clip to be played (used if the currently clip is still fading out)
    /// </summary>
    private AudioClip waitingClip;

    /// <summary>
    /// True: Clip is fading out, False: No fading active right now
    /// </summary>
    private bool IsFadeOut;

    /// <summary>
    /// True: Once fadeout is complete, play the waitingClip. Otherwise just stop
    /// </summary>
    private bool PlayOnceFaded;

    #endregion

    #region Static Functions

    /// <summary>
    /// Buffer for the raw data from an MP3 file
    /// </summary>
    private static byte[] RawMusicFile;

    /// <summary>
    /// Could the raw bytes be converted into uncompressed audio
    /// </summary>
    private static bool LoadSuccess;

    /// <summary>
    /// Buffer for the raw WAVE data (uncompressed from a MP3 file)
    /// </summary>
    private static byte[] RawWaveFile;

    /// <summary>
    /// Is currently an audiofile playing or not?
    /// </summary>
    /// <returns>True: File is Playing, False: No file playing/last file is fading out</returns>
    public static bool IsPlaying()
    {
        return !Instance.IsFadeOut && Instance.AudioSource.isPlaying;
    }

    /// <summary>
    /// Plays the given audio file
    /// </summary>
    /// <param name="file">Path of the audio file</param>
    public static void Play(string file)
    {
        Instance.PlaySong(file);
    }

    /// <summary>
    /// Plays the given audio clip
    /// </summary>
    /// <param name="audioClip">Audioclip from the assets</param>
    public static void Play(AudioClip audioClip)
    {
        if (!Instance.IsLoading)
        {
            Instance.StartClip(audioClip);
        }
    }

    /// <summary>
    /// Stops the currently playing song (if one is playing)
    /// </summary>
    /// <param name="fadeOut">True: Song will fadeout, False: Song will stop immediately</param>
    public static void Stop(bool fadeOut = true)
    {
        Instance.StopSong(fadeOut);
    }

    /// <summary>
    /// Returns the overall length of the currently playing song in seconds or -1 if no song is playing
    /// </summary>
    /// <returns>Length of song in seconds, -1 if no song is playing</returns>
    public static float GetLength()
    {
        if (Instance.AudioSource.clip != null)
        {
            return Instance.AudioSource.clip.length;
        }
        return -1;
    }

    /// <summary>
    /// Returns the current timecode of the currently playing song (the position along the track) in seconds elapsed since play start.
    /// </summary>
    /// <returns>Timecode in seconds</returns>
    public static float GetTimeCode()
    {
        if (Instance.AudioSource.isPlaying)
        {
            return Instance.AudioSource.time;
        }
        return 0;
    }

    /// <summary>
    /// Once the mp3 music file has been loaded as raw bytes, this function can uncompress them into a WAV clip.
    /// </summary>
    private static void LoadMp3AsClip()
    {
        if (RawMusicFile != null && RawMusicFile.Length > 0)
        {
            MemoryStream waveStream = new MemoryStream();
            Mp3FileReader mp3Stream = new Mp3FileReader(new MemoryStream(RawMusicFile));
            WaveFileWriter.WriteWavFileToStream(waveStream, mp3Stream);
            RawWaveFile = waveStream.ToArray();
            LoadSuccess = true;
        }
        else
        {
            LoadSuccess = false;
        }
    }

    #endregion

	/// <summary>
    /// Unity Start fetches the AudioSource and prepares the player
    /// </summary>
	void Start () {
        AudioSource = GetComponent<AudioSource>();
        IsFadeOut = false;
	}
	
	/// <summary>
    /// Unity Update checks for FadeOut and starts playing the next song if fadeout is finished.
    /// It also checks if the loading routine (for Mp3) is finished and plays that song next
    /// </summary>
	void Update () {
        if(IsFadeOut)
        {
            if(Time.time >= mFadeEndTime)
            {
                AudioSource.Stop();
                IsFadeOut = false;
            } else
            {
                AudioSource.volume = Mathf.Lerp(0, 1, (mFadeEndTime - Time.time) / FadeOutSeconds);
            }
        }
        else if(PlayOnceFaded)
        {
            PlayOnceFaded = false;
            AudioSource.clip = waitingClip;
            AudioSource.volume = 1f;
            AudioSource.Play();
        }

		if(LoadSuccess)
        {
            LoadSuccess = false;
            StartClip(WavUtility.ToAudioClip(RawWaveFile));
        }
	}

    /// <summary>
    /// Stops the currently playing song, if fadeout is set, the song will fadeout before stopping
    /// </summary>
    /// <param name="fadeOut">True: Song will fadeout for FadeOutSeconds time, False: Song will immediately stop</param>
    private void StopSong(bool fadeOut)
    {
        if(AudioSource.isPlaying && !IsFadeOut)
        {
            if (fadeOut && FadeOutSeconds > 0)
            {
                mFadeEndTime = Time.time + FadeOutSeconds;
                IsFadeOut = true;
            }
            else
            {
                IsFadeOut = false;
                AudioSource.Stop();
            }
        }
    }

    /// <summary>
    /// Starts playing the given clip. If we're still fading out the previous clip, the start will wait
    /// </summary>
    /// <param name="mClip">New audioclip to be played</param>
    private void StartClip(AudioClip mClip)
    {
        IsLoading = false;

        if(IsFadeOut)
        {
            waitingClip = mClip;
            PlayOnceFaded = true;
        }
        else
        {
            waitingClip = null;
            PlayOnceFaded = false;
            AudioSource.clip = mClip;
            AudioSource.volume = 1f;
            AudioSource.Play();
        }
    }

    /// <summary>
    /// Plays the given file in the audio player. MP3s need to be converted beforehand (may take a few seconds), WAV and OGG can be played directly.
    /// </summary>
    /// <param name="file">Path to file to be opened and played</param>
    public void PlaySong(string file)
    {
        if(!IsLoading && file != null)
        {
            if (file.StartsWith(FILE_PREFIX) || file.StartsWith(HTTP_PREFIX) || file.StartsWith(HTTPS_PREFIX))
            {
                fileURL = file;
            } else
            {
                fileURL = FILE_PREFIX + file;
            }
            
            LoadSuccess = false;
            IsLoading = true;

            StartCoroutine(LoadFile(fileURL));
        }
    }
    
    /// <summary>
    /// Starts a new coroutine that loads the content of the given file
    /// </summary>
    /// <param name="fileURL">Path of the file to be opened</param>
    /// <returns>IEnumerator for the coroutine</returns>
    private IEnumerator LoadFile(string fileURL)
    {
        using (WWW www = new WWW(fileURL))
        {
            yield return www;
            StartWaveThread(www, GetFileType(fileURL));
        }
    }

    /// <summary>
    /// Returns the type of the given file. Only MP3, WAV and OGG supported!
    /// </summary>
    /// <param name="fileURL">Path of the file</param>
    /// <returns>MP3, OGG or WAV</returns>
    private FileType GetFileType(string fileURL)
    {
        if(fileURL.EndsWith(".mp3"))
        {
            return FileType.MP3;
        } else if(fileURL.EndsWith(".wav"))
        {
            return FileType.WAV;
        }
        return FileType.OGG;
    }

    /// <summary>
    /// Converts the downloaded data into an audio clip
    /// </summary>
    /// <param name="loadedData">Data loaded from harddrive or network</param>
    /// <param name="type">Type of the Data (OGG, MP3 or WAV)</param>
    private void StartWaveThread(WWW loadedData, FileType type)
    {
        LoadSuccess = false;
        RawWaveFile = null;
        RawMusicFile = null;

        switch (type)
        {
            case FileType.MP3:
                RawMusicFile = loadedData.bytes;
                Thread tr = new Thread(LoadMp3AsClip);
                tr.Start();
                break;
            case FileType.OGG:
                StartClip(loadedData.GetAudioClipCompressed());
                break;
            case FileType.WAV:
                StartClip(loadedData.GetAudioClip());
                break;
        }
    }

    
}
