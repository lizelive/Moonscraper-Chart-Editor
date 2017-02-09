﻿#define TIMING_DEBUG
//#undef UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System;

public class ChartEditor : MonoBehaviour { 
    public static bool editOccurred = false;
    const int POOL_SIZE = 100;
    public const int MUSIC_STREAM_ARRAY_POS = 0;
    public const int GUITAR_STREAM_ARRAY_POS = 1;
    public const int RHYTHM_STREAM_ARRAY_POS = 2;

    [Header("Prefabs")]
    public GameObject notePrefab;
    public GameObject starpowerPrefab;
    public GameObject sectionPrefab;
    public GameObject bpmPrefab;
    public GameObject tsPrefab;
    [Header("Inspectors")]
    public NotePropertiesPanelController noteInspector;
    public SectionPropertiesPanelController sectionInspector;
    public BPMPropertiesPanelController bpmInspector;
    public TimesignaturePropertiesPanelController tsInspector;
    public GameObject groupSelectInspector;
    [Header("Tool prefabs")]
    public GameObject ghostNote;
    public GameObject ghostStarpower;
    public GameObject ghostSection;
    public GameObject ghostBPM;
    public GameObject ghostTimeSignature;
    public GroupMove groupMove;
    [Header("Misc.")]
    public UnityEngine.UI.Button play;
    public UnityEngine.UI.Button undo;
    public UnityEngine.UI.Button redo;
    public Transform strikelineAudio;
    public Transform visibleStrikeline;
    public TimelineHandler timeHandler;
    public Transform camYMin;
    public Transform camYMax;
    public Transform autoUpScroll;
    public Transform mouseYMaxLimit;
    public Transform mouseYMinLimit;
    public SongPropertiesPanelController songPropertiesCon;
    public AudioSource clapSource;
    public UnityEngine.Audio.AudioMixerGroup mixer;

    public uint minPos { get; private set; }
    public uint maxPos { get; private set; }
    [HideInInspector]
    public AudioSource[] musicSources;

    public Song currentSong { get; private set; }
    public Chart currentChart { get; private set; }
    string currentFileName = string.Empty;

    MovementController movement;
    SongObjectPoolManager _songObjectPoolManager;
    public SongObjectPoolManager songObjectPoolManager { get { return _songObjectPoolManager; } }

    string lastLoadedFile = string.Empty;

    GameObject songObjectParent;
    GameObject chartObjectParent;

    OpenFileName saveFileDialog;

    public ActionHistory actionHistory;

    public SongObject currentSelectedObject
    {
        get
        {
            if (currentSelectedObjects.Length == 1)
                return currentSelectedObjects[0];
            else
                return null;
        }
        set
        {
            if (value == null)
                currentSelectedObjects = new SongObject[0];
            else
                currentSelectedObjects = new SongObject[] { value };
        }
    }
    public SongObject[] currentSelectedObjects = new SongObject[0];

    GameObject currentPropertiesPanel = null;
    Vector3? stopResetPos = null;

    [DllImport("user32.dll", EntryPoint = "SetWindowText")]
    public static extern bool SetWindowText(System.IntPtr hwnd, System.String lpString);
    [DllImport("user32.dll", EntryPoint = "FindWindow")]
    public static extern System.IntPtr FindWindow(System.String className, System.String windowName);
    [DllImport("user32.dll")]
    public static extern System.IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

#if !UNITY_EDITOR
    System.IntPtr windowPtr = IntPtr.Zero;
    string originalWindowName;
#endif

    void SetApplicationWindowPointer()
    {
#if !UNITY_EDITOR
        const int nChars = 256;
        System.Text.StringBuilder buffer = new System.Text.StringBuilder(nChars);
        windowPtr = GetForegroundWindow();
        GetWindowText(windowPtr, buffer, nChars);
        if (buffer.ToString() != GetComponent<Settings>().productName)
        {
            windowPtr = IntPtr.Zero;
            buffer.Length = 0;
        }
        else
            originalWindowName = buffer.ToString();
#endif
    }

    // Use this for initialization
    void Awake () {
        _songObjectPoolManager = GetComponent<SongObjectPoolManager>();

        minPos = 0;
        maxPos = 0;

        noteInspector.gameObject.SetActive(false);
        sectionInspector.gameObject.SetActive(false);
        bpmInspector.gameObject.SetActive(false);
        tsInspector.gameObject.SetActive(false);

        // Create grouping objects to make reading the inspector easier
        songObjectParent = new GameObject();
        songObjectParent.name = "Song Objects";
        songObjectParent.tag = "Song Object";

        chartObjectParent = new GameObject();
        chartObjectParent.name = "Chart Objects";
        chartObjectParent.tag = "Chart Object";

        // Create a default song
        currentSong = new Song();
        LoadSong(currentSong);

        musicSources = new AudioSource[3];
        for (int i = 0; i < musicSources.Length; ++i)
        {
            musicSources[i] = gameObject.AddComponent<AudioSource>();
            musicSources[i].volume = 0.5f;
            musicSources[i].outputAudioMixerGroup = mixer;
        }

        movement = GameObject.FindGameObjectWithTag("Movement").GetComponent<MovementController>();

        // Initialise object
        songPropertiesCon.gameObject.SetActive(true);
        songPropertiesCon.gameObject.SetActive(false);

        editOccurred = false;
    }

    Vector3 mousePos = Vector3.zero;
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            mousePos = Input.mousePosition;
        }

        if (Input.GetMouseButton(0) && mousePos != Input.mousePosition && currentSelectedObjects.Length > 0 && !Mouse.GetSelectableObjectUnderMouse())
        {
            groupMove.SetSongObjects(currentSelectedObjects, true);

            currentSelectedObject = null;
        }

        if (Input.GetMouseButtonUp(0) && !Mouse.GetSelectableObjectUnderMouse() && !Mouse.IsUIUnderPointer() && mousePos == Input.mousePosition)
        {
            currentSelectedObject = null;
            mousePos = Vector3.zero;
        }

        // Update object positions that supposed to be visible into the range of the camera
        minPos = currentSong.WorldYPositionToChartPosition(camYMin.position.y);
        maxPos = currentSong.WorldYPositionToChartPosition(camYMax.position.y);

        // Update song objects within range
#if false
        enableSongObjects(currentSong.events, SongObject.ID.Event, minPos, maxPos);
        enableSongObjects(currentSong.syncTrack, SongObject.ID.BPM, minPos, maxPos);

        enableSongObjects(currentChart.notes, SongObject.ID.Note, minPos, maxPos);
        enableSongObjects(currentChart.starPower, SongObject.ID.Starpower, minPos, maxPos);
        enableSongObjects(currentChart.events, SongObject.ID.ChartEvent, minPos, maxPos);
#endif
        //Debug.Log(currentSelectedObjects.Length);
        // Update the current properties panel
        if (currentSelectedObject != null)
        {
            GameObject previousPanel = currentPropertiesPanel;

            switch (currentSelectedObjects[0].classID)
            {
                case ((int)SongObject.ID.Note):
                    noteInspector.currentNote = (Note)currentSelectedObject;
                    currentPropertiesPanel = noteInspector.gameObject;
                    break;
                case ((int)SongObject.ID.Section):
                    sectionInspector.currentSection = (Section)currentSelectedObject;
                    currentPropertiesPanel = sectionInspector.gameObject;
                    break;
                case ((int)SongObject.ID.BPM):
                    bpmInspector.currentBPM = (BPM)currentSelectedObject;
                    currentPropertiesPanel = bpmInspector.gameObject;
                    break;
                case ((int)SongObject.ID.TimeSignature):
                    tsInspector.currentTS = (TimeSignature)currentSelectedObject;
                    currentPropertiesPanel = tsInspector.gameObject;
                    break;
                default:
                    currentPropertiesPanel = null;
                    currentSelectedObject = null;
                    break;
            }

            if (currentPropertiesPanel != previousPanel)
            {
                if (previousPanel)
                    previousPanel.SetActive(false);
            }

            if (currentPropertiesPanel != null)
            {
                currentPropertiesPanel.gameObject.SetActive(true);
            }
        }
        else if (currentPropertiesPanel)
        {
            currentPropertiesPanel.gameObject.SetActive(false);
        }

        if (Toolpane.currentTool == Toolpane.Tools.GroupSelect)
        {
            currentPropertiesPanel = groupSelectInspector;
            currentPropertiesPanel.gameObject.SetActive(true);
        }

        if (Globals.applicationMode == Globals.ApplicationMode.Editor)
        {
            undo.interactable = actionHistory.canUndo;
            redo.interactable = actionHistory.canRedo;
        }
        else
        {
            undo.interactable = false;
            redo.interactable = false;
        }

        // Set window text to represent if the current song has been saved or not
#if !UNITY_EDITOR
        if (windowPtr != IntPtr.Zero)
        {
            if (editOccurred)
                SetWindowText(windowPtr, originalWindowName + "*");
            else
                SetWindowText(windowPtr, originalWindowName);
        }
#endif
    }

    void OnApplicationFocus(bool hasFocus)
    {
#if !UNITY_EDITOR
        if (hasFocus && windowPtr == IntPtr.Zero)
            SetApplicationWindowPointer();
#endif
        if (hasFocus)
            Time.timeScale = 1;
        else
        {
            Time.timeScale = 0;
        }

        if (hasFocus && Globals.applicationMode == Globals.ApplicationMode.Playing)
            Play();
        else
        {
            foreach (AudioSource source in musicSources)
                source.Stop();
        }
    }
    static bool quitting = false;
    void OnApplicationQuit()
    {
        quitting = true;
        editCheck();

        while (currentSong.IsSaving);
    }

    void editCheck()
    {    
        // Check for unsaved changes
        if (editOccurred)
        {
            if (quitting)
                UnityEngine.Application.CancelQuit();
#if !UNITY_EDITOR
            
            DialogResult result = MessageBox.Show("Want to save unsaved changes?", "Warning", MessageBoxButtons.YesNo);

            if (result == DialogResult.Yes)
            {                    
                Save();
            }
#endif

            if (quitting)
                UnityEngine.Application.Quit();
        }
    }

    public void New()
    {
        editCheck();

        lastLoadedFile = string.Empty;
        currentSong = new Song();

        LoadSong(currentSong);

        movement.SetPosition(0);
        StartCoroutine(resetLag());

        currentSelectedObject = null;
        editOccurred = true;
    }

    IEnumerator resetLag()
    {
        yield return null;
        songPropertiesCon.gameObject.SetActive(true);
    }

    // Wrapper function
    public void Load()
    {
        Stop();
        StartCoroutine(_Load());
    }

    public void Save()
    {
        if (lastLoadedFile != string.Empty)
            Save(lastLoadedFile);
        else
            SaveAs();
    }

    public void SaveAs(bool forced = true)
    {
        try {
            string fileName;

#if UNITY_EDITOR
            fileName = UnityEditor.EditorUtility.SaveFilePanel("Save as...", "", currentSong.name, "chart");
#else

            OpenFileName openSaveFileDialog = new OpenFileName();

            openSaveFileDialog.structSize = Marshal.SizeOf(openSaveFileDialog);
            openSaveFileDialog.filter = "Chart files (*.chart)\0*.chart";
            openSaveFileDialog.file = new String(new char[256]);
            openSaveFileDialog.maxFile = openSaveFileDialog.file.Length;

            openSaveFileDialog.fileTitle = new String(new char[64]);
            openSaveFileDialog.maxFileTitle = openSaveFileDialog.fileTitle.Length;

            if (lastLoadedFile != string.Empty)
                openSaveFileDialog.file = System.IO.Path.GetFileNameWithoutExtension(lastLoadedFile);
            else
            {
                openSaveFileDialog.file = new String(currentSong.name.ToCharArray());
            }

            if (!forced)
                openSaveFileDialog.file += "(UNFORCED)";

            openSaveFileDialog.initialDir = "";
            openSaveFileDialog.title = "Save as";
            openSaveFileDialog.defExt = "chart";
            openSaveFileDialog.flags = 0x000002;        // Overwrite warning

            if (LibWrap.GetSaveFileName(openSaveFileDialog))
            {
                fileName = openSaveFileDialog.file;
            }
            else
            {
                throw new System.Exception("Could not open file");
            }
#endif

            Save(fileName, forced);           
        }
        catch (System.Exception e)
        {
            // User probably just canceled
            Debug.LogError(e.Message);
        }
    }

    void Save (string filename, bool forced = true)
    {
        if (currentSong != null)
        {
            editOccurred = false;            
            currentSong.Save(filename, forced);
            lastLoadedFile = filename;
        }
    }

    public void StartGameplay()
    {
        if (Globals.applicationMode == Globals.ApplicationMode.Playing || movement.transform.position.y < movement.initPos.y)
            return;

        stopResetPos = movement.transform.position;

        float strikelineYPos = visibleStrikeline.position.y - (0.01f * Globals.hyperspeed);     // Offset to prevent errors where it removes a note that is on the strikeline

        // Hide everything behind the strikeline
        foreach (Note note in currentChart.notes)
        {
            if (note.controller)
            {
                if (note.worldYPosition < strikelineYPos)
                    note.controller.HideFullNote();
                else
                    break;
            }
        }

        // Set position 3 seconds beforehand
        float time = Song.WorldYPositionToTime(strikelineYPos);
        movement.transform.position = new Vector3(movement.transform.position.x, Song.TimeToWorldYPosition(time - 3), movement.transform.position.z);

        Globals.bot = false;
        Play();
    }

    bool cancel;
    public void Play()
    {
        mixer.audioMixer.SetFloat("Pitch", 1 / (Globals.gameSpeed));
        play.interactable = false;
        Globals.applicationMode = Globals.ApplicationMode.Playing;
        cancel = false;

        float playPoint = Song.WorldYPositionToTime(strikelineAudio.position.y) + currentSong.offset;
        if (playPoint < 0)
        {
            StartCoroutine(delayedStartAudio(-playPoint * Globals.gameSpeed));
        }
        else
        {
            foreach (AudioSource source in musicSources)
                source.time = playPoint;       // No need to add audio calibration as position is base on the strikeline position

            foreach (AudioSource source in musicSources)
            {
                source.pitch = Globals.gameSpeed;
                source.Play();
            }
        } 
    }

    IEnumerator delayedStartAudio(float delay)
    {
        yield return new WaitForSeconds(delay);
        float playPoint = Song.WorldYPositionToTime(strikelineAudio.position.y) + currentSong.offset;

        if (!cancel && Globals.applicationMode == Globals.ApplicationMode.Playing)
        {
            if (playPoint >= 0)
            {
                foreach (AudioSource source in musicSources)
                    source.time = playPoint;

                foreach (AudioSource source in musicSources)
                {
                    source.pitch = Globals.gameSpeed;
                    source.Play();
                }
            }
            else
            {
                StartCoroutine(delayedStartAudio(-playPoint));
            }
        }
    }

    public void Stop()
    {
        cancel = true;
        play.interactable = true;
        Globals.applicationMode = Globals.ApplicationMode.Editor;
        foreach (AudioSource source in musicSources)
            source.Stop();

        if (currentChart != null)
        {
            foreach (Note note in currentChart.notes)
            {
                if (note.controller)
                    note.controller.Activate();
            }
        }
        if (stopResetPos != null)
            movement.transform.position = (Vector3)stopResetPos;

        Globals.bot = true;
        stopResetPos = null;
    }

    IEnumerator _Load()
    {
        editCheck();

        Song backup = currentSong;
#if TIMING_DEBUG
        float totalLoadTime = 0;
#endif
        try
        {           
#if UNITY_EDITOR
            currentFileName = UnityEditor.EditorUtility.OpenFilePanel("Load Chart", "", "chart");
#else
            OpenFileName openChartFileDialog = new OpenFileName();

            openChartFileDialog.structSize = Marshal.SizeOf(openChartFileDialog);
            openChartFileDialog.filter = "Chart files (*.chart)\0*.chart";
            openChartFileDialog.file = new String(new char[256]);
            openChartFileDialog.maxFile = openChartFileDialog.file.Length;

            openChartFileDialog.fileTitle = new String(new char[64]);
            openChartFileDialog.maxFileTitle = openChartFileDialog.fileTitle.Length;

            openChartFileDialog.initialDir = "";
            openChartFileDialog.title = "Open file";
            openChartFileDialog.defExt = "chart";

            if (LibWrap.GetOpenFileName(openChartFileDialog))
            {
                currentFileName = openChartFileDialog.file;
            }
            else
            {
                throw new System.Exception("Could not open file");
            }        
#endif
            
        }
        catch (System.Exception e)
        {
            // Most likely closed the window explorer, just ignore for now.
            currentSong = backup;
            Debug.LogError(e.Message);

            // Immediate exit
            yield break;
        }

        // Wait for saving to complete just in case
        while (currentSong.IsSaving) ;

#if TIMING_DEBUG
        totalLoadTime = Time.realtimeSinceStartup;
#endif
        currentSong = new Song(currentFileName);
        editOccurred = false;

#if TIMING_DEBUG
        Debug.Log("File load time: " + (Time.realtimeSinceStartup - totalLoadTime));
#endif

        // Wait for audio to fully load
        while (currentSong.IsAudioLoading)
            yield return null;

        lastLoadedFile = currentFileName;

        LoadSong(currentSong);

#if TIMING_DEBUG
        Debug.Log("Total load time: " + (Time.realtimeSinceStartup - totalLoadTime));
#endif
    }

    void LoadSong(Song song)
    {
        editOccurred = false;

        // Clear the previous song in the game-view
       /* foreach (Transform songObject in songObjectParent.transform)
        {
            Destroy(songObject.gameObject);
        }*/

#if TIMING_DEBUG
        float objectLoadTime = Time.realtimeSinceStartup;
#endif
        // Create the song objects
        //CreateSongObjects(song);

#if TIMING_DEBUG
        Debug.Log("Song objects load time: " + (Time.realtimeSinceStartup - objectLoadTime));
#endif

        // Load the default chart
        LoadChart(song.expert_single);

        // Reset audioSources upon successfull load
        foreach (AudioSource source in musicSources)
            source.clip = null;

        // Load audio
        if (currentSong.musicStream != null)
        {
            SetAudioSources();
            movement.SetPosition(0);
        }
    }

    // Chart should be part of the current song
    void LoadChart(Chart chart)
    {
        actionHistory = new ActionHistory();
        Stop();
#if TIMING_DEBUG
        float time = Time.realtimeSinceStartup;
#endif
        // Remove objects from previous chart
        /*foreach (Transform chartObject in chartObjectParent.transform)
        {
            Destroy(chartObject.gameObject);
        }*/

        currentChart = chart;

        songObjectPoolManager.NewChartReset();

        //CreateChartObjects(currentChart);
#if TIMING_DEBUG
        Debug.Log("Chart objects load time: " + (Time.realtimeSinceStartup - time));
#endif
    }

    // Create Sections, bpms, events and time signature objects
    GameObject CreateSongObjects(Song song)
    {
        for (int i = 0; i < song.sections.Length; ++i)
        {           
            // Attach the note to the object
            CreateSectionObject(song.sections[i]);
        }

        for (int i = 0; i < song.bpms.Length; ++i)
        {
            // Attach the note to the object
            CreateBPMObject(song.bpms[i]);
        }

        for (int i = 0; i < song.timeSignatures.Length; ++i)
        {
            // Attach the note to the object
            CreateTSObject(song.timeSignatures[i]);
        }

        return songObjectParent;
    }

    SectionController CreateSectionObject(Section section)
    {
        // Attach the note to the object
        SectionController controller = CreateSongObject(this.sectionPrefab).GetComponentInChildren<SectionController>();

        // Link controller and note together
        controller.section = section;
        controller.UpdateSongObject();
        return controller;
    }

    BPMController CreateBPMObject(BPM bpm)
    {
        // Attach the note to the object
        BPMController controller = CreateSongObject(this.bpmPrefab).GetComponent<BPMController>();

        // Link controller and note together
        controller.bpm = bpm;
        controller.UpdateSongObject();
        return controller;
    }

    TimesignatureController CreateTSObject(TimeSignature ts)
    {
        // Attach the note to the object
        TimesignatureController controller = CreateSongObject(this.tsPrefab).GetComponent<TimesignatureController>();

        // Link controller and note together
        controller.ts = ts;
        controller.UpdateSongObject();
        return controller;
    }

    // Create note, starpower and chart event objects
    GameObject CreateChartObjects(Chart chart)
    {    
        // Get reference to the current set of notes in case real notes get deleted
        Note[] notes = chart.notes;
        for (int i = 0; i < notes.Length; ++i)
        {
            // Make sure notes haven't been deleted
            if (notes[i].song != null)
            {
                CreateNoteObject(notes[i]);
            }
        }

        Starpower[] starpowers = chart.starPower;
        for (int i = 0; i < starpowers.Length; ++i)
        {
            // Make sure notes haven't been deleted
            if (notes[i].song != null)
            {
                CreateStarpowerObject(starpowers[i]);
            }
        }
        
        return chartObjectParent;
    }
    
    NoteController CreateNoteObject(Note note)
    {
        // Attach the note to the object
        NoteController controller = CreateChartObject(this.notePrefab).GetComponent<NoteController>();

        // Link controller and note together
        controller.note = note;
        controller.UpdateSongObject();
        return controller;
    }

    StarpowerController CreateStarpowerObject(Starpower starpower)
    {
        // Attach the note to the object
        StarpowerController controller = CreateChartObject(this.starpowerPrefab).GetComponent<StarpowerController>();

        // Link controller and note together
        controller.starpower = starpower;
        controller.UpdateSongObject();
        return controller;
    }

    GameObject CreateChartObject(GameObject chartObjectPrefab)
    {
        // Convert the chart data into gameobject
        GameObject chartObject = Instantiate(chartObjectPrefab);

        chartObject.transform.SetParent(chartObjectParent.transform);
        chartObject.SetActive(false);
        return chartObject;
    }

    GameObject CreateSongObject(GameObject songObjectPrefab)
    {
        // Convert the chart data into gameobject
        GameObject chartObject = Instantiate(songObjectPrefab);

        chartObject.transform.SetParent(songObjectParent.transform);
        //chartObject.SetActive(false);
        return chartObject;
    }

    // For dropdown UI
    public void LoadExpert()
    {
        LoadChart(currentSong.expert_single);
    }

    public void LoadExpertDoubleGuitar()
    {
        LoadChart(currentSong.expert_double_guitar);
    }

    public void LoadExpertDoubleBass()
    {
        LoadChart(currentSong.expert_double_bass);
    }

    public void LoadHard()
    {
        LoadChart(currentSong.hard_single);
    }

    public void LoadHardDoubleGuitar()
    {
        LoadChart(currentSong.hard_double_guitar);
    }

    public void LoadHardDoubleBass()
    {
        LoadChart(currentSong.hard_double_bass);
    }

    public void LoadMedium()
    {
        LoadChart(currentSong.medium_single);
    }

    public void LoadMediumDoubleGuitar()
    {
        LoadChart(currentSong.medium_double_guitar);
    }

    public void LoadMediumDoubleBass()
    {
        LoadChart(currentSong.medium_double_bass);
    }

    public void LoadEasy()
    {
        LoadChart(currentSong.easy_single);
    }

    public void LoadEasyDoubleGuitar()
    {
        LoadChart(currentSong.easy_double_guitar);
    }

    public void LoadEasyDoubleBass()
    {
        LoadChart(currentSong.easy_double_bass);
    }

    void enableSongObjects(SongObject[] songObjects, SongObject.ID id, uint min, uint max)
    {
        SongObject[] songObjectsRanged = SongObject.GetRange(songObjects, min, max);

        foreach (SongObject songObject in songObjectsRanged)
        {
            if (songObject.controller != null && !songObject.controller.gameObject.activeSelf)
                songObject.controller.gameObject.SetActive(true);
        }

        // Check if sustains need to be rendered
        if (id == SongObject.ID.Note && songObjectsRanged.Length > 0)
        {

            // Find the last known note of each fret type to find any sustains that might overlap. Cancel if there's an open note.
            foreach (Note prevNote in Note.GetPreviousOfSustains(songObjectsRanged[0] as Note))
            {
                if (prevNote.controller != null)
                    prevNote.controller.gameObject.SetActive(true);
            }
        }
        else if (id == SongObject.ID.Starpower)
        {
            int arrayPos = SongObject.FindClosestPosition(min, songObjects);
            if (arrayPos != Globals.NOTFOUND)
            {
                // Find the back-most position
                while (arrayPos > 0 && songObjects[arrayPos].position >= min)
                {
                    --arrayPos;
                }
                // Render previous sp sustain in case of overlap into current position
                if (arrayPos >= 0)
                {
                    if (songObjects[arrayPos].controller != null)
                        songObjects[arrayPos].controller.gameObject.SetActive(true);
                }
            }              
        }
        /*
        // Enable all objects within the min-max position
        int arrayPos = SongObject.FindClosestPosition(min, songObjects);
        if (arrayPos != Globals.NOTFOUND)
        {
            // Find the back-most position
            while (arrayPos > 0 && songObjects[arrayPos].position >= min)
            {
                --arrayPos;
            }

            // Check if sustains need to be rendered
            if (id == SongObject.ID.Note)
            {
                // Check if the note found needs to be rendered for it's sustain
                foreach (Note chordNote in (songObjects[arrayPos] as Note).GetChord())
                {
                    if (chordNote.controller != null)
                        chordNote.controller.gameObject.SetActive(true);
                }

                // Find the last known note of each fret type to find any sustains that might overlap. Cancel if there's an open note.
                foreach(Note prevNote in Note.GetPreviousOfSustains(songObjects[arrayPos] as Note))
                {
                    if (prevNote.controller != null)
                        prevNote.controller.gameObject.SetActive(true);
                }
            }
            else if (id == SongObject.ID.Starpower)
            {
                // Render previous sp sustain in case of overlap into current position
                if (arrayPos >= 0)
                {
                    if (songObjects[arrayPos].controller != null)
                        songObjects[arrayPos].controller.gameObject.SetActive(true);
                }
            }
        }

        foreach (SongObject songObject in SongObject.GetRange(songObjects, min, max))
        {
            if (songObject.controller != null && !songObject.controller.gameObject.activeSelf)
                songObject.controller.gameObject.SetActive(true);
        }*/
    }

    public void EnableMenu(DisplayMenu menu)
    {
        menu.gameObject.SetActive(true);
    }

    public void SetAudioSources()
    {
        musicSources[MUSIC_STREAM_ARRAY_POS].clip = currentSong.musicStream;
        musicSources[GUITAR_STREAM_ARRAY_POS].clip = currentSong.guitarStream;
        musicSources[RHYTHM_STREAM_ARRAY_POS].clip = currentSong.rhythmStream;
    }

    public void UndoWrap()
    {
        actionHistory.Undo(this);
    }

    public void RedoWrap()
    {
        actionHistory.Redo(this);
    }
}
