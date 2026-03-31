using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI要素作成専用MCPツール（Button、Slider、Text等）
/// </summary>
[McpServerToolType, Description("Create and setup UI elements (Button, Slider, Text, etc.)")]
internal sealed class UIElementCreationTool
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

    [McpServerTool, Description("Create a functional UI Slider with Background, Fill Area, and Handle. Complete Unity Slider structure.")]
    public async ValueTask<string> CreateSlider(
        [Description("Parent path (e.g., 'Canvas')")]
        string parentPath,
        [Description("Slider name (e.g., 'AgeSlider')")]
        string sliderName,
        [Description("Position X")]
        float posX,
        [Description("Position Y")]
        float posY,
        [Description("Width")]
        float width = 200,
        [Description("Height")]
        float height = 20,
        [Description("Min value (default: 0)")]
        float minValue = 0,
        [Description("Max value (default: 1)")]
        float maxValue = 1,
        [Description("Initial value (default: 0)")]
        float value = 0,
        [Description("Whole numbers only (default: false)")]
        bool wholeNumbers = false)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            GameObject parent = InspectorTool.GameObjectResolver.Resolve(parentPath);

            // Delete existing GameObject with same name
            string fullPath = $"{parentPath}/{sliderName}";
            try
            {
                GameObject existing = InspectorTool.GameObjectResolver.Resolve(fullPath);
                Undo.DestroyObjectImmediate(existing);
            }
            catch
            {
                // No existing object, continue
            }

            // Create Slider root GameObject
            GameObject sliderObj = new GameObject(sliderName);
            sliderObj.transform.SetParent(parent.transform, false);

            RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0, 1);
            sliderRect.anchorMax = new Vector2(0, 1);
            sliderRect.pivot = new Vector2(0, 1);
            sliderRect.sizeDelta = new Vector2(width, height);
            sliderRect.anchoredPosition = new Vector2(posX, posY);

            Slider slider = sliderObj.AddComponent<Slider>();

            // Create Background
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(sliderObj.transform, false);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.25f);
            bgRect.anchorMax = new Vector2(1, 0.75f);
            bgRect.sizeDelta = Vector2.zero;
            bgRect.anchoredPosition = Vector2.zero;
            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            bgImage.type = Image.Type.Sliced;
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            // Create Fill Area
            GameObject fillAreaObj = new GameObject("Fill Area");
            fillAreaObj.transform.SetParent(sliderObj.transform, false);
            RectTransform fillAreaRect = fillAreaObj.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1, 0.75f);
            fillAreaRect.sizeDelta = new Vector2(-20, 0);
            fillAreaRect.anchoredPosition = new Vector2(-10, 0);

            // Create Fill
            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(fillAreaObj.transform, false);
            RectTransform fillRect = fillObj.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;
            fillRect.anchoredPosition = Vector2.zero;
            Image fillImage = fillObj.AddComponent<Image>();
            fillImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fillImage.type = Image.Type.Sliced;
            fillImage.color = new Color(0.3f, 0.6f, 1f, 1f);

            // Create Handle Slide Area
            GameObject handleAreaObj = new GameObject("Handle Slide Area");
            handleAreaObj.transform.SetParent(sliderObj.transform, false);
            RectTransform handleAreaRect = handleAreaObj.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.sizeDelta = new Vector2(-20, 0);
            handleAreaRect.anchoredPosition = Vector2.zero;

            // Create Handle
            GameObject handleObj = new GameObject("Handle");
            handleObj.transform.SetParent(handleAreaObj.transform, false);
            RectTransform handleRect = handleObj.AddComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0, 0);
            handleRect.anchorMax = new Vector2(0, 1);
            handleRect.sizeDelta = new Vector2(20, 0);
            handleRect.anchoredPosition = Vector2.zero;
            Image handleImage = handleObj.AddComponent<Image>();
            handleImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            handleImage.color = Color.white;

            // Configure Slider component
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = minValue;
            slider.maxValue = maxValue;
            slider.wholeNumbers = wholeNumbers;
            slider.value = value;

            Undo.RegisterCreatedObjectUndo(sliderObj, $"Create Slider '{sliderName}'");
            EditorUtility.SetDirty(parent);

            return $"Successfully created Slider '{sliderName}' at '{fullPath}' (min: {minValue}, max: {maxValue}, value: {value}).";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            return $"Error: {e.Message}";
        }
    }
}
