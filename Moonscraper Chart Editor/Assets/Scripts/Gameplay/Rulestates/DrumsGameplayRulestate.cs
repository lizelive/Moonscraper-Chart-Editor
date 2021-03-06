﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DrumsGameplayRulestate : BaseGameplayRulestate {

    DrumsNoteHitAndMissDetect hitAndMissNoteDetect;

    public DrumsGameplayRulestate(MissFeedback missFeedback) : base(missFeedback)
    {
        hitAndMissNoteDetect = new DrumsNoteHitAndMissDetect(HitNote, MissNote);
    }

    public void Update(float time, HitWindow<DrumsNoteHitKnowledge> hitWindow, GamepadInput drumsInput)
    {
        uint noteStreak = stats.noteStreak;
        int missCount = UpdateWindowExit(time, hitWindow);
        LaneInfo laneInfo = ChartEditor.GetInstance().laneInfo;

        for (int i = 0; i < missCount; ++i)
        {
            if (noteStreak > 0)
                Debug.Log("Missed due to note falling out of window");

            MissNote(time, DrumsNoteHitAndMissDetect.MissSubType.NoteMiss, null);
        }

        hitAndMissNoteDetect.Update(time, hitWindow, drumsInput, stats.noteStreak, laneInfo);
    }

    public override void Reset()
    {
        base.Reset();
        hitAndMissNoteDetect.Reset();
    }

    void HitNote(float time, DrumsNoteHitKnowledge noteHitKnowledge)
    {
        base.HitNote(time, noteHitKnowledge);
    }

    void MissNote(float time, DrumsNoteHitAndMissDetect.MissSubType missSubType, DrumsNoteHitKnowledge noteHitKnowledge)
    {
        base.MissNote(time, missSubType == DrumsNoteHitAndMissDetect.MissSubType.NoteMiss, noteHitKnowledge);
    }
}
