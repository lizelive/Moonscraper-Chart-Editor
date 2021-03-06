﻿// Copyright (c) 2016-2017 Alexander Ong
// See LICENSE in project root for license information.

using UnityEngine;
using System.Collections;
using System;

public class StarpowerController : SongObjectController
{
    public GameObject tail;
    public Starpower starpower { get { return (Starpower)songObject; } set { Init(value, this); } }
    public const float position = -3.0f;
    
    Starpower unmodifiedSP = null;

    new void Awake()
    {
        base.Awake();
    }

    protected override void UpdateCheck()
    {
        if (starpower != null)
        {
            uint endPosition = starpower.tick + starpower.length;

            if ((starpower.tick >= editor.minPos && starpower.tick < editor.maxPos) ||
                    (endPosition > editor.minPos && endPosition < editor.maxPos) ||
                    (starpower.tick < editor.minPos && endPosition >= editor.maxPos))
            {
                if (Globals.applicationMode == Globals.ApplicationMode.Editor)
                    UpdateSongObject();
            }
            else
                gameObject.SetActive(false);
        }
        else
            gameObject.SetActive(false);
    }

    public override void UpdateSongObject()
    {
        if (starpower.song != null)
        {
            transform.position = new Vector3(CHART_CENTER_POS + position, starpower.worldYPosition, 0);

            UpdateTailLength();
        }
    }

    public void UpdateTailLength()
    {
        float length = starpower.song.TickToWorldYPosition(starpower.tick + starpower.length) - starpower.worldYPosition;

        Vector3 scale = tail.transform.localScale;
        scale.y = length;
        tail.transform.localScale = scale;

        Vector3 position = transform.position;
        position.y += length / 2.0f;
        tail.transform.position = position;
    }

    void TailDrag()
    {
        ChartEditor.isDirty = true;
        uint snappedChartPos;

        if (Mouse.world2DPosition != null && ((Vector2)Mouse.world2DPosition).y < editor.mouseYMaxLimit.position.y)
        {
            snappedChartPos = Snapable.TickToSnappedTick(starpower.song.WorldYPositionToTick(((Vector2)Mouse.world2DPosition).y), GameSettings.step, starpower.song.resolution);           
        }
        else
        {
            snappedChartPos = Snapable.TickToSnappedTick(starpower.song.WorldYPositionToTick(editor.mouseYMaxLimit.position.y), GameSettings.step, starpower.song.resolution);
        }

        uint max = starpower.length;

        starpower.SetLengthByPos(snappedChartPos);

        if (starpower.length > max)
            max = starpower.length;

        int start, length;
        var notes = starpower.chart.notes;
        SongObjectHelper.GetRange(notes, starpower.tick, starpower.tick + max, out start, out length);

        for (int i = start; i < start + length; ++i)
        {
            if (notes[i].controller)
                notes[i].controller.SetDirty();
        }
    }
    
    public override void OnSelectableMouseDrag()
    {
        // Move note
        if (!dragCheck())
            base.OnSelectableMouseDrag();
    }

    public bool dragCheck()
    {
        if (Globals.applicationMode == Globals.ApplicationMode.Editor && Input.GetMouseButton(1))
        {
            if (unmodifiedSP == null)
                unmodifiedSP = (Starpower)starpower.Clone();

            TailDrag();
            return true;
        }

        return false;
    }

    public override void OnSelectableMouseUp()
    {
        if (unmodifiedSP != null && unmodifiedSP.length != starpower.length)
        {
            editor.actionHistory.Insert(new ActionHistory.Modify(unmodifiedSP, starpower));
        }

        unmodifiedSP = null;
    }
}
