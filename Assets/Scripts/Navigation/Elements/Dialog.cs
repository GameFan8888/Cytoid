using System;
using DG.Tweening;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UI.ProceduralImage;

[RequireComponent(typeof(CanvasGroup), typeof(TransitionElement))]
public class Dialog : MonoBehaviour
{
    public DialogUpdateEvent onUpdate = new DialogUpdateEvent();

    [GetComponent] public Canvas canvas;
    [GetComponent] public CanvasGroup canvasGroup;
    public Text messageText;
    public Transform progressHolder;
    public ProceduralImage progressImage;
    public Transform positiveButtonHolder;
    public Transform negativeButtonHolder;
    public SpinnerButton positiveButton;
    public SpinnerButton negativeButton;

    private string message;
    private bool useProgress;
    private float progress;
    private bool usePositiveButton;
    private bool useNegativeButton;

    public bool IsOpened { get; set; }

    public string Message
    {
        get => message;
        set
        {
            message = value;
            messageText.text = message;
            messageText.transform.parent.RebuildLayout();
        }
    }

    public bool UseProgress
    {
        get => useProgress;
        set
        {
            useProgress = value;
            Progress = 0f;
            progressHolder.gameObject.SetActive(value);
            progressHolder.parent.RebuildLayout();
        }
    }

    public float Progress
    {
        get => progress;
        set
        {
            progress = Mathf.Clamp(value, 0, 1);
            progressImage.rectTransform.DOWidth(progress *
                                                ((RectTransform) progressImage.transform.parent.transform).rect.width,
                0.4f).SetEase(Ease.OutCubic);
        }
    }

    public bool UsePositiveButton
    {
        get => usePositiveButton;
        set
        {
            usePositiveButton = value;
            positiveButtonHolder.gameObject.SetActive(value);
            positiveButtonHolder.transform.parent.RebuildLayout();
        }
    }

    public bool UseNegativeButton
    {
        get => useNegativeButton;
        set
        {
            useNegativeButton = value;
            negativeButtonHolder.gameObject.SetActive(value);
            negativeButtonHolder.transform.parent.RebuildLayout();
        }
    }

    public Action<Dialog> OnPositiveButtonClicked { get; set; } = (dialog) => { dialog.Close(); };
    public Action<Dialog> OnNegativeButtonClicked { get; set; } = (dialog) => { dialog.Close(); };

    protected virtual void Awake()
    {
        canvas.overrideSorting = true;
        if (SceneManager.GetActiveScene().name == "Game")
        {
            canvas.sortingLayerName = "GameOverlay3";
        }
        canvas.sortingOrder = NavigationSortingOrder.Dialog;
        canvasGroup.alpha = 1;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        positiveButton.spinOnClick = false;
        positiveButton.onPointerClick.AddListener(_ => OnPositiveButtonClicked(this));
        negativeButton.spinOnClick = false;
        negativeButton.onPointerClick.AddListener(_ => OnNegativeButtonClicked(this));
        progressImage.rectTransform.SetWidth(0);
    }

    protected virtual void Update()
    {
        if (IsOpened) onUpdate.Invoke(this);
    }

    public virtual void Open()
    {
        if (IsOpened) return;
        IsOpened = true;
        canvasGroup.blocksRaycasts = true;
        Context.SetMajorCanvasBlockRaycasts(false);
        GetComponentsInChildren<TransitionElement>().ForEach(it => it.UseCurrentStateAsDefault());
        GetComponentsInChildren<TransitionElement>().ForEach(it => it.Enter());
    }

    public virtual void Close(bool willDestroy = true)
    {
        if (!IsOpened) return;
        IsOpened = false;
        canvasGroup.blocksRaycasts = false;
        Context.SetMajorCanvasBlockRaycasts(true);
        GetComponentsInChildren<TransitionElement>().ForEach(it => it.Leave());
        if (willDestroy) GetComponent<TransitionElement>().onLeaveCompleted.AddListener(() => { Destroy(gameObject); });
    }

    public static Dialog Instantiate()
    {
        var dialog = Instantiate(DialogObjectProvider.Instance.dialogPrefab,
            DialogObjectProvider.Instance.dialogHolder, false);
        dialog.Message = "Hello world!";
        dialog.UsePositiveButton = true;
        dialog.UseNegativeButton = false;
        dialog.UseProgress = false;
        return dialog;
    }

    public static void PromptGoBack(string message)
    {
        var dialog = Instantiate();
        dialog.Message = message;
        dialog.OnPositiveButtonClicked = it =>
        {
            Context.ScreenManager.ChangeScreen(Context.ScreenManager.PopAndPeekHistory(), ScreenTransition.Out,
                addTargetScreenToHistory: false);
            it.Close();
        };
        dialog.Open();
    }
    
    public static void PromptUnclosable(string message, Action onPositive = default)
    {
        var dialog = Instantiate();
        dialog.Message = message;
        dialog.UsePositiveButton = onPositive != default;
        if (onPositive == default) onPositive = () => { };
        dialog.OnPositiveButtonClicked = _ => onPositive();
        dialog.UseNegativeButton = false;
        dialog.Open();
    }

    public static void PromptAlert(string message, Action onPositive = default)
    {
        var dialog = Instantiate();
        dialog.UsePositiveButton = true;
        dialog.UseNegativeButton = false;
        if (onPositive == default) onPositive = () => { };
        dialog.OnPositiveButtonClicked = _ =>
        {
            onPositive();
            dialog.Close();
        };
        dialog.Message = message;
        dialog.Open();
    }
    
    public static void Prompt(string message, Action onPositive = default, Action onNegative = default)
    {
        if (onPositive == default) onPositive = () => { };
        if (onNegative == default) onNegative = () => { };
        var dialog = Instantiate();
        dialog.UsePositiveButton = true;
        dialog.UseNegativeButton = true;
        dialog.OnPositiveButtonClicked = _ =>
        {
            onPositive();
            dialog.Close();
        };
        dialog.OnNegativeButtonClicked = _ =>
        {
            onNegative();
            dialog.Close();
        };
        dialog.Message = message;
        dialog.Open();
    }
}

public class DialogUpdateEvent : UnityEvent<Dialog>
{
}

#if UNITY_EDITOR

[CustomEditor(typeof(Dialog))]
public class DialogEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (GUILayout.Button("Sample Dialog"))
        {
            Dialog.Instantiate().Open();
        }
    }
}

#endif