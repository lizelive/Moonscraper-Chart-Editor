﻿// Copyright (c) 2016-2017 Alexander Ong
// See LICENSE in project root for license information.

using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class BPMPropertiesPanelController : PropertiesPanelController {
    public BPM currentBPM { get { return (BPM)currentSongObject; } set { currentSongObject = value; } }
    public InputField bpmValue;
    public Toggle anchorToggle;
    public Button increment, decrement;
    public Selectable[] AnchorAheadDisable;

    float incrementalTimer = 0;
    float autoIncrementTimer = 0;
    const float AUTO_INCREMENT_WAIT_TIME = 0.5f;
    const float AUTO_INCREMENT_RATE = 0.08f;

    uint? lastAutoVal = null;
    BPM anchorAdjustmentOriginalValue = null;
    BPM anchorAdjustment = null;

    BPM prevBPM;

    void Start()
    {
        bpmValue.onValidateInput = validatePositiveDecimal;
    }

    void OnEnable()
    {
        bool edit = ChartEditor.isDirty;
        UpdateBPMInputFieldText();

        incrementalTimer = 0;
        autoIncrementTimer = 0;

        ChartEditor.isDirty = edit;
    }

    void UpdateBPMInputFieldText()
    {
        if (currentBPM != null)
            bpmValue.text = ((float)currentBPM.value / 1000.0f).ToString();
    }

    void Controls()
    {
        if (ShortcutInput.GetInputDown(Shortcut.ToggleBpmAnchor) && anchorToggle.IsInteractable())
            anchorToggle.isOn = !anchorToggle.isOn;
    }

    protected override void Update()
    {
        base.Update();
        if (currentBPM != null)
        {
            // Update inspector information
            positionText.text = "Position: " + currentBPM.tick.ToString();
            if (!Services.IsTyping)
                UpdateBPMInputFieldText();

            anchorToggle.isOn = currentBPM.anchor != null;

            bool interactable = !IsNextBPMAnAnchor();
            foreach (Selectable ui in AnchorAheadDisable)
                ui.interactable = interactable;

        }

        editor.currentSong.UpdateCache();

        if (incrementalTimer > AUTO_INCREMENT_WAIT_TIME)
            autoIncrementTimer += Time.deltaTime;
        else
            autoIncrementTimer = 0;

        if (!(ShortcutInput.GetInput(Shortcut.BpmIncrease) && ShortcutInput.GetInput(Shortcut.BpmDecrease)))    // Can't hit both at the same time
        {
            if (!Services.IsTyping && !Globals.modifierInputActive)
            {
                if (ShortcutInput.GetInputDown(Shortcut.BpmDecrease) && decrement.interactable)
                {
                    lastAutoVal = currentBPM.value;
                    decrement.onClick.Invoke();
                }
                else if (ShortcutInput.GetInputDown(Shortcut.BpmIncrease) && increment.interactable)
                {
                    lastAutoVal = currentBPM.value;
                    increment.onClick.Invoke();
                }

                // Adjust to time rather than framerate
                if (incrementalTimer > AUTO_INCREMENT_WAIT_TIME && autoIncrementTimer > AUTO_INCREMENT_RATE)
                {
                    if (ShortcutInput.GetInput(Shortcut.BpmDecrease) && decrement.interactable)
                        decrement.onClick.Invoke();
                    else if (ShortcutInput.GetInput(Shortcut.BpmIncrease) && increment.interactable)
                        increment.onClick.Invoke();

                    autoIncrementTimer = 0;
                }

                // 
                if (ShortcutInput.GetInput(Shortcut.BpmIncrease) || ShortcutInput.GetInput(Shortcut.BpmDecrease))
                {
                    incrementalTimer += Time.deltaTime;
                    ChartEditor.isDirty = true;
                }
            }
            else
                incrementalTimer = 0;

            // Handle key release, add in action history
            if ((ShortcutInput.GetInputUp(Shortcut.BpmIncrease) || ShortcutInput.GetInputUp(Shortcut.BpmDecrease)) && lastAutoVal != null)
            {
                incrementalTimer = 0;
                editor.actionHistory.Insert(new ActionHistory.Modify(new BPM(currentSongObject.tick, (uint)lastAutoVal), currentSongObject));
                if (anchorAdjustment != null)
                {
                    editor.actionHistory.Insert(new ActionHistory.Modify(anchorAdjustmentOriginalValue, anchorAdjustment));
                    anchorAdjustment = null;
                    anchorAdjustmentOriginalValue = null;
                }

                ChartEditor.isDirty = true;
                lastAutoVal = null;// currentBPM.value;
            }
        }

        Controls();

        prevBPM = currentBPM;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        currentBPM = null;
        editor.currentSong.UpdateCache();
    }

    public void UpdateBPMValue(string value)
    {
        if (prevBPM != currentBPM)
            return;

        if (lastAutoVal == null)
            lastAutoVal = currentBPM.value;

        uint prevValue = currentBPM.value;
        if (value.Length > 0 && value[value.Length - 1] == '.')
            value = value.Remove(value.Length - 1);
        
        if (value != string.Empty && value[value.Length - 1] != '.' && currentBPM != null && float.Parse(value) != 0)
        {
            // Convert the float string to an int string
            int zerosToAdd = 0;
            if (value.Contains("."))
            {
                int index = value.IndexOf('.');

                zerosToAdd = 7 - (value.Length + (3 - index));      // string length can be a total of 7 characters; 6 digits and the "."
                value = value.Remove(index, 1);
            }
            else
            {
                zerosToAdd = 3;     // Number of zeros after the decimal point
            }

            for (int i = 0; i < zerosToAdd; ++i)
                value += "0";

            // Actually parse the value now
            uint parsedVal = uint.Parse(value);// * 1000;     // Store it in another variable due to weird parsing-casting bug at decimal points of 2 or so. Seems to fix it for whatever reason.

            AdjustForAnchors(parsedVal);
            //currentBPM.value = (uint)parsedVal;
            //UpdateInputFieldRecord();
        }
        else if (value == ".")
            bpmValue.text = string.Empty;

        if (prevValue != currentBPM.value)
            ChartEditor.isDirty = true;
    }

    public void EndEdit(string value)
    {
        if (value == string.Empty || currentBPM.value <= 0)
        {
            //currentBPM.value = 120000;
            AdjustForAnchors(120000);
            //UpdateInputFieldRecord();
        }

        UpdateBPMInputFieldText();
        //Debug.Log(((float)currentBPM.value / 1000.0f).ToString().Length);

        // Add action recording here?
        if (lastAutoVal != null && lastAutoVal != currentBPM.value)
        {
            editor.actionHistory.Insert(new ActionHistory.Modify(new BPM(currentBPM.tick, (uint)lastAutoVal), currentBPM), -ActionHistory.ACTION_WINDOW_TIME - 0.01f);
            if (anchorAdjustment != null)
            {
                editor.actionHistory.Insert(new ActionHistory.Modify(anchorAdjustmentOriginalValue, anchorAdjustment), -ActionHistory.ACTION_WINDOW_TIME - 0.01f);
                anchorAdjustment = null;
                anchorAdjustmentOriginalValue = null;
            }
        }

        anchorAdjustment = null;
        anchorAdjustmentOriginalValue = null;
        lastAutoVal = null;
    }

    public char validatePositiveDecimal(string text, int charIndex, char addedChar)
    {
        int selectionLength = Mathf.Abs(bpmValue.selectionAnchorPosition - bpmValue.selectionFocusPosition);
        int selectStart = bpmValue.selectionAnchorPosition < bpmValue.selectionFocusPosition ? bpmValue.selectionAnchorPosition : bpmValue.selectionFocusPosition;

        if (selectStart < bpmValue.text.Length)
            text = text.Remove(selectStart, selectionLength);

        if ((addedChar == '.' && !text.Contains(".") && text.Length > 0) || (addedChar >= '0' && addedChar <= '9'))
        {
            if ((text.Contains(".") && text.IndexOf('.') > 2 && charIndex <= text.IndexOf('.')) || (addedChar != '.' && !text.Contains(".") && text.Length > 2))
                return '\0';

            if (addedChar != '.')
            {
                if (bpmValue.selectionAnchorPosition == text.Length && bpmValue.selectionFocusPosition == 0)
                    return addedChar;

                if (!text.Contains(".") && text.Length < 3)         // Adding a number, no decimal point
                    return addedChar;
                else if (text.Contains(".") && text.IndexOf('.') <= 3)
                    return addedChar;
            }

             return addedChar;
        }

        return '\0';
    }

    public void IncrementBPM()
    {
        BPM original = (BPM)currentBPM.Clone();
        AdjustForAnchors(currentBPM.value + 1000);

        if (Input.GetMouseButtonUp(0) && currentBPM.value != original.value)
            editor.actionHistory.Insert(new ActionHistory.Modify(original, currentBPM));

        UpdateBPMInputFieldText(); 
    }

    public void DecrementBPM()
    {
        BPM original = (BPM)currentBPM.Clone();
        uint newValue = currentBPM.value;

        if (newValue > 1000)
            newValue -= 1000;

        AdjustForAnchors(newValue);

        if (Input.GetMouseButtonUp(0) && currentBPM.value != original.value)
            editor.actionHistory.Insert(new ActionHistory.Modify(original, currentBPM));

        UpdateBPMInputFieldText();
    }

    bool AdjustForAnchors(uint newBpmValue)
    {
        ChartEditor.GetInstance().songObjectPoolManager.SetAllPoolsDirty();

        int pos = SongObjectHelper.FindObjectPosition(currentBPM, currentBPM.song.bpms);
        if (pos != SongObjectHelper.NOTFOUND)
        {
            BPM anchor = null;
            BPM bpmToAdjust = null;

            int anchorPos = 0;

            // Get the next anchor
            for (int i = pos + 1; i < currentBPM.song.bpms.Count; ++i)
            {
                if (currentBPM.song.bpms[i].anchor != null)
                {
                    anchor = currentBPM.song.bpms[i];
                    anchorPos = i;
                    // Get the bpm before that anchor
                    bpmToAdjust = currentBPM.song.bpms[i - 1];

                    break;
                }
            }

            if (anchor == null || bpmToAdjust == currentBPM)
            {
                if (currentBPM.value != newBpmValue)
                    ChartEditor.isDirty = true;

                currentBPM.value = newBpmValue;
                return true;
            }

            // Calculate the minimum the bpm can adjust to
            const float MIN_DT = 0.01f;

            float bpmTime = (float)anchor.anchor - MIN_DT;
            float resolution = currentBPM.song.resolution;
            // Calculate the time of the 2nd bpm pretending that the adjustable one is super close to the anchor
            for (int i = anchorPos - 1; i > pos + 1; --i)
            {
                // Calculate up until 2 bpms before the anchor
                // Re-hash of the actual time calculation equation in Song.cs
                bpmTime -= (float)TickFunctions.DisToTime(currentBPM.song.bpms[i - 1].tick, currentBPM.song.bpms[i].tick, resolution, currentBPM.song.bpms[i - 1].value / 1000.0f);
            }

            float timeBetweenFirstAndSecond = bpmTime - currentBPM.time;
            // What bpm will result in this exact time difference?
            uint minVal = (uint)(Mathf.Ceil((float)TickFunctions.DisToBpm(currentBPM.song.bpms[pos].tick, currentBPM.song.bpms[pos + 1].tick, timeBetweenFirstAndSecond, currentBPM.song.resolution)) * 1000);

            if (newBpmValue < minVal)
                newBpmValue = minVal;

            if (anchorAdjustment == null)
            {
                anchorAdjustment = bpmToAdjust;
                anchorAdjustmentOriginalValue = new BPM(bpmToAdjust);
            }

            BPM anchorBPM = anchor;
            uint oldValue = currentBPM.value;
            currentBPM.value = newBpmValue;

            double deltaTime = (double)anchorBPM.anchor - editor.currentSong.LiveTickToTime(bpmToAdjust.tick, editor.currentSong.resolution);
            uint newValue = (uint)Mathf.Round((float)(TickFunctions.DisToBpm(bpmToAdjust.tick, anchorBPM.tick, deltaTime, editor.currentSong.resolution) * 1000.0d));
            currentBPM.value = oldValue;
            if (deltaTime > 0 && newValue > 0)
            {
                if (newValue != 0)
                    bpmToAdjust.value = newValue;
                currentBPM.value = newBpmValue;

                ChartEditor.isDirty = true;
            }
        }
        else
        {
            if (currentBPM.value != newBpmValue)
                ChartEditor.isDirty = true;

            currentBPM.value = newBpmValue;
        }

        return true;
    }

    BPM NextBPM()
    {
        int pos = SongObjectHelper.FindObjectPosition(currentBPM, currentBPM.song.bpms);
        if (pos != SongObjectHelper.NOTFOUND && pos + 1 < currentBPM.song.bpms.Count)
        {
            return currentBPM.song.bpms[pos + 1];
        }

        return null;
    }

    bool IsNextBPMAnAnchor()
    {
        BPM next = NextBPM();
        if (next != null && next.anchor != null)
            return true;

        return false;
    }

    public void SetAnchor(bool anchored)
    {
        if (currentBPM != prevBPM)
            return;

        BPM original = new BPM(currentBPM);
        if (anchored)
            currentBPM.anchor = currentBPM.song.LiveTickToTime(currentBPM.tick, currentBPM.song.resolution);
        else
            currentBPM.anchor = null;

        editor.actionHistory.Insert(new ActionHistory.Modify(original, currentBPM));

        Debug.Log("Anchor toggled to: " + currentBPM.anchor);
    }
}
