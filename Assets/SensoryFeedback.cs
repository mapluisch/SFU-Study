using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SensoryModality { none = 0, visual = 1, auditory = 2, tactile = 3 };
public enum FeedbackOrientation { none = 0, left = 1, right = 2 };

public class SensoryFeedback : MonoBehaviour
{
    public SensoryModality currentModality;
    void Start()
    {
        currentModality = SensoryModality.none;
    }

    public void SetModality(SensoryModality modality)
    {
        currentModality = modality;
    }

    public void VisualFeedback()
    {
        if (currentModality == SensoryModality.visual)
        {
            Debug.Log("Visual Feedback");
        }
    }

    public void AuditoryFeedback()
    {
        if (currentModality == SensoryModality.auditory)
        {
            Debug.Log("Auditory Feedback");
        }
    }

    public void TactileFeedback()
    {
        if (currentModality == SensoryModality.tactile)
        {
            Debug.Log("Tactile Feedback");
        }
    }

}
