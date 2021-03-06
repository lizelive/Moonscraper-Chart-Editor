﻿// Copyright (c) 2016-2017 Alexander Ong
// See LICENSE in project root for license information.

using UnityEngine;
using System.Collections.Generic;
//using System.Windows.Forms;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.IO;

public class ClipboardObjectController : Snapable {
    static string CLIPBOARD_FILE_LOCATION;

    public GroupSelect groupSelectTool;
    public Transform strikeline;
    public static Clipboard clipboard = new Clipboard();
    [SerializeField]
    ToolPanelController viewModeController;
    Renderer ren;

    uint pastePos = 0;

    protected override void Awake()
    {
        base.Awake();
        ren = GetComponent<Renderer>();
        EventsManager.onApplicationModeChangedEventList.Add(OnApplicationModeChanged);
        CLIPBOARD_FILE_LOCATION = UnityEngine.Application.persistentDataPath + "/MoonscraperClipboard.bin";
    }

    new void LateUpdate()
    {
        if (Mouse.world2DPosition != null && Input.mousePosition.y < Camera.main.WorldToScreenPoint(editor.mouseYMaxLimit.position).y)
        {
            pastePos = objectSnappedChartPos;
        }
        else
        {
            pastePos = editor.currentSong.WorldPositionToSnappedTick(strikeline.position.y, GameSettings.step);
        }

        transform.position = new Vector3(transform.position.x, editor.currentSong.TickToWorldYPosition(pastePos), transform.position.z);

        if (ShortcutInput.GetInputDown(Shortcut.ClipboardPaste))
        {
            Paste(pastePos);
            groupSelectTool.reset();
        }
    }

    void OnApplicationModeChanged(Globals.ApplicationMode applicationMode)
    {
        // Can only paste in editor mode
        gameObject.SetActive(applicationMode == Globals.ApplicationMode.Editor);
    }

    public static void SetData(SongObject[] data, Clipboard.SelectionArea area, Song song)
    {
        clipboard = new Clipboard();
        clipboard.data = data;
        clipboard.resolution = song.resolution;
        clipboard.instrument = MenuBar.currentInstrument;
        clipboard.SetCollisionArea(area, song);
        //System.Windows.Forms.Clipboard.SetDataObject("", false);   // Clear the clipboard to mimic the real clipboard. For some reason putting custom objects on the clipboard with this dll doesn't work.

        try
        {
            FileStream fs = null;
            
            try
            {
                fs = new FileStream(CLIPBOARD_FILE_LOCATION, FileMode.Create, FileAccess.ReadWrite);
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(fs, clipboard);
            }
            catch (SerializationException e)
            {
                Logger.LogException(e, "Failed to serialize");
            }
            catch (System.Exception e)
            {
                Logger.LogException(e, "Failed to serialize in general");
            }
            finally
            {
                if (fs != null)
                    fs.Close();
                else
                    Debug.LogError("Filestream when writing clipboard data failed to initialise");
            }
        }
        catch (System.Exception e)
        {
            Logger.LogException(e, "Failed to copy data");
        }
    }

    // Paste the clipboard data into the chart, overwriting anything else in the process
    public void Paste(uint chartLocationToPaste)
    {
        //if (System.Windows.Forms.Clipboard.GetDataObject().GetFormats().Length > 0 && 
        //    !(
        //        System.Windows.Forms.Clipboard.ContainsText(TextDataFormat.UnicodeText) && 
        //        System.Windows.Forms.Clipboard.ContainsText(TextDataFormat.Text) && 
        //        System.Windows.Forms.Clipboard.GetText() == "")
        //    )     // Something else is pasted on the clipboard instead of Moonscraper stuff.
        //    return;

        FileStream fs = null;
        clipboard = null;
        try
        {
            // Read clipboard data from a file instead of the actual clipboard because the actual clipboard doesn't work for whatever reason
            fs = new FileStream(CLIPBOARD_FILE_LOCATION, FileMode.Open);
            BinaryFormatter formatter = new BinaryFormatter();

            clipboard = (Clipboard)formatter.Deserialize(fs);
        }
        catch (System.Exception e)
        {
            Logger.LogException(e, "Failed to read from clipboard file");
            clipboard = null;
        }
        finally
        {
            if (fs != null)
                fs.Close();
            else
                Debug.LogError("Filestream when reading clipboard data failed to initialise");
        }

        if (Globals.applicationMode == Globals.ApplicationMode.Editor && clipboard != null && clipboard.data.Length > 0)
        {
            List<ActionHistory.Action> record = new List<ActionHistory.Action>();
            Rect collisionRect = clipboard.GetCollisionRect(chartLocationToPaste, editor.currentSong);
            if (clipboard.areaChartPosMin > clipboard.areaChartPosMax)
            {
                Debug.LogError("Clipboard minimum (" + clipboard.areaChartPosMin + ") is greater than clipboard the max (" + clipboard.areaChartPosMax + ")");
            }
            uint colliderChartDistance = TickFunctions.TickScaling(clipboard.areaChartPosMax - clipboard.areaChartPosMin, clipboard.resolution, editor.currentSong.resolution);

            viewModeController.ToggleSongViewMode(!clipboard.data[0].GetType().IsSubclassOf(typeof(ChartObject)));

            // Overwrite any objects in the clipboard space
            if (clipboard.data[0].GetType().IsSubclassOf(typeof(ChartObject)))
            {
                foreach (ChartObject chartObject in editor.currentChart.chartObjects)
                {
                    if (chartObject.tick >= chartLocationToPaste && chartObject.tick <= (chartLocationToPaste + colliderChartDistance) && PrefabGlobals.HorizontalCollisionCheck(PrefabGlobals.GetCollisionRect(chartObject), collisionRect))
                    {
                        chartObject.Delete(false);

                        record.Add(new ActionHistory.Delete(chartObject));
                    }
                }
            }
            else
            {
                // Overwrite synctrack, leave sections alone
                foreach (SyncTrack syncObject in editor.currentSong.syncTrack)
                {
                    if (syncObject.tick >= chartLocationToPaste && syncObject.tick <= (chartLocationToPaste + colliderChartDistance) && PrefabGlobals.HorizontalCollisionCheck(PrefabGlobals.GetCollisionRect(syncObject), collisionRect))
                    {
                        syncObject.Delete(false);

                        record.Add(new ActionHistory.Delete(syncObject));
                    }
                }
            }

            editor.currentChart.UpdateCache();
            editor.currentSong.UpdateCache();

            uint maxLength = editor.currentSong.TimeToTick(editor.currentSong.length, editor.currentSong.resolution);

            // Paste the new objects in
            foreach (SongObject clipboardSongObject in clipboard.data)
            {
                SongObject objectToAdd = clipboardSongObject.Clone();

                objectToAdd.tick = chartLocationToPaste +
                    TickFunctions.TickScaling(clipboardSongObject.tick, clipboard.resolution, editor.currentSong.resolution) -
                    TickFunctions.TickScaling(clipboard.areaChartPosMin, clipboard.resolution, editor.currentSong.resolution);

                if (objectToAdd.tick >= maxLength)
                    break;

                if (objectToAdd.GetType() == typeof(Note))
                {
                    Note note = (Note)objectToAdd;

                    if (clipboard.instrument == Song.Instrument.GHLiveGuitar || clipboard.instrument == Song.Instrument.GHLiveBass)
                    {
                        // Pasting from a ghl track
                        if (!Globals.ghLiveMode)
                        {
                            if (note.ghliveGuitarFret == Note.GHLiveGuitarFret.Open)
                                note.guitarFret = Note.GuitarFret.Open;
                            else if (note.ghliveGuitarFret == Note.GHLiveGuitarFret.White3)
                                continue;
                        }
                    }
                    else if (Globals.ghLiveMode)
                    {
                        // Pasting onto a ghl track
                        if (note.guitarFret == Note.GuitarFret.Open)
                            note.ghliveGuitarFret = Note.GHLiveGuitarFret.Open;
                    }

                    note.length = TickFunctions.TickScaling(note.length, clipboard.resolution, editor.currentSong.resolution);

                    record.AddRange(PlaceNote.AddObjectToCurrentChart(note, editor, false));
                }
                else if (objectToAdd.GetType() == typeof(Starpower))
                {
                    Starpower sp = (Starpower)objectToAdd;
                    sp.length = TickFunctions.TickScaling(sp.length, clipboard.resolution, editor.currentSong.resolution);

                    record.AddRange(PlaceStarpower.AddObjectToCurrentChart(sp, editor, false));
                }
                else
                {
                    PlaceSongObject.AddObjectToCurrentEditor(objectToAdd, editor, false);

                    record.Add(new ActionHistory.Add(objectToAdd));
                }               
            }
            editor.currentChart.UpdateCache();
            editor.currentSong.UpdateCache();
            editor.actionHistory.Insert(record.ToArray());
            editor.actionHistory.Insert(editor.FixUpBPMAnchors().ToArray());
        }
        // 0 objects in clipboard, don't bother pasting
    }
}
