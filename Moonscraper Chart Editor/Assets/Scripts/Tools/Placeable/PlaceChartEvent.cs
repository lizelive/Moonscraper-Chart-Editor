﻿// Copyright (c) 2016-2017 Alexander Ong
// See LICENSE in project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlaceChartEvent : PlaceSongObject
{
    public ChartEvent chartEvent { get { return (ChartEvent)songObject; } set { songObject = value; } }
    new public ChartEventController controller { get { return (ChartEventController)base.controller; } set { base.controller = value; } }

    protected override void SetSongObjectAndController()
    {
        chartEvent = new ChartEvent(0, "Default");

        controller = GetComponent<ChartEventController>();
        controller.chartEvent = chartEvent;
    }

    protected override void Update()
    {
        base.Update();
        chartEvent.chart = editor.currentChart;
    }

    protected new void LateUpdate()
    {
        // Re-do the controller's position setting
        base.LateUpdate();

        var events = editor.currentChart.events;

        int offset = 0;
        int index, length;
        SongObjectHelper.GetRange(events, chartEvent.tick, chartEvent.tick, out index, out length);

        // Determine the offset for the object
        for (int i = index; i < index + length; ++i)
        {
            if (events[i].GetType() != chartEvent.GetType())
                continue;

            offset += ChartEventController.OFFSET_SPACING;
        }

        transform.position = new Vector3(SongObjectController.CHART_CENTER_POS + ChartEventController.position + offset, chartEvent.worldYPosition, 0);
    }

    protected override void AddObject()
    {
        ActionHistory.Add action;
        string debugMessage = string.Empty;
       
        // Add chart event
        ChartEvent chartEvent = new ChartEvent(this.chartEvent);
        editor.currentChart.Add(chartEvent);

        action = new ActionHistory.Add(chartEvent);

        debugMessage = "Added Chart Event \"";

        debugMessage += chartEvent.eventName + "\"";

        Debug.Log(debugMessage);

        editor.actionHistory.Insert(action);
        editor.currentSelectedObject = chartEvent;
    }

    public static void AddObjectToCurrentChart(ChartEvent chartEvent, ChartEditor editor, bool update = true)
    {
        ChartEvent eventToAdd = new ChartEvent(chartEvent);

        editor.currentChart.Add(eventToAdd, update);
        editor.currentSelectedObject = eventToAdd;
    }

    protected override void Controls()
    {
        if (!GameSettings.keysModeEnabled)
        {
            if (Toolpane.currentTool == Toolpane.Tools.SongEvent && Globals.applicationMode == Globals.ApplicationMode.Editor && Input.GetMouseButtonDown(0))
            {
                int pos = SongObjectHelper.FindObjectPosition(chartEvent, editor.currentChart.events);
                if (pos == SongObjectHelper.NOTFOUND)
                {
                    //RecordAddActionHistory(chartEvent, editor.currentChart.events);

                    AddObject();
                }
                // Link to the event already in
                else
                    editor.currentSelectedObject = editor.currentChart.events[pos];
            }
        }
        else if (ShortcutInput.GetInputDown(Shortcut.AddSongObject))
        {
            var searchArray = editor.currentChart.events;
            int pos = SongObjectHelper.FindObjectPosition(chartEvent, searchArray);
            if (pos == SongObjectHelper.NOTFOUND)
            {
                editor.actionHistory.Insert(new ActionHistory.Add(chartEvent));
                AddObject();
            }
            else
            {
                editor.actionHistory.Insert(new ActionHistory.Delete(searchArray[pos]));
                searchArray[pos].Delete();
                editor.currentSelectedObject = null;
            }
        }
    }
}
