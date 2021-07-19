using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public List<AudioSource> AvailableSources = new List<AudioSource>();
    public AudioSource BackgroundAudio;
    public Dictionary<string, AudioHandle> Sounds = new Dictionary<string, AudioHandle>();
    public AudioSource[] AllSources = new AudioSource[8];
    private bool BackgroundPaused = false;
	void Awake()
	{
		ServiceManager.Register(this);
		BackgroundAudio = gameObject.AddComponent<AudioSource>();
		for (int i = 0; i < 8; i++)
		{
			AllSources[i] = gameObject.AddComponent<AudioSource>();
			AvailableSources.Add(AllSources[i]);
		}

		//EXAMPLE SOUND REMOVE LATER(or dont lol, maybe a fun surprise if someone ever decompiles this)
		var bonk = new AudioHandle
		{
			clip = LoadAudioFromResources("bonk"),
			Delay = 0,
			ShouldFadeIn = true,
			ShouldFadeOut = false,
			fadeDuration = 2f,
			volume = 1,
			isValid = true
		};
		AddAudio(bonk);
		//played like PlaySound("bonk");
		PlaySound("bonk");
	}

	void OnDestroy()    
    {
        ServiceManager.Unregister(this);
    }

    private void Update()
    {
        foreach (AudioSource source in AllSources)
        {
            if (source.isPlaying && AvailableSources.Contains(source))
                AvailableSources.Remove(source);
            else if (!source.isPlaying && !AvailableSources.Contains(source))
                AvailableSources.Add(source);
			
			if(!AvailableSources.Contains(source))
			{	
				if((source.clip.length - source.time ) <= Sounds[source.clip.name].fadeDuration && !Sounds[source.clip.name].IsFading && Sounds[source.clip.name].ShouldFadeOut)
				{
					StartCoroutine(Sounds[source.clip.name].fadeOut());
				}
			}
        }

        if (!BackgroundPaused && !BackgroundAudio.isPlaying && BackgroundAudio.clip != null)
        {
            if (BackgroundAudio.clip != null)
                BackgroundAudio.Play();
        }

    }

	public AudioClip LoadAudioFromResources(string name)
	{
		if (Resources.Load<AudioClip>("Sounds/" + name) != null)
		{
			return Resources.Load<AudioClip>("Sounds/" + name);
		}
		else
			LogManager.LogError("Sound not found in sounds directory");
		return null;
	}

	/// <summary>
	/// To get the handles clip use AudioManager.LoadAudioFromResources("AudioName")
	/// </summary>
	/// <param name="handle"></param>
	public void AddAudio(AudioHandle handle)
	{
		if (handle.clip != null)
		{
			if (!Sounds.ContainsValue(handle))
			{
				Sounds.Add(handle.clip.name, handle);
				handle.Init();
			}
			else
			{
				LogManager.LogError("Sounds list already contains specified sound!");
			}
		}
		else
			LogManager.LogError("AudioHandle's clip is null!");
	}

	public void SetBackgroundAudio(string SoundName)
	{
		if (Sounds.ContainsKey(SoundName))
		{
			if (Sounds[SoundName].isValid)
			{
				BackgroundAudio.clip = Sounds[SoundName].clip;
				BackgroundAudio.volume = Sounds[SoundName].volume;

				BackgroundAudio.Play();
			}
			else
				LogManager.LogWarn($"{SoundName} is currently not valid.");
		}
			LogManager.LogError("Could not play specified sound because it does not exist in the sounds array.");

	}

	public void PauseBackground()
	{
		BackgroundPaused = true;
		BackgroundAudio.Pause();
	}

	public void UnPauseBackground()
	{
		BackgroundAudio.UnPause();
		BackgroundPaused = false;
	}


	public void PlaySound(string SoundName)
	{
		if (Sounds[SoundName].isValid)
		{
			if (Sounds.ContainsValue(Sounds[SoundName]))
			{
				
					AvailableSources[0].clip = Sounds[SoundName].clip;
					AvailableSources[0].volume = Sounds[SoundName].volume;
					AvailableSources[0].PlayDelayed(Sounds[SoundName].Delay);
					
				if (Sounds[SoundName].ShouldFadeIn && Sounds[SoundName].fadeDuration > 0 && !Sounds[SoundName].IsFading)
						StartCoroutine(Sounds[SoundName].fadeIn());

			}
			else
				LogManager.LogError("Could not play specified sound because it does not exist in the sounds array.");
		}
		else
			LogManager.LogWarn($"{SoundName} is currently not valid.");
	}

	public void ForceFadeOut(string SoundName)
	{
		if (Sounds[SoundName].isValid)
		{
			if (Sounds.ContainsKey(SoundName))
				StartCoroutine(Sounds[SoundName].fadeOut());
			else
				LogManager.LogError("Could not play specified sound because it does not exist in the sounds array.");
		}
		else
			LogManager.LogWarn($"{SoundName} is currently not valid.");
	}

	public AudioSource GetAudioSourceOfHandle(string AudioName)
	{
		foreach(AudioSource source in AllSources)
		{
			if(source.clip == Sounds[AudioName].clip)
			{
				return source;
			}
		}
		return null;
	}
}