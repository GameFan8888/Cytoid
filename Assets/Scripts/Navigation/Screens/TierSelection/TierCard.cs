using System;
using System.Threading;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class TierCard : MonoBehaviour
{
    [GetComponent] public RectTransform rectTransform;
    [GetComponent] public CanvasGroup canvasGroup;
    [GetComponent] public Canvas canvas;

    public CharacterBackdrop characterBackdrop;
    public TierGradientPane gradientPane;
    public TierStageCard[] stageCards;
    public GameObject lockedOverlayRoot;
    public Image lockedOverlayIcon;
    public Sprite lockedIcon;
    public Sprite unlockedIcon;
    public Text lockedOverlayText;
    public GameObject criteriaHolder;
    public CriterionEntry criterionEntryPrefab;
    
    public Image characterImage;
    public Image cardOverlayImage;
    
    public Tier Tier { get; private set; }
    public int Index { get; private set; }
    public bool IsScrollRectFix { get; set; }
    
    private Vector2 screenCenter;
    private bool active;
    private bool fadedOut;
    private CancellationTokenSource characterPreviewToken;

    private void Awake()
    {
        canvasGroup.alpha = 0;
        characterImage.SetAlpha(0);
    }

    public void ScrollCellContent(object obj) => SetModel((Tier) obj);

    public void SetModel(Tier tier)
    {
        Tier = tier;
        IsScrollRectFix = tier.isScrollRectFix;
        if (IsScrollRectFix)
        {
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(false);
            }
        }
        else
        {
            Index = tier.index;
            canvasGroup.alpha = 1f;
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(true);
            }

            active = true;
            screenCenter = this.GetScreenParent<TierSelectionScreen>().ScreenCenter;

            characterBackdrop.gameObject.SetActive(tier.data.character != null);
            if (tier.data.character != null)
            {
                LoadCharacterPreview(tier.locked
                    ? tier.data.character.silhouetteURL
                    : tier.data.character.thumbnailURL);
            }

            gradientPane.SetModel(tier);

            for (var stage = 0; stage < 3; stage++)
            {
                stageCards[stage].SetModel(
                    tier.data.localStages[stage], 
                    new ColorGradient(tier.data.colorPalette.stages[stage], 90f)
                );
            }
            
            lockedOverlayRoot.SetActive(tier.locked || !tier.StagesDownloaded);
            if (tier.locked)
            {
                lockedOverlayIcon.sprite = lockedIcon;
                lockedOverlayText.text = "Locked";
            } 
            else if (!tier.StagesDownloaded)
            {
                lockedOverlayIcon.sprite = unlockedIcon;
                lockedOverlayText.text = "Not downloaded";
            }

            foreach (Transform child in criteriaHolder.transform) Destroy(child.gameObject);
            foreach (var criterion in tier.data.criteria)
            {
                var criterionEntry = Instantiate(criterionEntryPrefab, criteriaHolder.transform);
                criterionEntry.text.text = criterion;
            }
            LayoutFixer.Fix(criteriaHolder.transform);
        }
    }

    public async void LoadCharacterPreview(string uri)
    {
        if (characterPreviewToken != null
            && !characterPreviewToken.IsCancellationRequested)
        {
            characterPreviewToken.Cancel();
            characterPreviewToken = null;
        }
        
        characterPreviewToken = new CancellationTokenSource();
        Sprite sprite;
        try
        {
            sprite = await Context.SpriteCache.CacheSpriteInMemory(
                uri,
                SpriteTag.CharacterThumbnail,
                characterPreviewToken.Token,
                useFileCache: true);
        }
        catch
        {
            characterImage.sprite = null;
            characterImage.SetAlpha(0);
            return;
        }

        if (sprite != null)
        {
            characterImage.sprite = sprite;
            characterImage.SetAlpha(1);
        }
    }

    public void ScrollCellReturn()
    {
        active = false;
        if (characterPreviewToken != null
            && !characterPreviewToken.IsCancellationRequested)
        {
            characterPreviewToken.Cancel();
            characterPreviewToken = null;
        }
    }

    public void Update()
    {
        if (!active || fadedOut) return;
        var t = Mathf.Clamp01(Math.Abs(rectTransform.GetScreenSpaceCenter().y - screenCenter.y) / 540);
        var a = 0.4f + Mathf.Lerp(0.8f, 0, t);
        cardOverlayImage.SetAlpha(1 - a);
        if (characterImage.sprite != null)
        {
            characterImage.color = Color.Lerp(Color.white, Color.black, 1 - a);
        }
        rectTransform.localScale = Vector3.one * (0.9f + Mathf.Lerp(0.1f, 0, t));
        if (t < 0.5f)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = 3;
        }
        else
        {
            canvas.overrideSorting = false;
        }
    }

    public void OnTierStart()
    {
        fadedOut = true;
        var t = Mathf.Clamp01(Math.Abs(rectTransform.GetScreenSpaceCenter().y - screenCenter.y) / 540);
        if (t < 0.5f)
        {
            DOTween.Sequence()
                .Append(rectTransform.DOScale(Vector3.one * 0.95f, 0.3f).SetEase(Ease.OutCubic))
                .Append(rectTransform.DOScale(Vector3.one, 0.3f).SetEase(Ease.InCubic));
        }
        else
        {
            cardOverlayImage.DOFade(1, 0.4f);
            if (characterImage.sprite != null)
            {
                characterImage.DOColor(Color.black, 0.4f);
            }
        }
    }
}