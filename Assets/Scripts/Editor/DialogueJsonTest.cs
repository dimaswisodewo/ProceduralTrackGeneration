using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public static class DialogueJsonTest {
    [MenuItem("Tools/Test Dialogue JSON Parser")]
    public static void TestParser() {
        string path = "Assets/Resources/dialogue_example.json";
        if (!File.Exists(path)) {
            Debug.LogError($"[DialogueJsonTest] Example JSON file not found at: {path}");
            return;
        }

        string jsonText = File.ReadAllText(path);
        Debug.Log("[DialogueJsonTest] Loading raw JSON content:\n" + jsonText);

        List<JsonDialogueEntry> entries = VisualNovelDialogueManager.ParseDialogueJson(jsonText);
        if (entries == null || entries.Count == 0) {
            Debug.LogError("[DialogueJsonTest] Failed to parse JSON or no entries found.");
            return;
        }

        Debug.Log($"[DialogueJsonTest] Parsed {entries.Count} dialogue entries successfully:");
        for (int i = 0; i < entries.Count; i++) {
            var entry = entries[i];
            Debug.Log($"Entry {i + 1}:\n" +
                      $"- Speaker Type: {entry.speakerType}\n" +
                      $"- Speaker Name: {entry.speakerName ?? "null"}\n" +
                      $"- Position: {entry.position}\n" +
                      $"- Primary Text: \"{entry.primaryText}\"\n" +
                      $"- Translation Text: \"{entry.translationText ?? "null"}\"\n" +
                      $"- Asset Name: {entry.assetName ?? "null"}");
        }
    }
}
