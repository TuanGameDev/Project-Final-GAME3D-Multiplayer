﻿using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
using System.Collections;

public class VideoPlayerController : MonoBehaviour
{
    public VideoPlayer videoPlayer;
    public Camera _camera;
    public TextMeshProUGUI skipText;
    public float fadeDuration = 1f;

    private bool isSkipping;

    private void Start()
    {
        videoPlayer.loopPointReached += VideoPlayer_OnLoopPointReached;
        videoPlayer.Play();
        _camera.gameObject.SetActive(false);
        skipText.gameObject.SetActive(false);
        StartCoroutine(FadeInSkipText());
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.E) && !isSkipping && skipText.gameObject.activeSelf)
        {
            SkipVideo();
        }
    }

    private void VideoPlayer_OnLoopPointReached(VideoPlayer source)
    {
        StartCoroutine(FadeOutVideo());
    }

    private void SkipVideo()
    {
        videoPlayer.Stop();
        gameObject.SetActive(false);
        _camera.gameObject.SetActive(true);
    }

    private IEnumerator FadeInSkipText()
    {
        skipText.gameObject.SetActive(true);
        Color startColor = skipText.color;
        startColor.a = 0f;
        skipText.color = startColor;
        float startTime = Time.time;
        float endTime = startTime + fadeDuration;

        while (Time.time < endTime)
        {
            float timeRatio = (Time.time - startTime) / fadeDuration;
            float alpha = Mathf.Lerp(0f, 1f, timeRatio);
            Color currentColor = skipText.color;
            currentColor.a = alpha;
            skipText.color = currentColor;

            yield return null;
        }
        Color finalColor = skipText.color;
        finalColor.a = 1f;
        skipText.color = finalColor;

        isSkipping = false;
    }

    private IEnumerator FadeOutVideo()
    {
        float startTime = Time.time;
        float endTime = startTime + fadeDuration;

        while (Time.time < endTime)
        {
            float timeRatio = (Time.time - startTime) / fadeDuration;
            float maxDarkness = Mathf.Lerp(0f, 1f, timeRatio);
            _camera.backgroundColor = new Color(0f, 0f, 0f, maxDarkness);

            yield return null;
        }
        _camera.backgroundColor = new Color(0f, 0f, 0f, 1f);

        SkipVideo();
    }
}