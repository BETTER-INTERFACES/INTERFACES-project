using System;
using UnityEngine;

[System.Serializable]
public class GAMAMessage2
{
    public string message_;
    public string timer_start;
    public string init_;
    public string scenario;
    public string score;
    public string add_to_score;
    public string phase;
    public string action_limit;

    public static GAMAMessage2 CreateFromJSON(string jsonString)
    {
        return JsonUtility.FromJson<GAMAMessage2>(jsonString);
    }
}