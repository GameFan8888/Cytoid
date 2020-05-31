using System;
using DG.Tweening;
using Newtonsoft.Json;
using UniRx.Async;
using UnityEngine;
using UnityEngine.UI;

public class GamePreparationScreen : Screen
{
    public const string Id = "GamePreparation";
    public const bool PrintDebugMessages = true;
    
    public AudioSource previewAudioSource;

    [GetComponentInChildrenName] public DepthCover cover;
    public Text bestPerformanceDescriptionText;
    public PerformanceWidget bestPerformanceWidget;

    public CircleButton startButton;

    public ActionTabs actionTabs;
    public RankingsTab rankingsTab;
    public RatingTab ratingTab;

    public RectTransform ownerRoot;
    public Avatar ownerAvatar;
    public Text ownerName;

    public GameObject gameplayIcon;
    public GameObject settingsIcon;
    public GameObject rankingsIcon;
    public GameObject ratingIcon;

    public RadioGroup practiceModeToggle;
    
    public Transform settingsTabContent;
    public CalibratePreferenceElement calibratePreferenceElement;
    public Transform generalSettingsHolder;
    public Transform gameplaySettingsHolder;
    public Transform visualSettingsHolder;
    public Transform advancedSettingsHolder;
    
    private bool initializedSettingsTab;
    
    public Level Level { get; set; }

    public override string GetId() => Id;

    private bool willCalibrate;

    public override void OnScreenInitialized()
    {
        base.OnScreenInitialized();
        actionTabs.onTabChanged.AddListener(async (prev, next) => 
        {
            if (!initializedSettingsTab && next.index == 3)
            {
                SpinnerOverlay.Show();
                await UniTask.DelayFrame(5);
                InitializeSettingsTab();
                await UniTask.DelayFrame(5);
                SpinnerOverlay.Hide();
            }
        });

        var lp = Context.Player;
        practiceModeToggle.Select((!lp.Settings.PlayRanked).BoolToString(), false);
        practiceModeToggle.onSelect.AddListener(it =>
        {
            var ranked = !bool.Parse(it);
            lp.Settings.PlayRanked = ranked;
            lp.SaveSettings();
            LoadLevelPerformance();
            UpdateStartButton();
        });

        startButton.interactableMonoBehavior.onPointerClick.AddListener(_ => OnStartButton());
        
        Context.LevelManager.OnLevelMetaUpdated.AddListener(OnLevelMetaUpdated);
        Context.OnlinePlayer.OnLevelBestPerformanceUpdated.AddListener(OnLevelBestPerformanceUpdated);
    }

    public override void OnScreenBecameActive()
    {
        base.OnScreenBecameActive();
        
        if (Context.SelectedLevel == null)
        {
            Debug.LogWarning("Context.SelectedLevel is null");
            return;
        }

        ProfileWidget.Instance.Enter();

        var needReload = Level != Context.SelectedLevel;
        Level = Context.SelectedLevel;

        if (PrintDebugMessages)
        {
            Debug.Log($"Id: {Level.Id}, Type: {Level.Type}");
            Debug.Log($"IsLocal: {Level.IsLocal}, Path: {Level.Path}");
            if (Level.OnlineLevel != null)
            {
                Debug.Log("OnlineLevel:");
                Debug.Log(JsonConvert.SerializeObject(Level.OnlineLevel));
            }
        }

        if (Context.IsOnline())
        {
            rankingsTab.UpdateRankings(Level.Id, Context.SelectedDifficulty.Id);
            ratingTab.UpdateLevelRating(Level.Id);
            
            var localVersion = Level.Meta.version;
            Context.LevelManager.FetchLevelMeta(Level.Id, true).Then(it =>
            {
                LoadOwner();
                print($"Remote version: {it.version}, local version: {localVersion}");
                if (it.version > Level.Meta.version)
                {
                    // Ask the user to update
                    var dialog = Dialog.Instantiate();
                    dialog.Message = "DIALOG_LEVEL_OUTDATED".Get();
                    dialog.UsePositiveButton = true;
                    dialog.UseNegativeButton = true;
                    dialog.OnPositiveButtonClicked = _ =>
                    {
                        DownloadAndUnpackLevel();
                        dialog.Close();
                    };
                    dialog.Open();
                }
            });
        }

        LoadLevelPerformance();
        LoadCover(needReload);
        LoadPreview(needReload);
        LoadOwner();
        
        UpdateTopMenu();
        UpdateStartButton();

        Context.OnSelectedLevelChanged.AddListener(OnSelectedLevelChanged);
    }

    private void OnSelectedLevelChanged(Level anotherLevel)
    {
        if (anotherLevel == Level) return;
        Level = anotherLevel;

        UpdateTopMenu();
        LoadPreview(true);
        LoadCover(true);
        LoadOwner();
        if (!previewAudioSource.isPlaying) LoadPreview(true);
        UpdateStartButton();
    }

    private void LoadOwner()
    {
        if (Level.Type == LevelType.Community && Level.OnlineLevel?.Owner != null)
        {
            ownerRoot.gameObject.SetActive(true);
            ownerAvatar.action = AvatarAction.ViewLevels;
            ownerAvatar.SetModel(Level.OnlineLevel.Owner);
            ownerName.text = Level.OnlineLevel.Owner.Uid;
        }
        else
        {
            ownerRoot.gameObject.SetActive(false);
            ownerAvatar.Dispose();
        }
    }

    private void UpdateTopMenu()
    {
        gameplayIcon.SetActive(Level.IsLocal);
        settingsIcon.SetActive(Level.IsLocal);
        if (Context.IsOnline())
        {
            rankingsIcon.SetActive(true);
            ratingIcon.SetActive(true);
        }
        else
        {
            rankingsIcon.SetActive(false);
            ratingIcon.SetActive(false);
        }
    }

    private void UpdateStartButton()
    {
        if (Level.IsLocal)
        {
            startButton.State = Context.Player.Settings.PlayRanked ? CircleButtonState.Start : CircleButtonState.Practice;
        }
        else
        {
            startButton.State = CircleButtonState.Download;
        }
    }

    private void OnLevelMetaUpdated(Level level)
    {
        if (level != Level) return;
        Toast.Enqueue(Toast.Status.Success, "TOAST_LEVEL_METADATA_SYNCHRONIZED".Get());
        Context.OnSelectedLevelChanged.Invoke(level);
    }
    
    private DateTime asyncCoverToken;

    public async void LoadCover(bool load)
    {
        if (load)
        {
            asyncCoverToken = DateTime.Now;
            
            string path;
            if (Level.IsLocal)
            {
                path = "file://" + Level.Path + Level.Meta.background.path;
            }
            else
            {
                path = Level.Meta.background.path.WithImageCdn().WithSizeParam(1280, 800);
            }

            var token = asyncCoverToken;

            var sprite = await Context.AssetMemory.LoadAsset<Sprite>(path, AssetTag.GameCover, allowFileCache: true);

            if (asyncCoverToken != token)
            {
                return;
            }

            if (State == ScreenState.Active)
            {
                cover.OnCoverLoaded(sprite);
            }
        }
        else
        {
            cover.OnCoverLoaded(null);
        }
    }

    private DateTime asyncPreviewToken;
    
    public async void LoadPreview(bool load)
    {
        if (load)
        {
            asyncPreviewToken = DateTime.Now;
            
            string path;
            if (Level.IsLocal)
            {
                path = "file://" + Level.Path + Level.Meta.music_preview.path;
            }
            else
            {
                path = Level.Meta.music_preview.path;
            }

            // Load
            var token = asyncPreviewToken;
            
            var audioClip = await Context.AssetMemory.LoadAsset<AudioClip>(path, AssetTag.PreviewMusic, allowFileCache: true);

            if (asyncPreviewToken != token)
            {
                return;
            }

            if (State == ScreenState.Active)
            {
                previewAudioSource.clip = audioClip;
            }
        }

        previewAudioSource.volume = 0;
        previewAudioSource.DOKill();
        previewAudioSource.DOFade(Context.Player.Settings.MusicVolume, 0.5f).SetEase(Ease.Linear);
        previewAudioSource.loop = true;
        previewAudioSource.Play();
    }

    public void LoadLevelPerformance()
    {
        var practice = !Context.Player.Settings.PlayRanked;
        bestPerformanceDescriptionText.text = (practice ? "GAME_PREP_BEST_PERFORMANCE_PRACTICE" : "GAME_PREP_BEST_PERFORMANCE").Get();
        
        var record = Level.Record;
        if (record == null)
        {
            bestPerformanceWidget.SetModel(new LevelRecord.Performance()); // 0
        }
        else
        {
            var bestPerformances = practice ? record.BestPracticePerformances : record.BestPerformances;
            bestPerformanceWidget.SetModel(bestPerformances.ContainsKey(Context.SelectedDifficulty.Id)
                ? bestPerformances[Context.SelectedDifficulty.Id]
                : new LevelRecord.Performance());
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(bestPerformanceDescriptionText.transform as RectTransform);
        
        calibratePreferenceElement.SetContent(null, null,
            () => record?.RelativeNoteOffset ?? 0f,
            offset =>
            {
                Level.Record.RelativeNoteOffset = offset;
                Level.SaveRecord();
            },
            "SETTINGS_UNIT_SECONDS".Get(),
            0.ToString());
    }

    public void OnLevelBestPerformanceUpdated(string levelId)
    {
        if (levelId != Level.Id) return;
        if (!Level.IsLocal) Level.Record = Context.Database.GetLevelRecord(levelId) ?? Level.Record;
        LoadLevelPerformance();
        Toast.Next(Toast.Status.Success, "TOAST_BEST_PERFORMANCE_SYNCHRONIZED".Get());
    }

    public override void OnScreenDestroyed()
    {
        base.OnScreenDestroyed();
        cover.image.color = Color.black;
    }

    public override void OnScreenBecameInactive()
    {
        base.OnScreenBecameInactive();
        Level = null;
        ownerRoot.gameObject.SetActive(false);
        ownerAvatar.Dispose();
        Context.LevelManager.OnLevelMetaUpdated.RemoveListener(OnLevelMetaUpdated);
        Context.OnlinePlayer.OnLevelBestPerformanceUpdated.RemoveListener(OnLevelBestPerformanceUpdated);
        Context.OnSelectedLevelChanged.RemoveListener(OnSelectedLevelChanged);

        asyncCoverToken = DateTime.Now;
        asyncPreviewToken = DateTime.Now;
        previewAudioSource.DOFade(0, 0.5f).SetEase(Ease.Linear).onComplete = () =>
        {
            previewAudioSource.Stop();
            Context.AssetMemory.DisposeTaggedCacheAssets(AssetTag.PreviewMusic);
        };
    }

    public override void OnScreenChangeFinished(Screen from, Screen to)
    {
        base.OnScreenChangeFinished(from, to);
        if (from == this && to != null)
        {
            initializedSettingsTab = false;
            if (!(to is ProfileScreen))
            {
                Context.AssetMemory.DisposeTaggedCacheAssets(AssetTag.GameCover);
            }
        }
    }

    public async void OnStartButton()
    {
        if (Level.IsLocal)
        {
            Context.SelectedGameMode = 
                willCalibrate ? GameMode.Calibration : 
                    Context.Player.Settings.PlayRanked ? GameMode.Standard : GameMode.Practice;
            
            State = ScreenState.Inactive;
            startButton.StopPulsing();

            cover.pulseElement.Pulse();
            ProfileWidget.Instance.FadeOut();
            LoopAudioPlayer.Instance.StopAudio(0.4f);

            Context.AudioManager.Get("LevelStart").Play();
            Context.SelectedMods = Context.Player.Settings.EnabledMods.ToHashSet();

            var sceneLoader = new SceneLoader("Game");
            sceneLoader.Load();

            await UniTask.Delay(TimeSpan.FromSeconds(0.8f));

            cover.mask.DOFade(1, 0.8f);

            await UniTask.Delay(TimeSpan.FromSeconds(0.8f));

            sceneLoader.Activate();
        }
        else
        {
            DownloadAndUnpackLevel();
        }
    }

    public void DownloadAndUnpackLevel()
    {
        Context.LevelManager.DownloadAndUnpackLevelDialog(
            Level,
            allowAbort: true,
            onDownloadSucceeded: () =>
            {
                if (Level.IsLocal)
                {
                    // Unload the current preview
                    Context.AssetMemory.DisposeTaggedCacheAssets(AssetTag.PreviewMusic);
                }
            },
            onDownloadAborted: () =>
            {
                Toast.Enqueue(Toast.Status.Success, "TOAST_DOWNLOAD_CANCELLED".Get());
            },
            onDownloadFailed: () =>
            {
                Toast.Next(Toast.Status.Failure, "TOAST_COULD_NOT_DOWNLOAD_LEVEL".Get());
            },
            onUnpackSucceeded: level =>
            {
                // Load with level manager and reload screen
                Level = level;
                Context.SelectedLevel = level;
                Toast.Enqueue(Toast.Status.Success, "TOAST_SUCCESSFULLY_DOWNLOADED_LEVEL".Get());

                if (Level.Type == LevelType.Community && Context.Player.ShouldOneShot("Community Level Offset Calibration"))
                {
                    Dialog.Prompt("DIALOG_TUTORIAL_OFFSET_CALIBRATION".Get());
                }
                
                UpdateTopMenu();
                LoadPreview(true);
                LoadCover(true);
                if (!previewAudioSource.isPlaying) LoadPreview(true);
                UpdateStartButton();
            },
            onUnpackFailed: () =>
            {
                Toast.Next(Toast.Status.Failure, "TOAST_COULD_NOT_UNPACK_LEVEL".Get());
            }
        );
    }

    public async void InitializeSettingsTab()
    {
        DestroySettingsTab();
        
        initializedSettingsTab = true;
        
        calibratePreferenceElement.SetContent("GAME_PREP_SETTINGS_LEVEL_NOTE_OFFSET".Get(), "GAME_PREP_SETTINGS_LEVEL_NOTE_OFFSET_DESC".Get());
        calibratePreferenceElement.calibrateButton.onPointerClick.AddListener(_ =>
        {
            willCalibrate = true;
            OnStartButton();
        });

        SettingsFactory.InstantiateGeneralSettings(generalSettingsHolder);
        SettingsFactory.InstantiateGameplaySettings(gameplaySettingsHolder);
        SettingsFactory.InstantiateVisualSettings(visualSettingsHolder);
        SettingsFactory.InstantiateAdvancedSettings(advancedSettingsHolder);

        LayoutStaticizer.Activate(settingsTabContent);
        LayoutFixer.Fix(settingsTabContent);
        await UniTask.DelayFrame(5);
        LayoutStaticizer.Staticize(settingsTabContent);
    }

    public void DestroySettingsTab()
    {
        initializedSettingsTab = false;
        foreach (Transform child in generalSettingsHolder) Destroy(child.gameObject);
        foreach (Transform child in gameplaySettingsHolder) Destroy(child.gameObject);
        foreach (Transform child in visualSettingsHolder) Destroy(child.gameObject);
        foreach (Transform child in advancedSettingsHolder) Destroy(child.gameObject);
    }

}