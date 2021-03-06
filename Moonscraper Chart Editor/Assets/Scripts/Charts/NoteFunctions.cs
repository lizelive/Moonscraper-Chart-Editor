﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class NoteFunctions {

    public static void GroupAddFlags(IList<Note> notes, Note.Flags flag, int index, int length)
    {
        for (int i = index; i < index + length; ++i)
        {
            notes[i].flags = notes[i].flags | flag;
        }
    }

    /// <summary>
    /// Gets all the notes (including this one) that share the same tick position as this one.
    /// </summary>
    /// <returns>Returns an array of all the notes currently sharing the same tick position as this note.</returns>
    public static Note[] GetChord(this Note note)
    {
        List<Note> chord = new List<Note>();
        chord.Add(note);
    
        Note previous = note.previous;
        while (previous != null && previous.tick == note.tick)
        {
            chord.Add(previous);
            previous = previous.previous;
        }
    
        Note next = note.next;
        while (next != null && next.tick == note.tick)
        {
            chord.Add(next);
            next = next.next;
        }
    
        return chord.ToArray();
    }

    public static void ApplyFlagsToChord(this Note note)
    {
        foreach (Note chordNote in note.chord)
        {
            chordNote.flags = note.flags;
        }
    }

    public static void GetPreviousOfSustains(List<Note> list, Note startNote)
    {
        list.Clear();

        Note previous = startNote.previous;

        int allVisited = startNote.gameMode == Chart.GameMode.GHLGuitar ? 63 : 31; // 0011 1111 for ghlive, 0001 1111 for standard
        int noteTypeVisited = 0;

        while (previous != null && noteTypeVisited < allVisited)
        {
            if (previous.IsOpenNote())
            {
                if (GameSettings.extendedSustainsEnabled)
                {
                    list.Add(previous);
                    return;
                }

                else if (list.Count > 0)
                    return;
                else
                {
                    list.Clear();
                    list.Add(previous);
                }
            }
            else if (previous.tick < startNote.tick)
            {
                if ((noteTypeVisited & (1 << previous.rawNote)) == 0)
                {
                    list.Add(previous);
                    noteTypeVisited |= 1 << previous.rawNote;
                }
            }

            previous = previous.previous;
        }
    }

    public static Note[] GetPreviousOfSustains(Note startNote)
    {
        List<Note> list = new List<Note>(6);

        GetPreviousOfSustains(list, startNote);

        return list.ToArray();
    }

    public static ActionHistory.Modify CapSustain(this Note note, Note cap)
    {
        if (cap == null)
            return null;

        Note originalNote = (Note)note.Clone();

        // Cap sustain length
        if (cap.tick <= note.tick)
            note.length = 0;
        else if (note.tick + note.length > cap.tick)        // Sustain extends beyond cap note 
        {
            note.length = cap.tick - note.tick;
        }

        uint gapDis = (uint)(note.song.resolution * 4.0f / GameSettings.sustainGap);

        if (GameSettings.sustainGapEnabled && note.length > 0 && (note.tick + note.length > cap.tick - gapDis))
        {
            if ((int)(cap.tick - gapDis - note.tick) > 0)
                note.length = cap.tick - gapDis - note.tick;
            else
                note.length = 0;
        }

        if (originalNote.length != note.length)
            return new ActionHistory.Modify(originalNote, note);
        else
            return null;
    }

    public static Note FindNextSameFretWithinSustainExtendedCheck(this Note note)
    {
        Note next = note.next;

        while (next != null)
        {
            if (!GameSettings.extendedSustainsEnabled)
            {
                if ((next.IsOpenNote() || (note.tick < next.tick)) && note.tick != next.tick)
                    return next;
            }
            else
            {
                if ((!note.IsOpenNote() && next.IsOpenNote() && !(note.gameMode == Chart.GameMode.Drums)) || (next.rawNote == note.rawNote))
                    return next;
            }

            next = next.next;
        }

        return null;
    }

    public static bool IsOpenNote(this Note note)
    {
        if (note.gameMode == Chart.GameMode.GHLGuitar)
            return note.ghliveGuitarFret == Note.GHLiveGuitarFret.Open;
        else
            return note.guitarFret == Note.GuitarFret.Open;
    }

    /// <summary>
    /// Calculates and sets the sustain length based the tick position it should end at. Will be a length of 0 if the note position is greater than the specified position.
    /// </summary>
    /// <param name="pos">The end-point for the sustain.</param>
    public static void SetSustainByPos(this Note note, uint pos)
    {
        if (pos > note.tick)
            note.length = pos - note.tick;
        else
            note.length = 0;

        // Cap the sustain
        Note nextFret;
        nextFret = note.FindNextSameFretWithinSustainExtendedCheck();

        if (nextFret != null)
        {
            note.CapSustain(nextFret);
        }
    }

    public static void SetType(this Note note, Note.NoteType type)
    {
        note.flags = Note.Flags.None;
        switch (type)
        {
            case (Note.NoteType.Strum):
                if (note.isChord)
                    note.flags &= ~Note.Flags.Forced;
                else
                {
                    if (note.isNaturalHopo)
                        note.flags |= Note.Flags.Forced;
                    else
                        note.flags &= ~Note.Flags.Forced;
                }

                break;

            case (Note.NoteType.Hopo):
                if (!note.cannotBeForced)
                {
                    if (note.isChord)
                        note.flags |= Note.Flags.Forced;
                    else
                    {
                        if (!note.isNaturalHopo)
                            note.flags |= Note.Flags.Forced;
                        else
                            note.flags &= ~Note.Flags.Forced;
                    }
                }
                break;

            case (Note.NoteType.Tap):
                if (!note.IsOpenNote())
                    note.flags |= Note.Flags.Tap;
                break;

            default:
                break;
        }

        note.ApplyFlagsToChord();
    }

    public static int ExpensiveGetExtendedSustainMask(this Note note)
    {
        int mask = 0;

        if (note.length > 0 && note.chart != null)
        {
            int index, length;
            var notes = note.chart.notes;
            SongObjectHelper.GetRange(notes, note.tick, note.tick + note.length - 1, out index, out length);

            for (int i = index; i < index + length; ++i)
            {
                mask |= notes[i].mask;
            }
        }

        return mask;
    }

    public static Note.GuitarFret SaveGuitarNoteToDrumNote(Note.GuitarFret fret_type)
    {
        if (fret_type == Note.GuitarFret.Open)
            return Note.GuitarFret.Green;
        else if (fret_type == Note.GuitarFret.Orange)
            return Note.GuitarFret.Open;
        else
            return fret_type + 1;
    }

    public static Note.GuitarFret LoadDrumNoteToGuitarNote(Note.GuitarFret fret_type)
    {
        if (fret_type == Note.GuitarFret.Open)
            return Note.GuitarFret.Orange;
        else if (fret_type == Note.GuitarFret.Green)
            return Note.GuitarFret.Open;
        else
            return fret_type - 1;
    }
}
