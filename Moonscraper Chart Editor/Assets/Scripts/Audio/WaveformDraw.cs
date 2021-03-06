﻿// Copyright (c) 2016-2017 Alexander Ong
// See LICENSE in project root for license information.

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class WaveformDraw : MonoBehaviour {
    ChartEditor editor;
    LineRenderer lineRen;

    SampleData currentSample = null;

    public Dropdown waveformSelect;
    public Text loadingText;

    int chartViewWaveformSelectionIndex = 0;
    int songViewWaveformSelectionIndex = 1;

    // Use this for initialization
    void Start () {
        editor = GameObject.FindGameObjectWithTag("Editor").GetComponent<ChartEditor>();
        lineRen = GetComponent<LineRenderer>();
        lineRen.sortingLayerName = "Highway";

        EventsManager.onViewModeSwitchEventList.Add(OnViewModeSwitch);
    }

    void OnViewModeSwitch(Globals.ViewMode viewMode)
    {
        int newIndex = 0;

        if (viewMode == Globals.ViewMode.Chart)
            newIndex = chartViewWaveformSelectionIndex;
        else if (viewMode == Globals.ViewMode.Song)
            newIndex = songViewWaveformSelectionIndex;

        waveformSelect.value = newIndex;
    }

    readonly int audioInstrumentEnumCount = System.Enum.GetValues(typeof(Song.AudioInstrument)).Length;
    // Update is called once per frame
    void Update () {
        currentSample = null;
        for (int audioIndex = 0; audioIndex < audioInstrumentEnumCount; ++audioIndex)
        {
            if (waveformSelect.value == (audioIndex + 1))
            {
                currentSample = editor.currentSong.GetSampleData((Song.AudioInstrument)audioIndex);
            }
        }

        if (Globals.viewMode == Globals.ViewMode.Chart)
            chartViewWaveformSelectionIndex = waveformSelect.value;
        else if (Globals.viewMode == Globals.ViewMode.Song)
            songViewWaveformSelectionIndex = waveformSelect.value;

        bool displayWaveform = waveformSelect.value > 0;// && Globals.viewMode == Globals.ViewMode.Song;
        //waveformSelect.gameObject.SetActive(Globals.viewMode == Globals.ViewMode.Song);

        loadingText.gameObject.SetActive(displayWaveform && currentSample.IsLoading);

        // Choose whether to display the waveform or not
        if (displayWaveform && currentSample != null && currentSample.data.Length > 0)
        {
            UpdateWaveformPointsFullData();

            // Then activate
            lineRen.enabled = true;
        }
        else
        {
            lineRen.numPositions = 0;
            lineRen.enabled = false;
        }
    }

    void UpdateWaveformPointsFullData()
    {
        if (currentSample.data.Length <= 0 || currentSample == null)
        {
            return;
        }

        float sampleRate = currentSample.length / currentSample.data.Length;// currentClip.samples / currentClip.length;
        float scaling = 1;
        const int iteration = 1;// 20;
        int channels = 1;// currentSample.channels;
        float fullOffset = -editor.currentSong.offset;

        // Determine what points of data to draw
        int startPos = timeToArrayPos(TickFunctions.WorldYPositionToTime(editor.camYMin.position.y) - fullOffset, iteration, channels, currentSample.length);
        int endPos = timeToArrayPos(TickFunctions.WorldYPositionToTime(editor.camYMax.position.y) - fullOffset, iteration, channels, currentSample.length);

        Vector3[] points = new Vector3[endPos - startPos];
#if false
        if (currentSample.clip > 0)
            scaling = (MAX_SCALE / currentSample.clip);
#endif

        // Turn data into world-position points to feed the line renderer
        Vector3 point = Vector3.zero;
        float[] currentSampleData = currentSample.data;
        float hs = GameSettings.hyperspeed / GameSettings.gameSpeed;

        for (int i = startPos; i < endPos; ++i)
        {
            point.x = currentSampleData[i] * scaling;

            // Manual inlining of Song.TimeToWorldYPosition
            float time = i * sampleRate + fullOffset;
            point.y = time * hs;

            points[i - startPos] = point;
        }

        lineRen.numPositions = points.Length;
        lineRen.SetPositions(points);
    }

    int timeToArrayPos(float time, int iteration, int channels, float totalAudioLength)
    {
        if (time < 0)
            return 0;
        else if (time >= totalAudioLength)
            return currentSample.data.Length - 1;

        // Need to floor it so it lines up with the first channel
        int arrayPoint = (int)((time / totalAudioLength * currentSample.data.Length) / (channels * iteration)) * channels * iteration;

        if (arrayPoint >= currentSample.data.Length)
            arrayPoint = currentSample.data.Length - 1;

        return arrayPoint;
    }
}
