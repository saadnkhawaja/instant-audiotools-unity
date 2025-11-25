/*
###:   ____  _  __  
###:  / ___|| |/ /   ░░░ SK ░░░
###: | |__  | ' /    Created by: Saad Khawaja
###:  \__ \ | . \    github.com/saadnkhawaja
###:  |___/ |_|\_\   
*/

using System;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.IO;

public class InstantAudioTools : EditorWindow
{
    private AudioClip selectedClip;
    private float[] originalClipData;
    private float[] previewClipData;
    private Texture2D waveformTexture;

    private float volume = 1f;
    private float fadeInDuration = 0f;
    private float fadeOutDuration = 0f;

    private float pendingFadeInDuration = 0f;
    private float pendingFadeOutDuration = 0f;
    private float pendingVolume = 1f;

    private float markerStart = 0f;
    private float markerEnd = 1f;
    private float pendingMarkerStart = 0f;
    private float pendingMarkerEnd = 1f;

    private bool showAudioOps;
    private AudioSource previewSource;
    
    

    private bool hasProcessedOperations = false;

    [MenuItem("Tools/Saad Khawaja/Instant Audio Tools")]
    public static void ShowWindow()
    {
        GetWindow<InstantAudioTools>("Instant Audio Tools");
    }

    private void OnEnable()
    {
        EditorApplication.update += Repaint;
        Selection.selectionChanged += OnSelectionChange;
        OnSelectionChange();
    }

    private void OnDisable()
    {
        EditorApplication.update -= Repaint;
        Selection.selectionChanged -= OnSelectionChange;
        StopPreview();
    }

    private void OnSelectionChange()
    {
        if (Selection.activeObject is AudioClip clip)
        {
            selectedClip = clip;
            originalClipData = new float[clip.samples * clip.channels];
            clip.GetData(originalClipData, 0);
            previewClipData = (float[])originalClipData.Clone();

            markerStart = pendingMarkerStart = 0f;
            markerEnd = pendingMarkerEnd = selectedClip.length;

            fadeInDuration = pendingFadeInDuration = 0f;
            fadeOutDuration = pendingFadeOutDuration = 0f;
            volume = pendingVolume = 1f;

            ApplyVolumePreview();
        }
    }

    private void ApplyPendingChanges()
    {
        markerStart = pendingMarkerStart;
        markerEnd = pendingMarkerEnd;
        fadeInDuration = pendingFadeInDuration;
        fadeOutDuration = pendingFadeOutDuration;
        volume = pendingVolume;

        ApplyVolumePreview();
    }

    void ApplySaveChanges()
    {
        markerStart = pendingMarkerStart;
        markerEnd = pendingMarkerEnd;
        fadeInDuration = pendingFadeInDuration;
        fadeOutDuration = pendingFadeOutDuration;
        volume = pendingVolume;

        ApplyVolumePreview();

        // 🧠 Trim the preview clip data to marker selection
        int sampleStart = Mathf.FloorToInt(markerStart * selectedClip.frequency) * selectedClip.channels;
        int sampleEnd = Mathf.FloorToInt(markerEnd * selectedClip.frequency) * selectedClip.channels;
        int sampleLength = Mathf.Max(0, sampleEnd - sampleStart);

        if (sampleLength > 0 && sampleEnd <= previewClipData.Length)
        {
            previewClipData = previewClipData.Skip(sampleStart).Take(sampleLength).ToArray();
        }
    }

    private void OnGUI()
    {
        if (selectedClip == null)
        {
            EditorGUILayout.HelpBox("Select an AudioClip from the Project window.", MessageType.Info);
            return;
        }

        DrawLogoAndInfo();
        DrawWaveformSection();
        DrawMarkerControls();
        DrawFadeControls();
        DrawVolumeControls();
        DrawAudioOperations();
        DrawPlaybackButtons();
        GUILayout.Space(10);
        DrawExportButtons();
    }
    
    
    
    private void DrawLogoAndInfo()
{
    Texture2D logoTexture = Resources.Load<Texture2D>("logo_iat");
    if (logoTexture != null)
    {
        GUILayout.Label(logoTexture, GUILayout.Height(128));
    }
    else
    {
        GUIStyle logoStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter
        };
        GUILayout.Label("🎵 Instant Audio Tools | Saad Khawaja", logoStyle);
    }

    EditorGUILayout.LabelField("Selected Clip:", selectedClip.name + Path.GetExtension(AssetDatabase.GetAssetPath(selectedClip)), EditorStyles.boldLabel);
    EditorGUILayout.LabelField("Length", $"{selectedClip.length:F2} sec");
    EditorGUILayout.LabelField("Samples", selectedClip.samples.ToString());
    EditorGUILayout.LabelField("Channels", selectedClip.channels.ToString());

    GUILayout.Space(10);
}

private void DrawWaveformSection()
{
    float waveformWidth = position.width - 20;
    DrawWaveform((int)waveformWidth, 100);
    GUILayout.Space(30);
}

private void DrawMarkerControls()
{
    EditorGUI.BeginChangeCheck();
    EditorGUILayout.LabelField($"Start: {pendingMarkerStart:F2}s | End: {pendingMarkerEnd:F2}s | Duration: {(pendingMarkerEnd - pendingMarkerStart):F2}s");
    EditorGUILayout.MinMaxSlider("Trim", ref pendingMarkerStart, ref pendingMarkerEnd, 0f, selectedClip.length);
    if (EditorGUI.EndChangeCheck())
        Repaint();
    GUILayout.Space(10);
}

private void DrawFadeControls()
{
    EditorGUI.BeginChangeCheck();
    pendingFadeInDuration = EditorGUILayout.Slider("Fade In Duration", pendingFadeInDuration, 0f, selectedClip.length);
    pendingFadeOutDuration = EditorGUILayout.Slider("Fade Out Duration", pendingFadeOutDuration, 0f, selectedClip.length);
    if (EditorGUI.EndChangeCheck())
        Repaint();
}

private void DrawVolumeControls()
{
    GUILayout.BeginHorizontal();
    EditorGUILayout.LabelField("Volume (Gain)", GUILayout.Width(110));

    pendingVolume = GUILayout.HorizontalSlider(pendingVolume, 0f, 3f);
    pendingVolume = Mathf.Round(pendingVolume * 10f) / 10f;

    pendingVolume = EditorGUILayout.FloatField(pendingVolume, GUILayout.Width(50));
    pendingVolume = Mathf.Clamp(pendingVolume, 0f, 3f);

    GUILayout.EndHorizontal();
    GUILayout.Space(10);
}

private void DrawAudioOperations()
{
    showAudioOps = EditorGUILayout.Foldout(showAudioOps, "⚙ Audio Operations", true);
    if (showAudioOps)
    {
        GUILayout.BeginVertical("box");
        if (GUILayout.Button("Trim Start (Remove silence)")) TrimSilenceStart();
        if (GUILayout.Button("Trim End (Remove silence)")) TrimSilenceEnd();
        if (GUILayout.Button("Reverse Audio")) ReverseAudio();
        if (GUILayout.Button("Normalize")) NormalizeAudio();
        if (GUILayout.Button("Remove Noise")) RemoveNoise();
        if (GUILayout.Button("Convert to Mono")) ForceMono();
        GUILayout.EndVertical();
    }
    GUILayout.Space(10);
}

private void DrawPlaybackButtons()
{
    GUILayout.BeginHorizontal();

    GUI.backgroundColor = Color.green;
    if (previewSource != null && previewSource.clip != null && previewSource.isPlaying)
    {
        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("■ Stop", GUILayout.Height(40)))
            StopPreview();
    }
    else
    {
        if (GUILayout.Button("▶ Play", GUILayout.Height(40)))
        {
            ApplyPendingChanges();
            PlayPreview();
        }
    }

    GUI.backgroundColor = Color.gray;
    if (GUILayout.Button("⟲ Revert", GUILayout.Height(40)))
        ResetAllValues();

    GUI.backgroundColor = Color.white;
    GUILayout.EndHorizontal();
}


private void ResetAllValues()
{
    StopPreview();

    pendingFadeInDuration = fadeInDuration = 0f;
    pendingFadeOutDuration = fadeOutDuration = 0f;
    pendingVolume = volume = 1f;
    pendingMarkerStart = markerStart = 0f;
    pendingMarkerEnd = markerEnd = selectedClip.length;
    hasProcessedOperations = false; // ✅ reset operations flag


    OnSelectionChange(); // reloads and reapplies
}

    private void DrawExportButtons()
    {
        // Hide if no changes
        if (!HasUserMadeChanges()) return;

        string path = AssetDatabase.GetAssetPath(selectedClip);
        string extension = Path.GetExtension(path).ToLowerInvariant();
        bool isWav = extension == ".wav";

        if (!isWav)
        {
            EditorGUILayout.HelpBox(
                $"⚠ This is a *{extension.ToUpper()}* file.\n" +
                "Instant Audio Tools can only overwrite WAV files.\n" +
                "Please use 'Save Audio As' instead.",
                MessageType.Warning
            );
            GUILayout.Space(5);
        }

        // Save and Overwrite
        EditorGUI.BeginDisabledGroup(!isWav);
        if (GUILayout.Button("💾 Save and Overwrite", GUILayout.Height(25)))
        {
            ApplySaveChanges();

            string absolutePath = Path.Combine(Directory.GetCurrentDirectory(), path);
            int sampleCount = previewClipData.Length / selectedClip.channels;

            var tempClip = AudioClip.Create("TempClip", sampleCount, selectedClip.channels, selectedClip.frequency, false);
            tempClip.SetData(previewClipData, 0);

            byte[] wavData = WavUtility.FromFloatArray(previewClipData, selectedClip.channels, selectedClip.frequency, out _, false);

            // Additional guard against mismatched byte length
            if (wavData == null || wavData.Length == 0)
            {
                Debug.LogError("WAV data is invalid. Export failed.");
                return;
            }

            File.WriteAllBytes(absolutePath, wavData);
            AssetDatabase.Refresh();

            ResetAllValues();

            AudioClip updatedClip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            Selection.activeObject = updatedClip;
            OnSelectionChange();
        }
        EditorGUI.EndDisabledGroup();

        // Save As new file
        GUI.backgroundColor = Color.cyan;
        if (GUILayout.Button("Save Audio As", GUILayout.Height(25)))
        {
            ApplySaveChanges();
            SaveAsNewClip();
        }
        GUI.backgroundColor = Color.white;
    }



private bool HasUserMadeChanges()
{
    return hasProcessedOperations ||
           pendingFadeInDuration > 0f ||
           pendingFadeOutDuration > 0f ||
           Mathf.Abs(pendingVolume - 1f) > 0.01f ||
           pendingMarkerStart > 0.001f ||
           pendingMarkerEnd < selectedClip.length - 0.001f;
}

 
private void DrawWaveform(int width, int height)
{
    if (waveformTexture == null || waveformTexture.width != width)
        GenerateWaveformTexture(width, height);

    float padding = 10f;
    Rect fullRect = GUILayoutUtility.GetRect(width, height);
    fullRect.xMin += padding;
    fullRect.xMax -= padding;

    // Begin group to clip anything drawn inside
    GUI.BeginGroup(fullRect);

    // 🧠 Scale the waveform vertically and center it visually
    float scaledHeight = height * pendingVolume;
    float yOffset = (height - scaledHeight) / 2f;
    Rect scaledRect = new Rect(0, yOffset, fullRect.width, scaledHeight);
    GUI.DrawTexture(scaledRect, waveformTexture);

    GUI.EndGroup(); // Restrict drawing to waveform box only

    // ✨ Highlight selection (not clipped)
    float trimStartPct = Mathf.Clamp01(pendingMarkerStart / selectedClip.length);
    float trimEndPct = Mathf.Clamp01(pendingMarkerEnd / selectedClip.length);

    float startX = Mathf.Lerp(fullRect.x, fullRect.xMax, trimStartPct);
    float endX = Mathf.Lerp(fullRect.x, fullRect.xMax, trimEndPct);
    float selectionWidth = endX - startX;

    Color prevColor = GUI.color;
    GUI.color = new Color(1f, 0.6f, 0.3f, 0.3f); // orange highlight
    GUI.DrawTexture(new Rect(startX, fullRect.y, selectionWidth, fullRect.height), Texture2D.whiteTexture);
    GUI.color = prevColor;

    // 🎯 Static time ruler
    Handles.color = Color.white;
    string[] baseLabels = { "0s", $"{(selectedClip.length / 2f):F1}s", $"{selectedClip.length:F1}s" };
    float[] basePositions = { 0f, 0.5f, 1f };

    for (int i = 0; i < basePositions.Length; i++)
    {
        float x = Mathf.Lerp(fullRect.x, fullRect.xMax, basePositions[i]);
        Handles.DrawLine(new Vector2(x, fullRect.yMax), new Vector2(x, fullRect.yMax + 5));
        GUI.Label(new Rect(x - 10, fullRect.yMax + 5, 40, 20), baseLabels[i], EditorStyles.miniLabel);
    }

    // ✨ Extra trim ticks (if markers changed)
    if (pendingMarkerStart > 0.01f || pendingMarkerEnd < selectedClip.length - 0.01f)
    {
        string[] trimLabels = { $"{pendingMarkerStart:F2}s", $"{pendingMarkerEnd:F2}s" };
        float[] trimPositions = { trimStartPct, trimEndPct };

        for (int i = 0; i < trimPositions.Length; i++)
        {
            float x = Mathf.Lerp(fullRect.x, fullRect.xMax, trimPositions[i]);
            Handles.color = Color.cyan;
            Handles.DrawLine(new Vector2(x, fullRect.yMin), new Vector2(x, fullRect.yMax));
            GUI.Label(new Rect(x - 10, fullRect.yMin - 15, 40, 20), trimLabels[i], EditorStyles.miniLabel);
        }
    }
}

    private void ReverseAudio() => ApplyAndReplace(data => data.Reverse().ToArray());

    private void NormalizeAudio()
    {
        float max = originalClipData.Max(Mathf.Abs);
        if (max > 0f) ApplyAndReplace(data => data.Select(s => s / max).ToArray());
    }

    private void RemoveNoise() =>
        ApplyAndReplace(data => data.Select(s => Mathf.Abs(s) < 0.01f ? 0f : s).ToArray());

    private void ForceMono()
    {
        if (selectedClip.channels == 1) return;
        ApplyAndReplace(data =>
        {
            int totalSamples = data.Length / selectedClip.channels;
            float[] mono = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
            {
                float avg = 0f;
                for (int c = 0; c < selectedClip.channels; c++)
                    avg += data[i * selectedClip.channels + c];
                mono[i] = avg / selectedClip.channels;
            }
            return mono;
        });
    }

    private void ApplyAndReplace(System.Func<float[], float[]> processor)
    {
        hasProcessedOperations = true;

        originalClipData = processor.Invoke(originalClipData);
        ApplyVolumePreview();
    }


    private void GenerateWaveformTexture(int width = 1024, int height = 100)
    {
        if (previewClipData == null || selectedClip == null) return;

        waveformTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        waveformTexture.hideFlags = HideFlags.HideAndDontSave;

        Color[] pixels = Enumerable.Repeat(new Color(0.15f, 0.15f, 0.15f, 1f), width * height).ToArray();
        Color waveColor = Color.cyan;

        int packSize = Mathf.Max(1, previewClipData.Length / width);
        int halfHeight = height / 2;

        for (int x = 0; x < width; x++)
        {
            float max = 0f;
            for (int i = 0; i < packSize; i++)
            {
                int index = x * packSize + i;
                if (index < previewClipData.Length)
                    max = Mathf.Max(max, Mathf.Abs(previewClipData[index]));
            }

            // 💡 Normalize only, no volume scaling
            int yMax = Mathf.Clamp((int)(max * halfHeight), 0, halfHeight);
            for (int y = halfHeight - yMax; y < halfHeight + yMax; y++)
                pixels[y * width + x] = waveColor;
        }

        waveformTexture.SetPixels(pixels);
        waveformTexture.Apply();
    }


    private void ApplyVolumePreview()
    {
        if (originalClipData == null) return;

        int length = originalClipData.Length;
        previewClipData = new float[length];

        int fadeInSamples = Mathf.FloorToInt(fadeInDuration * selectedClip.frequency) * selectedClip.channels;
        int fadeOutSamples = Mathf.FloorToInt(fadeOutDuration * selectedClip.frequency) * selectedClip.channels;

        for (int i = 0; i < length; i++)
        {
            float sample = originalClipData[i] * volume;

            if (i < fadeInSamples)
                sample *= i / (float)fadeInSamples;

            if (i >= length - fadeOutSamples)
            {
                int fadeOutIndex = i - (length - fadeOutSamples);
                sample *= 1f - (fadeOutIndex / (float)fadeOutSamples);
            }

            previewClipData[i] = sample;
        }

        GenerateWaveformTexture();
    }

    private void TrimSilenceStart()
    {
        int channels = selectedClip.channels;
        int startSample = 0;
        for (int i = 0; i < originalClipData.Length; i += channels)
        {
            if (Mathf.Abs(originalClipData[i]) > 0.001f)
            {
                startSample = i;
                break;
            }
        }

        originalClipData = originalClipData.Skip(startSample).ToArray();
        hasProcessedOperations = true;
        ApplyVolumePreview();
    }

    private void TrimSilenceEnd()
    {
        int channels = selectedClip.channels;
        int endSample = originalClipData.Length;
        for (int i = originalClipData.Length - 1; i >= 0; i -= channels)
        {
            if (Mathf.Abs(originalClipData[i]) > 0.001f)
            {
                endSample = i + 1;
                break;
            }
        }

        originalClipData = originalClipData.Take(endSample).ToArray();
        hasProcessedOperations = true;

        ApplyVolumePreview();
    }

    private void TrimToMarkers()
    {
        int sampleStart = Mathf.FloorToInt(markerStart * selectedClip.frequency) * selectedClip.channels;
        int sampleEnd = Mathf.FloorToInt(markerEnd * selectedClip.frequency) * selectedClip.channels;
        int newLength = Mathf.Max(0, sampleEnd - sampleStart);

        originalClipData = originalClipData.Skip(sampleStart).Take(newLength).ToArray();
        hasProcessedOperations = true; // ✅ Also flag here

        ApplyVolumePreview();
    }

    private void PlayPreview()
    {
        StopPreview();
        if (selectedClip == null || previewClipData == null) return;

        int sampleStart = Mathf.FloorToInt(markerStart * selectedClip.frequency) * selectedClip.channels;
        int sampleEnd = Mathf.FloorToInt(markerEnd * selectedClip.frequency) * selectedClip.channels;
        int sampleLength = Mathf.Max(0, sampleEnd - sampleStart);

        if (sampleLength <= 0 || sampleStart >= previewClipData.Length) return;

        float[] clipSegment = previewClipData.Skip(sampleStart).Take(sampleLength).ToArray();

        AudioClip tempClip = AudioClip.Create("Preview", clipSegment.Length / selectedClip.channels, selectedClip.channels, selectedClip.frequency, false);
        tempClip.SetData(clipSegment, 0);

        GameObject go = new GameObject("AudioPreview");
        previewSource = go.AddComponent<AudioSource>();
        previewSource.clip = tempClip;
        previewSource.Play();
    }


    private void PausePreview()
    {
        if (previewSource != null && previewSource.isPlaying)
            previewSource.Pause();
    }

    private void StopPreview()
    {
        if (previewSource != null)
        {
            previewSource.Stop();
            DestroyImmediate(previewSource.gameObject);
            previewSource = null;
        }
    }

    private void SaveAsNewClip()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Edited Clip",
            selectedClip.name + "_edited",
            "wav",
            "Save edited audio clip"
        );

        if (string.IsNullOrEmpty(path)) return;

        ApplySaveChanges();

        int sampleCount = previewClipData.Length / selectedClip.channels;
        var tempClip = AudioClip.Create("TempClip", sampleCount, selectedClip.channels, selectedClip.frequency, false);
        tempClip.SetData(previewClipData, 0);

        //byte[] wavData = WavUtility.FromAudioClip(tempClip, out _, false);
        
       // byte[] wavData = WavUtility.FromFloatArray(previewClipData, selectedClip.channels, selectedClip.frequency, out _, false);

       // Create a new AudioClip from the trimmed previewClipData
       int trimmedSamples = previewClipData.Length / selectedClip.channels;
       AudioClip trimmedClip = AudioClip.Create(
           "TrimmedClip", trimmedSamples, selectedClip.channels, selectedClip.frequency, false);
       trimmedClip.SetData(previewClipData, 0);

       string paths = "";
// Now save using the trimmedClip instead of the original full clip
       byte[] wavBytes = WavUtility.FromAudioClip(trimmedClip, out paths);


       if (wavBytes == null || wavBytes.Length == 0)
       {
           Debug.LogError("WAV export failed: data was null or empty.");
           return;
       }


       File.WriteAllBytes(path, wavBytes);

        // 👇 Refresh and import instead of using CreateAsset
        AssetDatabase.ImportAsset(path);
        AssetDatabase.Refresh();

        Debug.Log("Saved and imported new audio clip: " + path);
    }

}
