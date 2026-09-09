using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TeamProject01.Gameplay
{
    public sealed class CardShowcaseDirector : MonoBehaviour
    {
        private const string CardUiPrefabReferencesResourcePath = "LevelCard/CardUiPrefabReferences";
        private const string TierFrameNormalResourcePath = "LevelCard/TierFrames/CardFrame_Normal";
        private const string TierFrameRareResourcePath = "LevelCard/TierFrames/CardFrame_Rare";
        private const string TierFrameUniqueResourcePath = "LevelCard/TierFrames/CardFrame_Unique";
        private const string DescriptionValueToken = "(N)";
        private const string DescriptionValueColor = "#2F6BFF";

#if UNITY_EDITOR
        private const string StatCatalogAssetPath = "Assets/Resources/LevelCard/StatUpgradeCatalog.asset";
        private const string CardReferenceAssetPath = "Assets/Resources/LevelCard/CardUiPrefabReferences.asset";
        private const string SegmentCatalogAssetPath = "Assets/Segments/_Catalog/SegmentCatalog.asset";
        private const string WeaponCatalogAssetPath = "Assets/Segments/_Catalog/CardSegment/WeaponEnhancementCatalog.asset";
        private const string StatCardPrefabPath = "Assets/Prefabs/LevelCard/Stat Upgrade/StatUpgradeCard.prefab";
        private const string SegmentCardPrefabPath = "Assets/Prefabs/LevelCard/Stat Upgrade/SegmentUpgradeCard.prefab";
        private const string SegmentChoiceCardPrefabPath = "Assets/Prefabs/LevelCard/Stat Upgrade/PF_SegmentChoiceCard.prefab";
        private const string RewardChoiceCardPrefabPath = "Assets/Prefabs/LevelCard/Stat Upgrade/PF_RewardChoiceCard.prefab";
#endif

        [Header("Catalogs")]
        public CardUiPrefabReferences PrefabReferences;
        public StatUpgradeCatalogAsset StatUpgradeCatalog;
        public SegmentCatalogAsset SegmentCatalog;
        public WeaponCatalogAsset WeaponCatalog;

        [Header("Templates")]
        public GameObject StatCardTemplate;
        public GameObject WeaponCardTemplate;
        public GameObject SegmentChoiceCardTemplate;
        public GameObject RewardChoiceCardTemplate;

        [Header("UI Roots")]
        public Canvas TargetCanvas;
        public RectTransform ViewportRoot;
        public RectTransform ContentRoot;

        [Header("Layout")]
        [Min(1)] public int CardsPerPage = 24;
        [Min(1)] public int Columns = 8;
        [Min(1)] public int Rows = 3;
        public Vector2 PageSize = new Vector2(1920f, 1080f);
        public Vector2 PagePadding = new Vector2(60f, 52f);
        public Vector2 CardSize = new Vector2(336f, 504f);
        [Min(0f)] public float PageGap = 80f;
        [Range(0.2f, 1.5f)] public float CardScale = 0.52f;
        [Min(0f)] public float HeaderHeight = 48f;
        public bool RenderSectionHeaders = true;

        [Header("Content")]
        public bool IncludeStatUpgradeCards = true;
        public bool IncludeWeaponEnhancementCards = true;
        public bool IncludeSegmentChoiceCards = true;
        public bool IncludeRewardChoiceCards = true;

        [Header("Runtime")]
        public bool BuildOnStart = true;
        public bool AutoScrollOnStart = true;
        public bool RebuildWithR = true;
        public bool ReplayScrollWithF = true;
        [Min(0.1f)] public float AutoScrollSeconds = 30f;
        [Min(0f)] public float AutoScrollDelay = 0.25f;
        public bool LoopAutoScroll;
        public bool UseUnscaledTime = true;

        private readonly List<ShowcaseCardEntry> cardBuffer = new List<ShowcaseCardEntry>(256);
        private readonly List<ShowcasePage> pageBuffer = new List<ShowcasePage>(32);
        private readonly List<StatUpgradeDefinition> statDefinitionBuffer = new List<StatUpgradeDefinition>(32);
        private readonly List<WeaponDefinition> weaponDefinitionBuffer = new List<WeaponDefinition>(128);
        private readonly List<SegmentDefinition> segmentDefinitionBuffer = new List<SegmentDefinition>(32);

        private Sprite cachedTierFrameNormalSprite;
        private Sprite cachedTierFrameRareSprite;
        private Sprite cachedTierFrameUniqueSprite;
        private Coroutine scrollRoutine;
        private int builtPageCount;

        private enum ShowcaseCardKind
        {
            StatUpgrade,
            WeaponEnhancement,
            SegmentChoice,
            RewardChoice
        }

        private enum RewardShowcaseKind
        {
            Gold,
            Experience,
            SegmentChoiceTicket
        }

        private sealed class ShowcaseCardEntry
        {
            public ShowcaseCardKind Kind;
            public StatUpgrade.StatCardTier Tier;
            public string Title;
            public string Description;
            public Sprite Icon;
            public float IconSizeOffset;
            public StatUpgradeDefinition StatDefinition;
            public WeaponDefinition WeaponDefinition;
            public SegmentDefinition SegmentDefinition;
            public RewardShowcaseKind RewardKind;
        }

        private sealed class ShowcasePage
        {
            public StatUpgrade.StatCardTier Tier;
            public int TierPageIndex;
            public int TierPageCount;
            public readonly List<ShowcaseCardEntry> Entries = new List<ShowcaseCardEntry>(24);
        }

        private void Start()
        {
            ResolveAssets();
            if (BuildOnStart)
            {
                BuildShowcase();
            }

            if (AutoScrollOnStart)
            {
                PlayScroll();
            }
        }

        private void Update()
        {
            if (RebuildWithR && WasPressedR())
            {
                BuildShowcase();
                if (AutoScrollOnStart)
                {
                    PlayScroll();
                }
            }

            if (ReplayScrollWithF && WasPressedF())
            {
                PlayScroll();
            }
        }

        public void BuildShowcase()
        {
            StopScroll();
            NormalizeSettings();
            ResolveAssets();
            EnsureUiRoots();
            ClearRoot(ContentRoot);
            BuildCardEntries(cardBuffer);
            BuildPages(cardBuffer, pageBuffer);
            builtPageCount = pageBuffer.Count;
            ConfigureContentSize(builtPageCount);

            for (int i = 0; i < pageBuffer.Count; i++)
            {
                CreatePage(pageBuffer[i], i);
            }

            SetScrollPosition(0f);
        }

        public void PlayScroll()
        {
            StopScroll();
            if (ContentRoot == null || builtPageCount <= 0)
            {
                return;
            }

            scrollRoutine = StartCoroutine(ScrollRoutine());
        }

        private IEnumerator ScrollRoutine()
        {
            if (AutoScrollDelay > 0f)
            {
                yield return WaitForSeconds(AutoScrollDelay);
            }

            do
            {
                float duration = Mathf.Max(0.1f, AutoScrollSeconds);
                float timer = 0f;
                while (timer < duration)
                {
                    timer += ReadDeltaTime();
                    float t = Mathf.Clamp01(timer / duration);
                    float eased = t * t * (3f - 2f * t);
                    SetScrollPosition(eased);
                    yield return null;
                }

                SetScrollPosition(1f);

                if (LoopAutoScroll)
                {
                    yield return null;
                    SetScrollPosition(0f);
                }
            }
            while (LoopAutoScroll);

            scrollRoutine = null;
        }

        private void StopScroll()
        {
            if (scrollRoutine == null)
            {
                return;
            }

            StopCoroutine(scrollRoutine);
            scrollRoutine = null;
        }

        private void SetScrollPosition(float normalized)
        {
            if (ContentRoot == null)
            {
                return;
            }

            float maxScroll = Mathf.Max(0f, builtPageCount - 1) * (PageSize.x + PageGap);
            ContentRoot.anchoredPosition = new Vector2(-maxScroll * Mathf.Clamp01(normalized), 0f);
        }

        private void BuildCardEntries(List<ShowcaseCardEntry> results)
        {
            results.Clear();
            AddTierEntries(results, StatUpgrade.StatCardTier.Normal);
            AddTierEntries(results, StatUpgrade.StatCardTier.Rare);
            AddTierEntries(results, StatUpgrade.StatCardTier.Unique);
        }

        private void AddTierEntries(List<ShowcaseCardEntry> results, StatUpgrade.StatCardTier tier)
        {
            if (IncludeStatUpgradeCards)
            {
                ResolveStatDefinitions(statDefinitionBuffer);
                for (int i = 0; i < statDefinitionBuffer.Count; i++)
                {
                    AddStatEntry(results, statDefinitionBuffer[i], tier);
                }
            }

            if (tier == StatUpgrade.StatCardTier.Normal && IncludeSegmentChoiceCards)
            {
                ResolveSegmentDefinitions(segmentDefinitionBuffer);
                for (int i = 0; i < segmentDefinitionBuffer.Count; i++)
                {
                    AddSegmentChoiceEntry(results, segmentDefinitionBuffer[i], tier);
                }
            }

            if (IncludeWeaponEnhancementCards)
            {
                ResolveWeaponDefinitions(weaponDefinitionBuffer);
                for (int i = 0; i < weaponDefinitionBuffer.Count; i++)
                {
                    AddWeaponEntry(results, weaponDefinitionBuffer[i], tier);
                }
            }

            if (IncludeRewardChoiceCards)
            {
                AddRewardEntry(results, RewardShowcaseKind.Gold, tier);
                AddRewardEntry(results, RewardShowcaseKind.Experience, tier);
                AddRewardEntry(results, RewardShowcaseKind.SegmentChoiceTicket, tier);
            }
        }

        private void AddStatEntry(List<ShowcaseCardEntry> results, StatUpgradeDefinition definition, StatUpgrade.StatCardTier tier)
        {
            if (definition == null)
            {
                return;
            }

            string title = string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.NormalizedId : definition.DisplayName;
            results.Add(new ShowcaseCardEntry
            {
                Kind = ShowcaseCardKind.StatUpgrade,
                Tier = tier,
                Title = title,
                Description = BuildStatDescription(definition, tier),
                Icon = definition.CardIconSprite,
                IconSizeOffset = definition.CardIconSizeOffset,
                StatDefinition = definition
            });
        }

        private void AddWeaponEntry(List<ShowcaseCardEntry> results, WeaponDefinition definition, StatUpgrade.StatCardTier tier)
        {
            if (definition == null)
            {
                return;
            }

            string title = string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.NormalizedId : definition.DisplayName;
            results.Add(new ShowcaseCardEntry
            {
                Kind = ShowcaseCardKind.WeaponEnhancement,
                Tier = tier,
                Title = title,
                Description = BuildWeaponDescription(definition, tier),
                Icon = definition.GetIconSpriteForLevel(1),
                IconSizeOffset = definition.CardIconSizeOffset,
                WeaponDefinition = definition
            });
        }

        private void AddSegmentChoiceEntry(List<ShowcaseCardEntry> results, SegmentDefinition definition, StatUpgrade.StatCardTier tier)
        {
            if (definition == null || !definition.HasId || definition.StarterOnly || !definition.CanAddByLevelChoice)
            {
                return;
            }

            string title = string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.NormalizedId : definition.DisplayName;
            string description = string.IsNullOrWhiteSpace(definition.Description)
                ? $"{definition.NormalizedId} 선택"
                : definition.Description;
            results.Add(new ShowcaseCardEntry
            {
                Kind = ShowcaseCardKind.SegmentChoice,
                Tier = tier,
                Title = title,
                Description = description,
                Icon = definition.GetIconSpriteForLevel(1),
                IconSizeOffset = definition.CardIconSizeOffset,
                SegmentDefinition = definition
            });
        }

        private void AddRewardEntry(List<ShowcaseCardEntry> results, RewardShowcaseKind rewardKind, StatUpgrade.StatCardTier tier)
        {
            int multiplier = Mathf.Max(1, Mathf.RoundToInt(StatUpgrade.GetTierMultiplier(tier)));
            results.Add(new ShowcaseCardEntry
            {
                Kind = ShowcaseCardKind.RewardChoice,
                Tier = tier,
                Title = ResolveRewardTitle(rewardKind),
                Description = ResolveRewardDescription(rewardKind, multiplier),
                Icon = ResolveRewardIcon(rewardKind),
                RewardKind = rewardKind
            });
        }

        private void BuildPages(List<ShowcaseCardEntry> cards, List<ShowcasePage> pages)
        {
            pages.Clear();
            AddTierPages(cards, pages, StatUpgrade.StatCardTier.Normal);
            AddTierPages(cards, pages, StatUpgrade.StatCardTier.Rare);
            AddTierPages(cards, pages, StatUpgrade.StatCardTier.Unique);
        }

        private void AddTierPages(List<ShowcaseCardEntry> cards, List<ShowcasePage> pages, StatUpgrade.StatCardTier tier)
        {
            int tierCount = CountTierEntries(cards, tier);
            if (tierCount <= 0)
            {
                return;
            }

            int perPage = Mathf.Max(1, CardsPerPage);
            int tierPageCount = Mathf.CeilToInt((float)tierCount / perPage);
            int localPageIndex = 0;
            ShowcasePage currentPage = null;
            for (int i = 0; i < cards.Count; i++)
            {
                ShowcaseCardEntry entry = cards[i];
                if (entry == null || entry.Tier != tier)
                {
                    continue;
                }

                if (currentPage == null || currentPage.Entries.Count >= perPage)
                {
                    currentPage = new ShowcasePage
                    {
                        Tier = tier,
                        TierPageIndex = localPageIndex,
                        TierPageCount = tierPageCount
                    };
                    pages.Add(currentPage);
                    localPageIndex++;
                }

                currentPage.Entries.Add(entry);
            }
        }

        private static int CountTierEntries(List<ShowcaseCardEntry> cards, StatUpgrade.StatCardTier tier)
        {
            int count = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null && cards[i].Tier == tier)
                {
                    count++;
                }
            }

            return count;
        }

        private void CreatePage(ShowcasePage page, int pageIndex)
        {
            GameObject pageObject = new GameObject($"CardShowcasePage_{pageIndex + 1:00}_{page.Tier}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            pageObject.transform.SetParent(ContentRoot, false);

            RectTransform pageRect = pageObject.GetComponent<RectTransform>();
            pageRect.anchorMin = new Vector2(0f, 0.5f);
            pageRect.anchorMax = new Vector2(0f, 0.5f);
            pageRect.pivot = new Vector2(0f, 0.5f);
            pageRect.sizeDelta = PageSize;
            pageRect.anchoredPosition = new Vector2(pageIndex * (PageSize.x + PageGap), 0f);
            pageRect.localScale = Vector3.one;

            Image background = pageObject.GetComponent<Image>();
            background.color = ResolvePageTint(page.Tier);
            background.raycastTarget = false;

            if (RenderSectionHeaders)
            {
                CreateHeader(pageRect, page);
            }

            for (int i = 0; i < page.Entries.Count; i++)
            {
                CreateCard(pageRect, page.Entries[i], i);
            }
        }

        private void CreateHeader(RectTransform pageRect, ShowcasePage page)
        {
            GameObject headerObject = new GameObject("GradeHeader", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            headerObject.transform.SetParent(pageRect, false);

            RectTransform rect = headerObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(PagePadding.x, -44f);
            rect.sizeDelta = new Vector2(680f, HeaderHeight);
            rect.localScale = Vector3.one;

            TextMeshProUGUI text = headerObject.GetComponent<TextMeshProUGUI>();
            text.text = $"{ResolveTierLabel(page.Tier)}  {page.TierPageIndex + 1}/{page.TierPageCount}";
            text.fontSize = 42f;
            text.enableAutoSizing = true;
            text.fontSizeMax = 42f;
            text.fontSizeMin = 24f;
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.color = ResolveHeaderColor(page.Tier);
            text.raycastTarget = false;
        }

        private void CreateCard(RectTransform pageRect, ShowcaseCardEntry entry, int index)
        {
            GameObject template = ResolveTemplate(entry);
            if (template == null)
            {
                return;
            }

            GameObject cardObject = Instantiate(template, pageRect);
            cardObject.name = $"Card_{index + 1:00}_{entry.Kind}_{entry.Tier}_{SanitizeName(entry.Title)}";
            cardObject.SetActive(true);

            RectTransform rect = cardObject.GetComponent<RectTransform>();
            if (rect == null)
            {
                rect = cardObject.AddComponent<RectTransform>();
            }

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = CardSize;
            rect.anchoredPosition = ResolveCardPosition(index);
            rect.localScale = Vector3.one * CardScale;
            rect.localRotation = Quaternion.identity;

            ApplyTierFrame(cardObject, entry.Tier);
            ConfigureCard(cardObject, entry);
            HideTooltipRoots(cardObject);
            ForceOpaqueCard(cardObject);
            DisableInteraction(cardObject);
        }

        private Vector2 ResolveCardPosition(int index)
        {
            int columns = Mathf.Max(1, Columns);
            int rows = Mathf.Max(1, Rows);
            int row = Mathf.Clamp(index / columns, 0, rows - 1);
            int column = index % columns;

            Vector2 displayedCardSize = ResolveDisplayedCardSize();
            float usableWidth = Mathf.Max(displayedCardSize.x * columns, PageSize.x - PagePadding.x * 2f);
            float usableHeight = Mathf.Max(displayedCardSize.y * rows, PageSize.y - PagePadding.y * 2f - HeaderHeight);
            float cellWidth = usableWidth / columns;
            float cellHeight = usableHeight / rows;
            float x = -usableWidth * 0.5f + cellWidth * (column + 0.5f);
            float y = usableHeight * 0.5f - cellHeight * (row + 0.5f) - HeaderHeight * 0.32f;
            return new Vector2(x, y);
        }

        private Vector2 ResolveDisplayedCardSize()
        {
            float scale = Mathf.Max(0.01f, CardScale);
            return new Vector2(CardSize.x * scale, CardSize.y * scale);
        }

        private void ConfigureCard(GameObject root, ShowcaseCardEntry entry)
        {
            switch (entry.Kind)
            {
                case ShowcaseCardKind.StatUpgrade:
                    ConfigureStatCard(root, entry);
                    break;
                case ShowcaseCardKind.WeaponEnhancement:
                    ConfigureWeaponCard(root, entry);
                    break;
                case ShowcaseCardKind.SegmentChoice:
                    ConfigureSegmentChoiceCard(root, entry);
                    break;
                case ShowcaseCardKind.RewardChoice:
                    ApplyCardTextAndIcon(root, entry.Title, entry.Description, entry.Icon, entry.IconSizeOffset);
                    break;
            }
        }

        private void ConfigureStatCard(GameObject root, ShowcaseCardEntry entry)
        {
            StatUpgrade statUpgrade = root.GetComponent<StatUpgrade>();
            if (statUpgrade != null && entry.StatDefinition != null)
            {
                statUpgrade.ConfigureFromDefinition(entry.StatDefinition, entry.Tier);
            }

            ApplyCardTextAndIcon(root, entry.Title, entry.Description, entry.Icon, entry.IconSizeOffset);
        }

        private void ConfigureWeaponCard(GameObject root, ShowcaseCardEntry entry)
        {
            SegmentAddCard card = root.GetComponent<SegmentAddCard>();
            if (card != null && entry.WeaponDefinition != null)
            {
                card.ApplyWeaponEnhancementTier(entry.Tier);
                card.ConfigureWeaponEnhancement(entry.WeaponDefinition, 1, entry.Description);
            }

            ApplyCardTextAndIcon(root, entry.Title, entry.Description, entry.Icon, entry.IconSizeOffset);
        }

        private void ConfigureSegmentChoiceCard(GameObject root, ShowcaseCardEntry entry)
        {
            SegmentAddCard card = root.GetComponent<SegmentAddCard>();
            if (card != null && entry.SegmentDefinition != null)
            {
                card.ConfigureCandidate(entry.SegmentDefinition.ToCatalogEntry());
            }

            ApplyCardTextAndIcon(root, entry.Title, entry.Description, entry.Icon, entry.IconSizeOffset);
        }

        private void ApplyCardTextAndIcon(GameObject root, string title, string description, Sprite icon, float iconSizeOffset)
        {
            if (root == null)
            {
                return;
            }

            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
            TMP_Text titleText = null;
            TMP_Text descriptionText = null;
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i].gameObject.name == "Card_Text")
                {
                    titleText = texts[i];
                }
                else if (texts[i].gameObject.name == "DescText")
                {
                    descriptionText = texts[i];
                }
            }

            if (titleText != null)
            {
                ConfigureText(titleText, 24f, false);
                titleText.text = title ?? string.Empty;
            }

            if (descriptionText != null)
            {
                string displayDescription = SegmentCardTagPresenter.Apply(root, description ?? string.Empty, descriptionText);
                ConfigureText(descriptionText, CountDescriptionLines(displayDescription) >= 3 ? 17f : 20f, true);
                descriptionText.richText = true;
                descriptionText.text = displayDescription;
            }

            ApplyIcon(root, icon, iconSizeOffset);
        }

        private static void ConfigureText(TMP_Text text, float maxFontSize, bool allowWrap)
        {
            if (text == null)
            {
                return;
            }

            text.enableAutoSizing = true;
            float templateFontSize = text.fontSize > 0f ? text.fontSize : maxFontSize;
            float resolvedMaxFontSize = Mathf.Min(maxFontSize, templateFontSize);
            text.fontSizeMax = resolvedMaxFontSize;
            text.fontSizeMin = Mathf.Max(8f, resolvedMaxFontSize * 0.55f);
            text.fontSize = resolvedMaxFontSize;
            text.textWrappingMode = allowWrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
        }

        private static void ApplyIcon(GameObject root, Sprite icon, float iconSizeOffset)
        {
            Transform imageTransform = root.transform.Find("Image");
            if (imageTransform == null || !imageTransform.TryGetComponent(out Image image))
            {
                return;
            }

            if (icon == null)
            {
                image.sprite = null;
                image.overrideSprite = null;
                image.enabled = false;
                return;
            }

            Vector2 slotSize = ResolveCardIconSlotSize(image.rectTransform);
            image.sprite = icon;
            image.overrideSprite = null;
            image.enabled = true;
            image.color = Color.white;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.rectTransform.sizeDelta = ApplyIconSizeOffset(slotSize, iconSizeOffset);
        }

        private static Vector2 ResolveCardIconSlotSize(RectTransform iconRect)
        {
            if (iconRect == null)
            {
                return new Vector2(130f, 130f);
            }

            Vector2 slotSize = iconRect.sizeDelta;
            if (slotSize.sqrMagnitude <= 0.0001f)
            {
                return new Vector2(130f, 130f);
            }

            return new Vector2(Mathf.Max(Mathf.Abs(slotSize.x), 130f), Mathf.Max(Mathf.Abs(slotSize.y), 130f));
        }

        private static Vector2 ApplyIconSizeOffset(Vector2 slotSize, float offset)
        {
            float positiveOffset = Mathf.Max(0f, Mathf.Clamp(offset, -100f, 100f));
            if (Mathf.Approximately(positiveOffset, 0f))
            {
                return slotSize;
            }

            return slotSize * (1f + positiveOffset / 100f);
        }

        private void ApplyTierFrame(GameObject root, StatUpgrade.StatCardTier tier)
        {
            if (root == null)
            {
                return;
            }

            Sprite frame = ResolveTierFrameSprite(tier);
            if (frame == null)
            {
                return;
            }

            Image image = root.GetComponent<Image>();
            if (image == null)
            {
                return;
            }

            image.sprite = frame;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = Color.white;
        }

        private Sprite ResolveTierFrameSprite(StatUpgrade.StatCardTier tier)
        {
            switch (tier)
            {
                case StatUpgrade.StatCardTier.Unique:
                    if (cachedTierFrameUniqueSprite == null)
                    {
                        cachedTierFrameUniqueSprite = Resources.Load<Sprite>(TierFrameUniqueResourcePath);
                    }
                    return cachedTierFrameUniqueSprite;
                case StatUpgrade.StatCardTier.Rare:
                    if (cachedTierFrameRareSprite == null)
                    {
                        cachedTierFrameRareSprite = Resources.Load<Sprite>(TierFrameRareResourcePath);
                    }
                    return cachedTierFrameRareSprite;
                default:
                    if (cachedTierFrameNormalSprite == null)
                    {
                        cachedTierFrameNormalSprite = Resources.Load<Sprite>(TierFrameNormalResourcePath);
                    }
                    return cachedTierFrameNormalSprite;
            }
        }

        private void ResolveStatDefinitions(List<StatUpgradeDefinition> results)
        {
            results.Clear();
            if (StatUpgradeCatalog != null)
            {
                StatUpgradeCatalog.AppendValidDefinitions(results);
            }

#if UNITY_EDITOR
            if (results.Count == 0)
            {
                string[] guids = AssetDatabase.FindAssets("t:StatUpgradeDefinition", new[] { "Assets/Resources/LevelCard/StatUpgrades" });
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    StatUpgradeDefinition definition = AssetDatabase.LoadAssetAtPath<StatUpgradeDefinition>(path);
                    if (definition != null && definition.HasAnyStatValue && !results.Contains(definition))
                    {
                        results.Add(definition);
                    }
                }
            }
#endif

            results.Sort((a, b) => string.Compare(GetSortName(a), GetSortName(b), System.StringComparison.OrdinalIgnoreCase));
        }

        private void ResolveWeaponDefinitions(List<WeaponDefinition> results)
        {
            results.Clear();
            if (WeaponCatalog != null)
            {
                WeaponCatalog.AppendAllEnhancements(results);
            }

#if UNITY_EDITOR
            if (results.Count == 0)
            {
                string[] guids = AssetDatabase.FindAssets("t:WeaponDefinition", new[] { "Assets/Segments/_Weapon" });
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    WeaponDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
                    if (definition != null && definition.HasAnyStatBonus && definition.HasTarget && !results.Contains(definition))
                    {
                        results.Add(definition);
                    }
                }
            }
#endif

            results.Sort((a, b) =>
            {
                int segmentCompare = string.Compare(a != null ? a.NormalizedTargetSegmentId : string.Empty, b != null ? b.NormalizedTargetSegmentId : string.Empty, System.StringComparison.OrdinalIgnoreCase);
                return segmentCompare != 0 ? segmentCompare : string.Compare(GetSortName(a), GetSortName(b), System.StringComparison.OrdinalIgnoreCase);
            });
        }

        private void ResolveSegmentDefinitions(List<SegmentDefinition> results)
        {
            results.Clear();
            if (SegmentCatalog != null && SegmentCatalog.Segments != null)
            {
                for (int i = 0; i < SegmentCatalog.Segments.Length; i++)
                {
                    SegmentDefinition definition = SegmentCatalog.Segments[i];
                    if (definition != null && definition.HasId && !definition.StarterOnly && definition.CanAddByLevelChoice && !results.Contains(definition))
                    {
                        results.Add(definition);
                    }
                }
            }

#if UNITY_EDITOR
            if (results.Count == 0)
            {
                string[] guids = AssetDatabase.FindAssets("t:SegmentDefinition", new[] { "Assets/Segments" });
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    SegmentDefinition definition = AssetDatabase.LoadAssetAtPath<SegmentDefinition>(path);
                    if (definition != null && definition.HasId && !definition.StarterOnly && definition.CanAddByLevelChoice && !results.Contains(definition))
                    {
                        results.Add(definition);
                    }
                }
            }
#endif

            results.Sort((a, b) => string.Compare(a != null ? a.NormalizedId : string.Empty, b != null ? b.NormalizedId : string.Empty, System.StringComparison.OrdinalIgnoreCase));
        }

        private void ResolveAssets()
        {
            if (PrefabReferences == null)
            {
                PrefabReferences = Resources.Load<CardUiPrefabReferences>(CardUiPrefabReferencesResourcePath);
            }

            if (StatUpgradeCatalog == null && PrefabReferences != null)
            {
                StatUpgradeCatalog = PrefabReferences.StatUpgradeCatalog;
            }

            if (StatCardTemplate == null && StatUpgradeCatalog != null)
            {
                StatCardTemplate = StatUpgradeCatalog.DefaultCardPrefab;
            }

            if (SegmentChoiceCardTemplate == null && PrefabReferences != null)
            {
                SegmentChoiceCardTemplate = PrefabReferences.SegmentChoiceCardPrefab;
            }

            if (RewardChoiceCardTemplate == null && PrefabReferences != null)
            {
                RewardChoiceCardTemplate = PrefabReferences.RewardChoiceCardPrefab;
            }

#if UNITY_EDITOR
            if (PrefabReferences == null)
            {
                PrefabReferences = AssetDatabase.LoadAssetAtPath<CardUiPrefabReferences>(CardReferenceAssetPath);
            }

            if (StatUpgradeCatalog == null)
            {
                StatUpgradeCatalog = AssetDatabase.LoadAssetAtPath<StatUpgradeCatalogAsset>(StatCatalogAssetPath);
            }

            if (SegmentCatalog == null)
            {
                SegmentCatalog = AssetDatabase.LoadAssetAtPath<SegmentCatalogAsset>(SegmentCatalogAssetPath);
            }

            if (WeaponCatalog == null)
            {
                WeaponCatalog = AssetDatabase.LoadAssetAtPath<WeaponCatalogAsset>(WeaponCatalogAssetPath);
            }

            if (StatCardTemplate == null)
            {
                StatCardTemplate = AssetDatabase.LoadAssetAtPath<GameObject>(StatCardPrefabPath);
            }

            if (WeaponCardTemplate == null)
            {
                WeaponCardTemplate = AssetDatabase.LoadAssetAtPath<GameObject>(SegmentCardPrefabPath);
            }

            if (SegmentChoiceCardTemplate == null)
            {
                SegmentChoiceCardTemplate = AssetDatabase.LoadAssetAtPath<GameObject>(SegmentChoiceCardPrefabPath);
            }

            if (RewardChoiceCardTemplate == null)
            {
                RewardChoiceCardTemplate = AssetDatabase.LoadAssetAtPath<GameObject>(RewardChoiceCardPrefabPath);
            }
#endif
        }

        private GameObject ResolveTemplate(ShowcaseCardEntry entry)
        {
            if (entry == null)
            {
                return null;
            }

            switch (entry.Kind)
            {
                case ShowcaseCardKind.StatUpgrade:
                    return entry.StatDefinition != null && entry.StatDefinition.CardPrefabOverride != null
                        ? entry.StatDefinition.CardPrefabOverride
                        : StatCardTemplate;
                case ShowcaseCardKind.WeaponEnhancement:
                    return entry.WeaponDefinition != null && entry.WeaponDefinition.CardPrefabOverride != null
                        ? entry.WeaponDefinition.CardPrefabOverride
                        : WeaponCardTemplate;
                case ShowcaseCardKind.SegmentChoice:
                    return SegmentChoiceCardTemplate != null ? SegmentChoiceCardTemplate : WeaponCardTemplate;
                case ShowcaseCardKind.RewardChoice:
                    return RewardChoiceCardTemplate != null ? RewardChoiceCardTemplate : StatCardTemplate;
                default:
                    return null;
            }
        }

        private void EnsureUiRoots()
        {
            if (TargetCanvas == null)
            {
                TargetCanvas = FindFirstObjectByType<Canvas>();
            }

            if (TargetCanvas == null)
            {
                GameObject canvasObject = new GameObject("CardShowcaseCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                TargetCanvas = canvasObject.GetComponent<Canvas>();
                TargetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            CanvasScaler scaler = TargetCanvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = TargetCanvas.gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = PageSize;
            scaler.matchWidthOrHeight = 0.5f;

            if (TargetCanvas.GetComponent<GraphicRaycaster>() == null)
            {
                TargetCanvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            if (ViewportRoot == null)
            {
                ViewportRoot = CreateRectRoot("CardShowcaseViewport", TargetCanvas.transform);
            }

            ViewportRoot.anchorMin = Vector2.zero;
            ViewportRoot.anchorMax = Vector2.one;
            ViewportRoot.pivot = new Vector2(0.5f, 0.5f);
            ViewportRoot.offsetMin = Vector2.zero;
            ViewportRoot.offsetMax = Vector2.zero;
            ViewportRoot.localScale = Vector3.one;

            Image viewportBackground = ViewportRoot.GetComponent<Image>();
            if (viewportBackground == null)
            {
                viewportBackground = ViewportRoot.gameObject.AddComponent<Image>();
            }

            viewportBackground.color = new Color(0.035f, 0.034f, 0.041f, 1f);
            viewportBackground.raycastTarget = false;

            if (ContentRoot == null)
            {
                ContentRoot = CreateRectRoot("CardShowcaseContent", ViewportRoot);
            }

            ContentRoot.anchorMin = new Vector2(0f, 0.5f);
            ContentRoot.anchorMax = new Vector2(0f, 0.5f);
            ContentRoot.pivot = new Vector2(0f, 0.5f);
            ContentRoot.localScale = Vector3.one;
            ContentRoot.localRotation = Quaternion.identity;
        }

        private static RectTransform CreateRectRoot(string objectName, Transform parent)
        {
            GameObject rootObject = new GameObject(objectName, typeof(RectTransform));
            rootObject.transform.SetParent(parent, false);
            return rootObject.GetComponent<RectTransform>();
        }

        private void ConfigureContentSize(int pageCount)
        {
            if (ContentRoot == null)
            {
                return;
            }

            float width = Mathf.Max(PageSize.x, pageCount * PageSize.x + Mathf.Max(0, pageCount - 1) * PageGap);
            ContentRoot.sizeDelta = new Vector2(width, PageSize.y);
        }

        private void NormalizeSettings()
        {
            Columns = Mathf.Max(1, Columns);
            Rows = Mathf.Max(1, Rows);
            CardsPerPage = Mathf.Max(1, CardsPerPage);
            int gridCapacity = Columns * Rows;
            if (CardsPerPage > gridCapacity)
            {
                CardsPerPage = gridCapacity;
            }

            PageSize.x = Mathf.Max(640f, PageSize.x);
            PageSize.y = Mathf.Max(360f, PageSize.y);
            CardSize.x = Mathf.Max(120f, CardSize.x);
            CardSize.y = Mathf.Max(160f, CardSize.y);
            PagePadding.x = Mathf.Clamp(PagePadding.x, 0f, PageSize.x * 0.35f);
            PagePadding.y = Mathf.Clamp(PagePadding.y, 0f, PageSize.y * 0.35f);
        }

        private string BuildStatDescription(StatUpgradeDefinition definition, StatUpgrade.StatCardTier tier)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            string description = string.IsNullOrWhiteSpace(definition.Description) ? definition.NormalizedId : definition.Description;
            return BuildDescriptionWithValues(description, BuildStatDescriptionValues(definition, tier));
        }

        private string BuildWeaponDescription(WeaponDefinition definition, StatUpgrade.StatCardTier tier)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            string description = string.IsNullOrWhiteSpace(definition.Description) ? definition.NormalizedId : definition.Description;
            return BuildDescriptionWithValues(description, BuildWeaponDescriptionValues(definition, tier));
        }

        private static string BuildDescriptionWithValues(string description, List<string> values)
        {
            string body = StripTierSymbols(description);
            if (string.IsNullOrWhiteSpace(body) || !body.Contains(DescriptionValueToken))
            {
                return body;
            }

            string[] lines = body.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            int valueIndex = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Contains(DescriptionValueToken))
                {
                    continue;
                }

                string valueText = valueIndex < values.Count ? values[valueIndex] : string.Empty;
                valueIndex++;
                string replacement = string.IsNullOrWhiteSpace(valueText)
                    ? string.Empty
                    : $"<color={DescriptionValueColor}><b>{valueText}</b></color>";
                lines[i] = lines[i].Replace(DescriptionValueToken, replacement);
            }

            return string.Join("\n", lines).Trim();
        }

        private static List<string> BuildStatDescriptionValues(StatUpgradeDefinition definition, StatUpgrade.StatCardTier tier)
        {
            List<string> values = new List<string>(8);
            if (definition == null)
            {
                return values;
            }

            float multiplier = StatUpgrade.GetTierMultiplier(tier);
            AddDescriptionValue(values, TryFormatPercentRate(definition.DamageMultiplierBonus * multiplier, out string damage), damage);
            AddDescriptionValue(values, TryFormatPercentRate(definition.MeleeDamageMultiplierBonus * multiplier, out string melee), melee);
            AddDescriptionValue(values, TryFormatPercentRate(definition.MagicDamageMultiplierBonus * multiplier, out string magic), magic);
            AddDescriptionValue(values, TryFormatPercentRate(definition.AttackSpeedMultiplierBonus * multiplier, out string attackSpeed), attackSpeed);
            AddDescriptionValue(values, TryFormatFloatValue(definition.TurnSpeedBonus * multiplier, out string turnSpeed), turnSpeed);
            AddDescriptionValue(values, TryFormatPercentRate(definition.CollisionForceBonus * multiplier, out string collision), collision);
            AddDescriptionValue(values, TryFormatFloatValue(definition.RejoinRangeBonus * multiplier, out string rejoin), string.IsNullOrWhiteSpace(rejoin) ? rejoin : $"{rejoin}M");
            AddDescriptionValue(values, TryFormatFloatValue(definition.NexusHealthBonus * multiplier, out string nexus), nexus);
            return values;
        }

        private static List<string> BuildWeaponDescriptionValues(WeaponDefinition definition, StatUpgrade.StatCardTier tier)
        {
            List<string> values = new List<string>(16);
            if (definition == null)
            {
                return values;
            }

            AddDescriptionValue(values, TryFormatFlatOrPercentValue(definition.GetBaseDamage(tier), definition.GetBaseDamagePercent(tier), out string baseDamage), baseDamage);
            AddDescriptionValue(values, TryFormatPercentRate(definition.GetSawPierceDamageRatio(tier), out string sawPierce), sawPierce);
            AddDescriptionValue(values, TryFormatFlatOrPercentValue(definition.GetProjectileSpeed(tier), definition.GetProjectileSpeedPercent(tier), out string projectileSpeed), projectileSpeed);
            AddDescriptionValue(values, TryFormatFlatOrPercentValue(definition.GetSearchRange(tier), definition.GetSearchRangePercent(tier), "M", out string searchRange), searchRange);
            AddDescriptionValue(values, TryFormatIntValue(definition.GetMaxChainDepth(tier), out string maxChainDepth), maxChainDepth);
            AddDescriptionValue(values, TryFormatFlatOrPercentValue(definition.GetChainRange(tier), definition.GetChainRangePercent(tier), "M", out string chainRange), chainRange);
            AddDescriptionValue(values, TryFormatPercentRate(definition.GetChainDamageFalloff(tier), out string chainFalloff), chainFalloff);
            AddDescriptionValue(values, TryFormatIntValue(definition.GetProjectileCount(tier), out string projectileCount), projectileCount);
            AddDescriptionValue(values, TryFormatPercentRate(definition.GetCooldownReduction(tier), out string cooldown), cooldown);
            AddDescriptionValue(values, TryFormatFloatValue(definition.GetSideConeAngle(tier), out string sideCone), sideCone);
            AddDescriptionValue(values, TryFormatFlatOrPercentValue(definition.GetLaserDuration(tier), definition.GetLaserDurationPercent(tier), out string laserDuration), laserDuration);
            AddDescriptionValue(values, TryFormatPercentRate(definition.GetLaserTickInterval(tier), out string laserTick), laserTick);
            AddDescriptionValue(values, TryFormatFlatOrPercentValue(definition.GetLandingRollDistance(tier), definition.GetLandingRollDistancePercent(tier), "M", out string rollDistance), rollDistance);
            AddDescriptionValue(values, TryFormatFlatOrPercentValue(definition.GetLandingRollDuration(tier), definition.GetLandingRollDurationPercent(tier), out string rollDuration), rollDuration);
            AddDescriptionValue(values, TryFormatIntValue(definition.GetPierceCount(tier), out string pierceCount), pierceCount);
            AddDescriptionValue(values, TryFormatFlatOrPercentValue(definition.GetExplosionRadius(tier), definition.GetExplosionRadiusPercent(tier), "M", out string explosionRadius), explosionRadius);
            return values;
        }

        private static void AddDescriptionValue(List<string> values, bool active, string valueText)
        {
            if (active)
            {
                values.Add(valueText);
            }
        }

        private static bool TryFormatFlatOrPercentValue(float flatValue, float percentValue, out string valueText)
        {
            return TryFormatFlatOrPercentValue(flatValue, percentValue, string.Empty, out valueText);
        }

        private static bool TryFormatFlatOrPercentValue(float flatValue, float percentValue, string flatSuffix, out string valueText)
        {
            if (TryFormatPercentRate(percentValue, out valueText))
            {
                return true;
            }

            if (!TryFormatFloatValue(flatValue, out valueText))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(flatSuffix))
            {
                valueText += flatSuffix;
            }

            return true;
        }

        private static bool TryFormatFloatValue(float value, out string valueText)
        {
            valueText = string.Empty;
            if (Mathf.Abs(value) <= 0.0001f)
            {
                return false;
            }

            valueText = value.ToString("0.###", CultureInfo.InvariantCulture);
            return true;
        }

        private static bool TryFormatIntValue(int value, out string valueText)
        {
            valueText = string.Empty;
            if (value == 0)
            {
                return false;
            }

            valueText = value.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        private static bool TryFormatPercentRate(float value, out string valueText)
        {
            valueText = string.Empty;
            if (value <= 0.0001f)
            {
                return false;
            }

            valueText = $"{(value * 100f).ToString("0.#", CultureInfo.InvariantCulture)}%";
            return true;
        }

        private static string StripTierSymbols(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string trimmed = text.Trim();
            int index = 0;
            while (index < trimmed.Length && (trimmed[index] == '+' || trimmed[index] == '-'))
            {
                index++;
            }

            string body = trimmed;
            if (index > 0 && index < trimmed.Length && char.IsWhiteSpace(trimmed[index]))
            {
                body = trimmed.Substring(index).TrimStart();
            }

            int symbolStart = body.Length;
            while (symbolStart > 0 && (body[symbolStart - 1] == '+' || body[symbolStart - 1] == '-'))
            {
                symbolStart--;
            }

            if (symbolStart < body.Length && (symbolStart == 0 || char.IsWhiteSpace(body[symbolStart - 1])))
            {
                body = body.Substring(0, symbolStart).TrimEnd();
            }

            return body;
        }

        private string ResolveRewardTitle(RewardShowcaseKind kind)
        {
            switch (kind)
            {
                case RewardShowcaseKind.Gold:
                    return "골드";
                case RewardShowcaseKind.Experience:
                    return "경험치";
                case RewardShowcaseKind.SegmentChoiceTicket:
                    return "세그먼트 선택";
                default:
                    return string.Empty;
            }
        }

        private string ResolveRewardDescription(RewardShowcaseKind kind, int multiplier)
        {
            switch (kind)
            {
                case RewardShowcaseKind.Gold:
                    return $"골드 +{100 * multiplier}";
                case RewardShowcaseKind.Experience:
                    return $"경험치 +{200 * multiplier}";
                case RewardShowcaseKind.SegmentChoiceTicket:
                    return $"선택권 x{Mathf.Max(1, multiplier)}";
                default:
                    return string.Empty;
            }
        }

        private Sprite ResolveRewardIcon(RewardShowcaseKind kind)
        {
            if (PrefabReferences == null)
            {
                return null;
            }

            switch (kind)
            {
                case RewardShowcaseKind.Gold:
                    return PrefabReferences.RewardGoldIconSprite;
                case RewardShowcaseKind.Experience:
                    return PrefabReferences.RewardExperienceIconSprite;
                case RewardShowcaseKind.SegmentChoiceTicket:
                    return PrefabReferences.RewardSegmentChoiceTicketIconSprite;
                default:
                    return null;
            }
        }

        private static int CountDescriptionLines(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return 0;
            }

            return Mathf.Max(1, description.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Length);
        }

        private static string GetSortName(StatUpgradeDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.NormalizedId : definition.DisplayName;
        }

        private static string GetSortName(WeaponDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.NormalizedId : definition.DisplayName;
        }

        private static string ResolveTierLabel(StatUpgrade.StatCardTier tier)
        {
            switch (tier)
            {
                case StatUpgrade.StatCardTier.Unique:
                    return "UNIQUE";
                case StatUpgrade.StatCardTier.Rare:
                    return "RARE";
                default:
                    return "NORMAL";
            }
        }

        private static Color ResolveHeaderColor(StatUpgrade.StatCardTier tier)
        {
            switch (tier)
            {
                case StatUpgrade.StatCardTier.Unique:
                    return new Color(0.52f, 1f, 0.62f, 1f);
                case StatUpgrade.StatCardTier.Rare:
                    return new Color(1f, 0.79f, 0.29f, 1f);
                default:
                    return new Color(0.88f, 0.92f, 1f, 1f);
            }
        }

        private static Color ResolvePageTint(StatUpgrade.StatCardTier tier)
        {
            switch (tier)
            {
                case StatUpgrade.StatCardTier.Unique:
                    return new Color(0.03f, 0.07f, 0.045f, 0.82f);
                case StatUpgrade.StatCardTier.Rare:
                    return new Color(0.08f, 0.064f, 0.026f, 0.82f);
                default:
                    return new Color(0.04f, 0.044f, 0.055f, 0.82f);
            }
        }

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Untitled";
            }

            StringBuilder builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                builder.Append(char.IsLetterOrDigit(c) ? c : '_');
            }

            return builder.ToString();
        }

        private static void DisableInteraction(GameObject root)
        {
            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i].interactable = false;
            }

            Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                graphics[i].raycastTarget = false;
            }
        }

        private static void ForceOpaqueCard(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            CanvasGroup[] groups = root.GetComponentsInChildren<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                if (!groups[i].gameObject.activeInHierarchy)
                {
                    continue;
                }

                groups[i].alpha = 1f;
                groups[i].interactable = false;
                groups[i].blocksRaycasts = false;
            }

            Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (!graphics[i].gameObject.activeInHierarchy)
                {
                    continue;
                }

                Color color = graphics[i].color;
                color.a = 1f;
                graphics[i].color = color;
            }
        }

        private static void HideTooltipRoots(GameObject root)
        {
            HideChild(root, "CardTooltip");
            HideChild(root, "DpsTooltip");
        }

        private static void HideChild(GameObject root, string childName)
        {
            if (root == null)
            {
                return;
            }

            Transform child = root.transform.Find(childName);
            if (child != null)
            {
                child.gameObject.SetActive(false);
            }
        }

        private static void ClearRoot(Transform root)
        {
            if (root == null)
            {
                return;
            }

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                GameObject child = root.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }

        private float ReadDeltaTime()
        {
            return UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        }

        private object WaitForSeconds(float seconds)
        {
            return UseUnscaledTime ? new WaitForSecondsRealtime(seconds) : new WaitForSeconds(seconds);
        }

        private static bool WasPressedR()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.rKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.R);
#endif
        }

        private static bool WasPressedF()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.fKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.F);
#endif
        }
    }
}
