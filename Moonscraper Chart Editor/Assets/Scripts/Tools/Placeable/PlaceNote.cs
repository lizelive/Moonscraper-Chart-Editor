﻿// Copyright (c) 2016-2017 Alexander Ong
// See LICENSE in project root for license information.

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

[RequireComponent(typeof(NoteController))]
public class PlaceNote : PlaceSongObject {
    public Note note { get { return (Note)songObject; } set { songObject = value; } }
    new public NoteController controller { get { return (NoteController)base.controller; } set { base.controller = value; } }

    [HideInInspector]
    public NoteVisualsManager visuals;

    [HideInInspector]
    public float horizontalMouseOffset = 0;

    public static bool addNoteCheck
    {
        get
        {
            return (Toolpane.currentTool == Toolpane.Tools.Note && Globals.applicationMode == Globals.ApplicationMode.Editor && Input.GetMouseButton(0));
        }
    }

    protected override void SetSongObjectAndController()
    {
        visuals = GetComponentInChildren<NoteVisualsManager>();
        note = new Note(0, Note.GuitarFret.Green);

        controller = GetComponent<NoteController>();
        controller.note = note;
        note.controller = controller;
    }

    protected override void Controls()
    {
        if (addNoteCheck)   // Now handled by the PlaceNoteController
        {
            //AddObject();
        }
    }

    protected override void OnEnable()
    {
        editor.currentSelectedObject = note;
        
        Update();
    }

    void OnDisable()
    {
        note.previous = null;
        note.next = null;
    }

    public void ExplicitUpdate()
    {
        Update();
    }

    // Update is called once per frame
    protected override void Update () {
        note.chart = editor.currentChart;
        base.Update();

        // Get previous and next note
        int pos = SongObjectHelper.FindClosestPosition(note.tick, editor.currentChart.notes);
        //Debug.Log(pos);
        if (pos == SongObjectHelper.NOTFOUND)
        {
            note.previous = null;
            note.next = null;
        }
        else
        {
            if (note.IsOpenNote())
                UpdateOpenPrevAndNext(pos);
            else
                UpdatePrevAndNext(pos);
        }

        UpdateFretType();
    }

    void UpdatePrevAndNext(int closestNoteArrayPos)
    {
        if (editor.currentChart.notes[closestNoteArrayPos] < note)
        {
            note.previous = editor.currentChart.notes[closestNoteArrayPos];
            note.next = editor.currentChart.notes[closestNoteArrayPos].next;
        }
        else if (editor.currentChart.notes[closestNoteArrayPos] > note)
        {
            note.next = editor.currentChart.notes[closestNoteArrayPos];
            note.previous = editor.currentChart.notes[closestNoteArrayPos].previous;
        }
        else
        {
            // Found own note
            note.previous = editor.currentChart.notes[closestNoteArrayPos].previous;
            note.next = editor.currentChart.notes[closestNoteArrayPos].next;
        }
    }

    void UpdateOpenPrevAndNext(int closestNoteArrayPos)
    {
        if (editor.currentChart.notes[closestNoteArrayPos] < note)
        {
            Note previous = GetPreviousOfOpen(note.tick, editor.currentChart.notes[closestNoteArrayPos]);

            note.previous = previous;
            if (previous != null)
                note.next = GetNextOfOpen(note.tick, previous.next);
            else
                note.next = GetNextOfOpen(note.tick, editor.currentChart.notes[closestNoteArrayPos]);
        }
        else if (editor.currentChart.notes[closestNoteArrayPos] > note)
        {
            Note next = GetNextOfOpen(note.tick, editor.currentChart.notes[closestNoteArrayPos]);

            note.next = next;
            note.previous = GetPreviousOfOpen(note.tick, next.previous);
        }
        else
        {
            // Found own note
            note.previous = editor.currentChart.notes[closestNoteArrayPos].previous;
            note.next = editor.currentChart.notes[closestNoteArrayPos].next;
        }
    }

    Note GetPreviousOfOpen(uint openNotePos, Note previousNote)
    {
        if (previousNote == null || previousNote.tick != openNotePos || (!previousNote.isChord && previousNote.tick != openNotePos))
            return previousNote;
        else
            return GetPreviousOfOpen(openNotePos, previousNote.previous);
    }

    Note GetNextOfOpen(uint openNotePos, Note nextNote)
    {
        if (nextNote == null || nextNote.tick != openNotePos || (!nextNote.isChord && nextNote.tick != openNotePos))
            return nextNote;
        else
            return GetNextOfOpen(openNotePos, nextNote.next);
    }

    protected virtual void UpdateFretType()
    {
        if (!note.IsOpenNote() && Mouse.world2DPosition != null)
        {
            Vector2 mousePosition = (Vector2)Mouse.world2DPosition;
            mousePosition.x += horizontalMouseOffset;
            note.rawNote = XPosToNoteNumber(mousePosition.x, editor.laneInfo);
        }
    }

    public static int XPosToNoteNumber(float xPos, LaneInfo laneInfo)
    {
        if (GameSettings.notePlacementMode == GameSettings.NotePlacementMode.LeftyFlip)
            xPos *= -1;

        float startPos = LaneInfo.positionRangeMin;
        float endPos = LaneInfo.positionRangeMax;

        int max = laneInfo.laneCount - 1;
        float factor = (endPos - startPos) / (max);

        for (int i = 0; i < max; ++i)
        {
            float currentPosCheck = startPos + i * factor + factor / 2.0f;
            if (xPos < currentPosCheck)
                return i;
        }

        return max;
    }

    public ActionHistory.Action[] AddNoteWithRecord()
    {
        return AddObjectToCurrentChart(note, editor);
    }

    protected override void AddObject()
    {
        AddObjectToCurrentChart(note, editor);
    }

    public static ActionHistory.Action[] AddObjectToCurrentChart(Note note, ChartEditor editor, bool update = true, bool copy = true)
    {
        Note throwaway;
        return AddObjectToCurrentChart(note, editor, out throwaway, update, copy);
    }

    public static ActionHistory.Action[] AddObjectToCurrentChart(Note note, ChartEditor editor, out Note addedNote, bool update = true, bool copy = true)
    {
        List<ActionHistory.Action> noteRecord = new List<ActionHistory.Action>();

        int index, length;
        SongObjectHelper.GetRange(editor.currentChart.notes, note.tick, note.tick, out index, out length);
        
        // Account for when adding an exact note as what's already in   
        if (length > 0)
        {
            bool cancelAdd = false;
            for (int i = index; i < index + length; ++i)
            {
                Note overwriteNote = editor.currentChart.notes[i];
                
                if (note.AllValuesCompare(overwriteNote))
                {
                    cancelAdd = true;
                    break;
                }
                if ((((note.IsOpenNote() || overwriteNote.IsOpenNote()) && !Globals.drumMode) || note.guitarFret == overwriteNote.guitarFret) && !note.AllValuesCompare(overwriteNote))
                {
                    noteRecord.Add(new ActionHistory.Delete(overwriteNote));
                }
            }
            if (!cancelAdd)
                noteRecord.Add(new ActionHistory.Add(note));
        }
        else
            noteRecord.Add(new ActionHistory.Add(note));
                
        Note noteToAdd;
        if (copy)
            noteToAdd = new Note(note);
        else
            noteToAdd = note;

        if (noteToAdd.IsOpenNote())
            noteToAdd.flags &= ~Note.Flags.Tap;

        editor.currentChart.Add(noteToAdd, update);
        if (noteToAdd.cannotBeForced)
            noteToAdd.flags &= ~Note.Flags.Forced;

        noteToAdd.ApplyFlagsToChord();

        //NoteController nCon = editor.CreateNoteObject(noteToAdd);
        standardOverwriteOpen(noteToAdd);

        noteRecord.InsertRange(0, CapNoteCheck(noteToAdd));
        noteRecord.InsertRange(0, ForwardCap(noteToAdd));     // Do this due to pasting from the clipboard

        // Check if the automatic un-force will kick in
        ActionHistory.Action forceCheck = AutoForcedCheck(noteToAdd);

        addedNote = noteToAdd;

        if (forceCheck != null)
            noteRecord.Insert(0, forceCheck);           // Insert at the start so that the modification happens at the end of the undo function, otherwise the natural force check prevents it from being forced

        foreach (Note chordNote in addedNote.chord)
        {
            if (chordNote.controller)
                chordNote.controller.SetDirty();
        }

        Note next = addedNote.nextSeperateNote;
        if (next != null)
        {
            foreach (Note chordNote in next.chord)
            {
                if (chordNote.controller)
                    chordNote.controller.SetDirty();
            }
        }

        return noteRecord.ToArray();
    }

    protected static void standardOverwriteOpen(Note note)
    {
        if (!note.IsOpenNote() && MenuBar.currentInstrument != Song.Instrument.Drums)
        {
            int index, length;
            SongObjectHelper.FindObjectsAtPosition(note.tick, note.chart.notes, out index, out length);

            // Check for open notes and delete
            for (int i = index; i < index + length; ++i)
            //foreach (Note chordNote in chordNotes)
            {
                Note chordNote = note.chart.notes[i];
                if (chordNote.IsOpenNote())
                {
                    chordNote.Delete();
                }
            }
        }
    }

    protected static ActionHistory.Action AutoForcedCheck(Note note)
    {
        Note next = note.nextSeperateNote;
        if (next != null && (next.flags & Note.Flags.Forced) == Note.Flags.Forced && next.cannotBeForced)
        {           
            Note originalNext = (Note)next.Clone();
            next.flags &= ~Note.Flags.Forced;
            next.ApplyFlagsToChord();

            return new ActionHistory.Modify(originalNext, next);
        }
        else
            return null;
    }

    protected static ActionHistory.Action[] ForwardCap(Note note)
    {
        List<ActionHistory.Action> actionRecord = new List<ActionHistory.Action>();
        Note next;
        next = note.nextSeperateNote;      
        
        if (!GameSettings.extendedSustainsEnabled)
        {
            // Get chord  
            next = note.nextSeperateNote;

            if (next != null)
            {
                foreach (Note noteToCap in note.chord)
                {
                    ActionHistory.Action action = noteToCap.CapSustain(next);
                    if (action != null)
                        actionRecord.Add(action);
                }
            }
        }
        else
        {
            // Find the next note of the same fret type or open
            next = note.next;
            while (next != null && next.guitarFret != note.guitarFret && !next.IsOpenNote())
                next = next.next;

            // If it's an open note it won't be capped

            if (next != null)
            {
                ActionHistory.Action action = note.CapSustain(next);
                if (action != null)
                    actionRecord.Add(action);
            }
        }


        return actionRecord.ToArray();
    }

    protected static ActionHistory.Action[] CapNoteCheck(Note noteToAdd)
    {
        List<ActionHistory.Action> actionRecord = new List<ActionHistory.Action>();

        Note[] previousNotes = NoteFunctions.GetPreviousOfSustains(noteToAdd);
        if (!GameSettings.extendedSustainsEnabled)
        {
            // Cap all the notes
            foreach (Note prevNote in previousNotes)
            {
                if (prevNote.controller != null)
                {
                    ActionHistory.Action action = prevNote.CapSustain(noteToAdd);
                    if (action != null)
                        actionRecord.Add(action);
                }
            }

            foreach(Note chordNote in noteToAdd.chord)
            {
                if (chordNote.controller != null)
                    chordNote.controller.note.length = noteToAdd.length; 
            }
        }
        else
        {
            // Cap only the sustain of the same fret type and open notes
            foreach (Note prevNote in previousNotes)
            {
                if (prevNote.controller != null && (noteToAdd.IsOpenNote() || prevNote.guitarFret == noteToAdd.guitarFret))
                {
                    ActionHistory.Action action = prevNote.CapSustain(noteToAdd);
                    if (action != null)
                        actionRecord.Add(action);
                }
            }
        }

        return actionRecord.ToArray();
    }
}
