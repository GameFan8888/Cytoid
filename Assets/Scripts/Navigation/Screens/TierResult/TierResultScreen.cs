using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using DG.Tweening;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Proyecto26;
using UniRx.Async;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class TierResultScreen : Screen, ScreenChangeListener
{
    public const string Id = "TierResult";

    public TransitionElement lowerLeftColumn;
    public TransitionElement lowerRightColumn;
    public TransitionElement upperRightColumn;

    public TierGradientPane gradientPane;
    public TierStageResultWidget[] stageResultWidgets;
    
    public Text scoreText;
    public Text newBestText;
    public Text gradeText;
    public Text accuracyText;
    public Text maxComboText;
    public Text standardMetricText;
    public Text advancedMetricText;

    public RankingsTab rankingsTab;

    public InteractableMonoBehavior shareButton;
    public CircleButton nextButton;
    public CircleButton retryButton;
    
    private TierState tierState;
    
    public override string GetId() => Id;

    public override async void OnScreenInitialized()
    {
        base.OnScreenInitialized();
        ScreenManager.Instance.AddHandler(this);
        
        tierState = Context.TierState;
        if (tierState == null)
        {
            tierState = new TierState(MockData.Season.tiers[0])
            {
                Combo = 1,
                CurrentStageIndex = 0,
                Health = 1000.0,
                IsFailed = false,
                MaxCombo = 1,
                Stages = new[]
                {
                    new GameState()
                }
            };
            tierState.OnComplete();
        }
        Context.TierState = null;
        
        // Load translucent cover
        var image = TranslucentCover.Instance.image;
        image.color = Color.white.WithAlpha(0);
        var sprite = Context.SpriteCache.GetCachedSpriteFromMemory("game://cover");
        if (sprite == null)
        {
            var level = tierState.CurrentStage.Level;
            var path = "file://" + level.Path + level.Meta.background.path;
            using (var request = UnityWebRequestTexture.GetTexture(path))
            {
                await request.SendWebRequest();
                if (request.isNetworkError || request.isHttpError)
                {
                    Debug.LogError($"Failed to download cover from {path}");
                    Debug.LogError(request.error);
                    return;
                }

                sprite = DownloadHandlerTexture.GetContent(request).CreateSprite();
            }
        }
        image.sprite = sprite;
        image.FitSpriteAspectRatio();

        nextButton.State = CircleButtonState.Next;
        nextButton.interactableMonoBehavior.onPointerClick.AddListener(_ =>
        {
            nextButton.StopPulsing();
            Done();
        });
        retryButton.State = CircleButtonState.Retry;
        retryButton.interactableMonoBehavior.onPointerClick.AddListener(_ =>
        {
            retryButton.StopPulsing();
            RetryTier();
        });

        // Update performance info
        if (tierState.Completion < 1)
        {
            scoreText.text = "TIER_FAILED".Get();
        }
        else
        {
            scoreText.text = (Mathf.FloorToInt((float) (tierState.Completion) * 100 * 100) / 100).ToString("0.00") + "%";
        }
        accuracyText.text = "RESULT_X_ACCURACY".Get((Math.Floor(tierState.AverageAccuracy * 100 * 100) / 100).ToString("0.00"));
        if (Mathf.Approximately((float) tierState.AverageAccuracy, 1))
        {
            accuracyText.text = "RESULT_FULL_ACCURACY".Get();
        }
        maxComboText.text = "RESULT_X_COMBO".Get(tierState.MaxCombo);
        if (tierState.GradeCounts[NoteGrade.Bad] == 0 && tierState.GradeCounts[NoteGrade.Miss] == 0)
        {
            maxComboText.text = "RESULT_FULL_COMBO".Get();
        }

        var scoreGrade = ScoreGrades.FromTierCompletion(tierState.Completion);
        gradeText.text = scoreGrade.ToString();
        gradeText.GetComponent<GradientMeshEffect>().SetGradient(scoreGrade.GetGradient());
        if (scoreGrade == ScoreGrade.MAX || scoreGrade == ScoreGrade.SSS)
        {
            scoreText.GetComponent<GradientMeshEffect>().SetGradient(scoreGrade.GetGradient());
        }
        else
        {
            scoreText.GetComponent<GradientMeshEffect>().enabled = false;
        }

        standardMetricText.text = $"<b>Perfect</b> {tierState.GradeCounts[NoteGrade.Perfect]}    " +
                                  $"<b>Great</b> {tierState.GradeCounts[NoteGrade.Great]}    " +
                                  $"<b>Good</b> {tierState.GradeCounts[NoteGrade.Good]}    " +
                                  $"<b>Bad</b> {tierState.GradeCounts[NoteGrade.Bad]}    " +
                                  $"<b>Miss</b> {tierState.GradeCounts[NoteGrade.Miss]}";
        advancedMetricText.text = $"<b>Early</b> {tierState.EarlyCount}    " +
                                  $"<b>Late</b> {tierState.LateCount}    " +
                                  $"<b>{"RESULT_AVG_TIMING_ERR".Get()}</b> {tierState.AverageTimingError:0.000}s    " +
                                  $"<b>{"RESULT_STD_TIMING_ERR".Get()}</b> {tierState.StandardTimingError:0.000}s";
        if (!Context.LocalPlayer.DisplayEarlyLateIndicators) advancedMetricText.text = "";

        newBestText.text = "";
        if (tierState.Completion >= 1)
        {
            if (tierState.Tier.completion < 1)
            {
                newBestText.text = "TIER_CLEARED".Get();
            }
            else
            {
                var historicBest = tierState.Tier.completion;
                var newBest = tierState.Completion;
                if (newBest > historicBest)
                {
                    tierState.Tier.completion = tierState.Completion; // Update cached tier json
                    newBestText.text =
                        $"+{(Mathf.FloorToInt((float) (newBest - historicBest) * 100 * 100) / 100f):0.00}%";
                }
            }
        }

        shareButton.onPointerClick.AddListener(_ => StartCoroutine(Share()));

        await Resources.UnloadUnusedAssets();
    }
    
    public override void OnScreenDestroyed()
    {
        base.OnScreenDestroyed();
        Context.ScreenManager.RemoveHandler(this);
    }

    public override async void OnScreenBecameActive()
    {
        base.OnScreenBecameActive();

        TranslucentCover.Instance.image.DOFade(0.9f, 0.8f);
        
        gradientPane.SetModel(tierState.Tier);
        for (var index = 0; index < Math.Min(tierState.Tier.Meta.localStages.Count, 3); index++)
        {
            var widget = stageResultWidgets[index];
            var level = tierState.Tier.Meta.localStages[index];
            var stageResult = tierState.Stages[index];
            widget.difficultyBall.SetModel(Difficulty.Parse(level.Meta.charts[0].type), level.Meta.charts[0].difficulty);
            widget.titleText.text = level.Meta.title;
            widget.performanceWidget.SetModel(new LocalPlayer.Performance
            {
                Score = (int) stageResult.Score, Accuracy = (float) (stageResult.Accuracy * 100)
            });
        }
        
        ProfileWidget.Instance.Enter();
        upperRightColumn.Enter();

        /*if (!Context.OnlinePlayer.IsAuthenticated)
        {
            await UniTask.WaitUntil(() => Context.OnlinePlayer.IsAuthenticated);
        }*/

        UploadRecord();
    }

    private bool isSharing;

    public IEnumerator Share()
    {
        if (isSharing) yield break;
        // TODO: Refactor with ResultScreen.cs
        
        isSharing = true;
        Context.AudioManager.Get("Navigate1").Play(ignoreDsp: true);
        yield return new WaitForEndOfFrame();
        var screenshot = new Texture2D(UnityEngine.Screen.width, UnityEngine.Screen.height, TextureFormat.RGB24, false);
        screenshot.ReadPixels(new Rect(0, 0, UnityEngine.Screen.width, UnityEngine.Screen.height), 0, 0);
        screenshot.Apply();
        var tmpPath = Path.Combine(Application.temporaryCachePath, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + ".png");
        File.WriteAllBytes(tmpPath, screenshot.EncodeToPNG());
        Destroy(screenshot);
        
        // TODO
        var shareText = $"#cytoid [{tierState.Tier.Meta.name}]";

        new NativeShare()
            .AddFile(tmpPath)
            .SetText(shareText)
            .Share();

        isSharing = false;
    }

    public void Done()
    {
        LoopAudioPlayer.Instance.FadeOutLoopPlayer(0.4f);
        LoopAudioPlayer.Instance.PlayMainLoopAudio();
        Context.ScreenManager.ChangeScreen(TierSelectionScreen.Id, ScreenTransition.Out, willDestroy: true,
            onFinished: screen => Resources.UnloadUnusedAssets());
        Context.AudioManager.Get("LevelStart").Play();
    }

    public async void RetryTier()
    {
        LoopAudioPlayer.Instance.FadeOutLoopPlayer(0.4f);

        State = ScreenState.Inactive;

        ProfileWidget.Instance.FadeOut();

        Context.AudioManager.Get("LevelStart").Play();

        var sceneLoader = new SceneLoader("Game");
        sceneLoader.Load();
        await UniTask.Delay(TimeSpan.FromSeconds(0.8f));
        TranslucentCover.Instance.image.DOFade(0, 0.8f);
        await UniTask.Delay(TimeSpan.FromSeconds(0.8f));
        sceneLoader.Activate();
    }

    private void EnterControls()
    {
        lowerLeftColumn.Enter();
        lowerRightColumn.Enter();
    }

    public void UploadRecord()
    {
        // TODO
        Toast.Next(Toast.Status.Success, "TOAST_PERFORMANCE_SYNCHRONIZED".Get());
        EnterControls();
    }
    
    public void OnScreenChangeStarted(Screen from, Screen to) => Expression.Empty();

    public void OnScreenChangeFinished(Screen from, Screen to)
    {
        if (from == this)
        {
            TranslucentCover.Instance.image.color = Color.white.WithAlpha(0);
            // Dispose game cover
            Context.SpriteCache.DisposeTaggedSpritesInMemory(SpriteTag.GameCover);
        }
    }
}