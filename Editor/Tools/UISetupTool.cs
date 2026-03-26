using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI設定専用MCPツール
/// </summary>
[McpServerToolType, Description("Setup UI components")]
internal sealed class UISetupTool
{
    [McpServerTool, Description("Set Button's TargetGraphic to its Image component. Automatically finds Image on the same GameObject.")]
    public async ValueTask<string> SetButtonTargetGraphic(
        [Description("Path to the Button GameObject (e.g., 'ForestUI/Panel/PlayButton')")]
        string buttonPath)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            GameObject buttonObj = InspectorTool.GameObjectResolver.Resolve(buttonPath);
            Button button = buttonObj.GetComponent<Button>();
            if (button == null)
            {
                return $"Error: No Button component found on '{buttonPath}'.";
            }

            Image image = buttonObj.GetComponent<Image>();
            if (image == null)
            {
                return $"Error: No Image component found on '{buttonPath}'.";
            }

            Undo.RecordObject(button, "Set Button TargetGraphic");
            button.targetGraphic = image;
            EditorUtility.SetDirty(button);

            return $"Successfully set TargetGraphic for Button on '{buttonPath}' to its Image component.";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            return $"Error: {e.Message}";
        }
    }

    [McpServerTool, Description("Add text label to a Button. Creates child GameObject with Text component, centers it, and sets the text.")]
    public async ValueTask<string> AddButtonText(
        [Description("Path to the Button GameObject (e.g., 'ForestUI/Panel/PlayButton')")]
        string buttonPath,
        [Description("Text to display on the button (e.g., 'Play')")]
        string text,
        [Description("Font size (default: 14)")]
        int fontSize = 14)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            GameObject buttonObj = InspectorTool.GameObjectResolver.Resolve(buttonPath);

            // Create child GameObject for text
            GameObject textObj = new GameObject($"{buttonObj.name}Text");
            textObj.transform.SetParent(buttonObj.transform, false);

            // Add RectTransform
            RectTransform rectTransform = textObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;

            // Add Text component
            Text textComponent = textObj.AddComponent<Text>();
            textComponent.text = text;
            textComponent.fontSize = fontSize;
            textComponent.alignment = TextAnchor.MiddleCenter;
            textComponent.color = Color.black;

            // Set default font (LegacyRuntime)
            textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Undo.RegisterCreatedObjectUndo(textObj, $"Add Button Text '{text}'");
            EditorUtility.SetDirty(buttonObj);

            return $"Successfully added text '{text}' to Button on '{buttonPath}'.";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            return $"Error: {e.Message}";
        }
    }

    [McpServerTool, Description("Create a legacy UI Button with default settings. Deletes existing GameObject at path if exists. Uses Unity's default Button sprite and colors.")]
    public async ValueTask<string> CreateLegacyButton(
        [Description("Parent path (e.g., 'ForestUI/Panel')")]
        string parentPath,
        [Description("Button name (e.g., 'PlayButton')")]
        string buttonName,
        [Description("Button text (e.g., 'Play')")]
        string buttonText,
        [Description("Position X")]
        float posX,
        [Description("Position Y")]
        float posY,
        [Description("Width")]
        float width = 160,
        [Description("Height")]
        float height = 30)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            GameObject parent = InspectorTool.GameObjectResolver.Resolve(parentPath);

            // Delete existing GameObject with same name
            string fullPath = $"{parentPath}/{buttonName}";
            try
            {
                GameObject existing = InspectorTool.GameObjectResolver.Resolve(fullPath);
                Undo.DestroyObjectImmediate(existing);
            }
            catch
            {
                // No existing object, continue
            }

            // Create Button GameObject
            GameObject buttonObj = new GameObject(buttonName);
            buttonObj.transform.SetParent(parent.transform, false);

            RectTransform buttonRect = buttonObj.AddComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0, 1);
            buttonRect.anchorMax = new Vector2(0, 1);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = new Vector2(width, height);
            buttonRect.anchoredPosition = new Vector2(posX, posY);

            // Add Image with Unity default sprite
            Image image = buttonObj.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = Color.white;

            // Add Button with default ColorBlock
            Button button = buttonObj.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = ColorBlock.defaultColorBlock;
            button.colors = colors;

            // Create Text child
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;

            Text textComponent = textObj.AddComponent<Text>();
            textComponent.text = buttonText;
            textComponent.fontSize = 14;
            textComponent.alignment = TextAnchor.MiddleCenter;
            textComponent.color = Color.black;
            textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Undo.RegisterCreatedObjectUndo(buttonObj, $"Create Legacy Button '{buttonName}'");
            EditorUtility.SetDirty(parent);

            return $"Successfully created legacy Button '{buttonName}' at '{fullPath}' with text '{buttonText}'.";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            return $"Error: {e.Message}";
        }
    }

    [McpServerTool, Description("Update text of a Button's Text child component.")]
    public async ValueTask<string> UpdateButtonText(
        [Description("Path to the Button GameObject (e.g., 'Canvas/Panel/PlayButton')")]
        string buttonPath,
        [Description("New text to display (e.g., '再生開始')")]
        string newText)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            GameObject buttonObj = InspectorTool.GameObjectResolver.Resolve(buttonPath);
            Text textComponent = buttonObj.GetComponentInChildren<Text>();

            if (textComponent == null)
            {
                return $"Error: No Text component found in children of '{buttonPath}'.";
            }

            Undo.RecordObject(textComponent, "Update Button Text");
            textComponent.text = newText;
            EditorUtility.SetDirty(textComponent);

            return $"Successfully updated text to '{newText}' on '{buttonPath}'.";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            return $"Error: {e.Message}";
        }
    }
}
