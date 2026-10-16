# 界面与UI

> 共 106 个类型。仅列 `public` / `protected` 成员。


## AdvButtonElement

`class AdvButtonElement : SelectableElement`

**字段/属性**

```csharp
public string onClickAudio;
```

**属性**

```csharp
public string text { get; set; }
public bool hasText { get; }
public Sprite spriteNormal { get; set; }
public Sprite spriteHighlight { get; set; }
public Sprite spriteClicked { get; set; }
public Sprite spriteDisabled { get; set; }
public string description { get; }
```

**方法**

```csharp
public string get_text()
public bool get_hasText()
public Sprite get_spriteNormal()
public Sprite get_spriteHighlight()
public Sprite get_spriteClicked()
public Sprite get_spriteDisabled()
public string get_description()
public void .ctor(Sprite normalSprite, string text, int width = -1, int height = -1, Action callback, string onClickAudio = "interface_click")
public AdvButtonElement Set(string text, int width = -1, int height = -1)
public AdvButtonElement SetCallback(Action callback)
public override void Validate()
public override void ResizePixels(int width, int height)
protected override void OnDefocus()
protected override void OnHover()
protected override void OnPress()
protected override void OnSelect()
protected override void OnDeselect()
protected override void OnReset()
protected override void OnDisable()
protected override void OnEnable()
protected override void OnHide()
protected override void OnShow()
public AdvButtonElement SetSprite(string atlasPath, string spriteName)
public override bool HasTooltip()
public override RichText GetTooltip()
```


## AnimatorOverrideController

`class AnimatorOverrideController : MulticastDelegate`

**方法**

```csharp
public void .ctor(object object, IntPtr method)
public virtual void Invoke()
```


## BannerLocationElement

`class BannerLocationElement : SelectableElement`

**属性**

```csharp
public string text { get; set; }
public string distText { get; set; }
public bool hasText { get; }
public bool isScanning { get; set; }
```

**方法**

```csharp
public string get_text()
public string get_distText()
public bool get_hasText()
public bool get_isScanning()
public void set_isScanning(bool value)
public BannerLocationElement SetColor(BannerLocationElement.Color color)
public void .ctor(string text, string distText, int width = -1, int height = -1, Action callback, BannerLocationElement.Color color = 0)
public BannerLocationElement Set(string text, string distText, string customSpritePath, int width = -1, int height = -1)
public BannerLocationElement SetCallback(Action callback)
public override void Validate()
public override void ResizePixels(int width, int height)
protected override void OnDefocus()
protected override void OnHover()
protected override void OnPress()
protected override void OnSelect()
protected override void OnDeselect()
protected override void OnReset()
protected override void OnDisable()
protected override void OnEnable()
protected override void OnHide()
protected override void OnShow()
public void OnScanning()
```


## BaseRuntimePanel

`class BaseRuntimePanel`

**字段/属性**

```csharp
public static readonly BaseRuntimePanel.<>c <>9;
```

**方法**

```csharp
public void .ctor()
```


## BaseVerticalCollectionView

`struct BaseVerticalCollectionView`

**字段/属性**

```csharp
public BaseVerticalCollectionView <>4__this;
public bool shiftKey;
```


## BatchRendererGroup

`class BatchRendererGroup : MulticastDelegate`

**方法**

```csharp
public void .ctor(object object, IntPtr method)
public virtual JobHandle Invoke(BatchRendererGroup rendererGroup, BatchCullingContext cullingContext)
```


## BindableElement

`class BindableElement : VisualElement.UxmlTraits`

**方法**

```csharp
public void .ctor()
public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
```


## Button

`class Button : IEnumerator<object>, IEnumerator, IDisposable`

**字段/属性**

```csharp
public Button <>4__this;
```

**方法**

```csharp
public void .ctor(int <>1__state)
```


## ButtonElement

`class ButtonElement : SelectableElement`

**字段/属性**

```csharp
public string onClickAudio;
```

**属性**

```csharp
public string text { get; set; }
public bool hasText { get; }
public string description { get; set; }
public ButtonElement.Color color { get; set; }
```

**方法**

```csharp
public string get_text()
public bool get_hasText()
public string get_description()
public void set_description(string value)
public ButtonElement.Color get_color()
public ButtonElement SetColor(ButtonElement.Color color)
public void .ctor(string text, int width = -1, int height = -1, Action callback, ButtonElement.Color color = 0, string onClickAudio = "interface_click")
public ButtonElement Set(string text, int width = -1, int height = -1)
public ButtonElement SetCallback(Action callback)
public override void Validate()
public override void ResizePixels(int width, int height)
protected override void OnDefocus()
protected override void OnHover()
protected override void OnPress()
protected override void OnSelect()
protected override void OnDeselect()
protected override void OnReset()
protected override void OnDisable()
protected override void OnEnable()
protected override void OnHide()
protected override void OnShow()
public ButtonElement SetDescription(string description)
public override bool HasTooltip()
public override RichText GetTooltip()
```


## ButtonStripField

`class ButtonStripField : BaseField.UxmlTraits<int>`

**方法**

```csharp
public void .ctor()
```


## CalendarDayElement

`class CalendarDayElement : MonoBehaviour`

**字段/属性**

```csharp
public VerticalLayoutGroup layoutGroup;
public Image backgroundImage;
public TextMeshProUGUI dayTMP;
public GameObject eventElementPrefab;
public List<CalendarEventElement> eventElements;
public bool isInit;
public int dayNumber;
public List<StoreEvent> events;
public bool isFirstOnCalendar;
public GameObject todayBackground;
```

**方法**

```csharp
public void Init()
public void SetPastDay()
public void SetToday()
public void SetFutureDay()
public void SetElement(int dayNumber, List<StoreEvent> events, CalendarDayElement previousDayElement)
public void Reset()
public void .ctor()
```


## ColorGradingRenderer

`enum ColorGradingRenderer`

**字段/属性**

```csharp
public int value__;
public const ColorGradingRenderer.Pass LutGenLDRFromScratch = 0;
public const ColorGradingRenderer.Pass LutGenLDR = 1;
public const ColorGradingRenderer.Pass LutGenHDR2D = 2;
```


## CombatEndedUIController

`class CombatEndedUIController : MonoBehaviour`

**字段/属性**

```csharp
public Label labelText;
public Button button;
```

**方法**

```csharp
public void Display(string text)
public void Hide()
public void ButtonClicked()
public void .ctor()
```


## CommissaryElement

`class CommissaryElement : MonoBehaviour`

**字段/属性**

```csharp
public TextMeshProUGUI productName;
public TextMeshProUGUI price;
public TextMeshProUGUI stock;
public Button buyButton;
public string id;
```

**方法**

```csharp
public void OnBuyClick()
public void Register()
public void .ctor()
```


## CommonTooltipHelper

`class CommonTooltipHelper`

**字段/属性**

```csharp
public static string UNIQUE_TYPE_STRING;
```

**方法**

```csharp
public static void AddUniqueTypeTooltip(GameItem gameItem, string tag)
public static void CreateItemTypeTooltip(RichTextBuilder builder, GameItem item)
```


## ConfirmPanelUIManager

`class ConfirmPanelUIManager : MonoBehaviour`

**字段/属性**

```csharp
public GameObject panel;
public TextMeshProUGUI bodyTMP;
public TextMeshProUGUI yesTMP;
public TextMeshProUGUI noTMP;
public Action yesAction;
public Action noAction;
```

**方法**

```csharp
public void OpenPanel(string body, Action yesAction, Action noAction, bool hideNoButton = False)
public void OnYesClick()
public void OnNoClick()
public void .ctor()
```


## ContactElement

`class ContactElement : MonoBehaviour`

**字段/属性**

```csharp
public TextMeshProUGUI titleTMP;
public TextMeshProUGUI statusTMP;
public long number;
```

**方法**

```csharp
public void OnQuickDial()
public void OnInit(long targetNumber)
public void .ctor()
```


## CustomUIDebugWindows

`class CustomUIDebugWindows`

**方法**

```csharp
public static void OpenSpriteDictViewer()
```


## CustomUIElement

`class CustomUIElement`

**字段/属性**

```csharp
public string id;
public GameObject root;
public TextMeshProUGUI text;
public Image image;
public Button button;
public Toggle toggle;
public Slider slider;
public TMP_InputField input;
public TMP_Dropdown dropdown;
public CustomUIProgressBar progressBar;
```

**方法**

```csharp
public static CustomUIElement Bind(GameObject go, string id)
public CustomUIElement SetText(string value)
public CustomUIElement SetSprite(Sprite sprite)
public CustomUIElement OnClick(Action callback)
public CustomUIElement SetBool(bool value, bool notify = False)
public CustomUIElement OnToggle(Action<bool> callback)
public CustomUIElement SetFloat(float value, bool notify = False)
public CustomUIElement OnSlider(Action<float> callback)
public CustomUIElement SetString(string value, bool notify = False)
public CustomUIElement OnInput(Action<string> callback)
public CustomUIElement SetIndex(int index, bool notify = False)
public CustomUIElement SetOptions(IList<string> options)
public CustomUIElement OnDropdown(Action<int> callback)
public CustomUIElement SetProgress(float normalized, string labelText)
public CustomUIElement SetTooltip(string tooltipText)
public CustomUIElement SetPreferredSize(float width, float height)
public void .ctor()
```


## CustomUITooltipTrigger

`class CustomUITooltipTrigger : MonoBehaviour, IPointerEnterHandler, IEventSystemHandler, IPointerExitHandler`

**字段/属性**

```csharp
public string tooltipText;
```

**方法**

```csharp
public void OnPointerEnter(PointerEventData eventData)
public void OnPointerExit(PointerEventData eventData)
public void .ctor()
```


## CustomUIWindow

`class CustomUIWindow`

**字段/属性**

```csharp
public string id;
public string layerId;
public GameObject root;
public CustomUIWindowFrame frame;
public bool closeOnEscape;
public Action onClose;
```

**属性**

```csharp
public bool IsAlive { get; }
public bool IsVisible { get; }
public RectTransform Rect { get; }
public RectTransform ContentRoot { get; }
```

**方法**

```csharp
public bool get_IsAlive()
public bool get_IsVisible()
public RectTransform get_Rect()
public RectTransform get_ContentRoot()
public void RegisterElement(CustomUIElement element)
public CustomUIElement Get(string elementId)
public void Show()
public void Close()
public void SetTitle(string title)
public void SetSize(float width, float height)
public void SetPosition(Vector2 anchoredPosition)
public void Center()
public void BringToFront()
public void .ctor()
```


## CustomUIWindowFrame

`class CustomUIWindowFrame : MonoBehaviour`

**字段/属性**

```csharp
public RectTransform contentRoot;
public TextMeshProUGUI titleText;
public Button closeButton;
public UIDragHandler dragHandle;
```

**方法**

```csharp
public void .ctor()
```


## DataView

`class DataView : IEqualityComparer<DataRow>`

**方法**

```csharp
public bool Equals(DataRow x, DataRow y)
public int GetHashCode(DataRow obj)
```


## DebugPanel

`class DebugPanel : MonoBehaviour`

**字段/属性**

```csharp
public GameObject debugEventPanel;
public TextMeshProUGUI currentEventTMP;
public TextMeshProUGUI cooldownEventTMP;
public GameObject debugPricePanel;
public TextMeshProUGUI priceDebugTMP;
public TextMeshProUGUI priceDebugTMP2;
public TextMeshProUGUI priceDebugTMP3;
public GameObject debugButtonPrefab;
public Transform spawnGroup;
public Transform kitGroup;
public Transform uiGroup;
public Transform cmdGroup;
public Transform marketingGroup;
public bool isQuickCustomerSpriteSwapping;
public bool isQuickAugSpriteSwapping;
```

**属性**

```csharp
public static DebugPanel Instance { get; set; }
```

**方法**

```csharp
public static DebugPanel get_Instance()
public static void ClearMainInv()
public void .ctor()
```


## DepthOfFieldRenderer

`enum DepthOfFieldRenderer`

**字段/属性**

```csharp
public int value__;
public const DepthOfFieldRenderer.Pass CoCCalculation = 0;
public const DepthOfFieldRenderer.Pass CoCTemporalFilter = 1;
public const DepthOfFieldRenderer.Pass DownsampleAndPrefilter = 2;
public const DepthOfFieldRenderer.Pass BokehSmallKernel = 3;
public const DepthOfFieldRenderer.Pass BokehMediumKernel = 4;
public const DepthOfFieldRenderer.Pass BokehLargeKernel = 5;
public const DepthOfFieldRenderer.Pass BokehVeryLargeKernel = 6;
public const DepthOfFieldRenderer.Pass PostFilter = 7;
public const DepthOfFieldRenderer.Pass Combine = 8;
public const DepthOfFieldRenderer.Pass DebugOverlay = 9;
```


## DOTweenModuleSprite

`class DOTweenModuleSprite`

**属性**

```csharp
public SpriteRenderer target; // 0x10
```

**方法**

```csharp
public void .ctor()
```


## DropdownMenuAction

`enum DropdownMenuAction`

**字段/属性**

```csharp
public int value__;
public const DropdownMenuAction.Status None = 0;
public const DropdownMenuAction.Status Normal = 1;
public const DropdownMenuAction.Status Disabled = 2;
public const DropdownMenuAction.Status Checked = 4;
public const DropdownMenuAction.Status Hidden = 8;
```


## DynamicHeightVirtualizationController

`enum DynamicHeightVirtualizationController`

**字段/属性**

```csharp
public int value__;
public const DynamicHeightVirtualizationController.ScrollDirection<T> Idle = 0;
public const DynamicHeightVirtualizationController.ScrollDirection<T> Up = 1;
public const DynamicHeightVirtualizationController.ScrollDirection<T> Down = 2;
```


## EmptyPixelElement

`class EmptyPixelElement : PixelElement`

**属性**

```csharp
public bool canResize { get; }
public int widthPixels { get; set; }
public int heightPixels { get; set; }
public TreeNodeRender handler { get; set; }
public TreeNodeRender inner { get; }
```

**方法**

```csharp
public bool get_canResize()
public int get_widthPixels()
public int get_heightPixels()
public TreeNodeRender get_handler()
public TreeNodeRender get_inner()
public void .ctor()
public void .ctor(int width, int height, bool canResize = False)
public int WidthPixelsClamp(int width)
public int HeightPixelsClamp(int height)
public void ResizePixels(int width, int height)
public void Validate()
```


## FocusController

`struct FocusController`

**字段/属性**

```csharp
public VisualElement m_SubTreeRoot;
public Focusable m_FocusedElement;
```


## GenericDropdownMenu

`struct GenericDropdownMenu`

**字段/属性**

```csharp
public GenericDropdownMenu <>4__this;
public int selectedIndex;
```


## GraphNodeWindow

`interface GraphNodeWindow : GraphNodeStorage`

**属性**

```csharp
public abstract GraphNodeStorage child { get; }
```

**方法**

```csharp
public abstract GraphNodeStorage get_child();
```


## IconProvider

`class IconProvider : MonoBehaviour`

**字段/属性**

```csharp
public Sprite apprentice;
public Sprite junker;
public Sprite waterMerchant;
public Sprite fence;
public Sprite farmer;
public Sprite gunsmith;
public Sprite chemist;
public Sprite organTrader;
public Sprite rockBottom;
public Sprite streetFoodVendor;
public Sprite rancher;
```

**属性**

```csharp
public static IconProvider Instance { get; set; }
```

**方法**

```csharp
public static IconProvider get_Instance()
public Sprite GetIcon(NewGameData.StartType startType)
public void .ctor()
```


## IndicatorElement

`class IndicatorElement : PixelElement`

**属性**

```csharp
public bool canResize { get; }
public int widthPixels { get; set; }
public int heightPixels { get; set; }
public TreeNodeRender handler { get; set; }
public TreeNodeRender inner { get; }
```

**方法**

```csharp
public bool get_canResize()
public int get_widthPixels()
public int get_heightPixels()
public TreeNodeRender get_handler()
public TreeNodeRender get_inner()
public static string ColorString(IndicatorElement.IndicatorColor currentColor, bool isLight)
public static RenderHandler.ColorPalette ColorText(IndicatorElement.IndicatorColor currentTextColor, bool isLight)
public void .ctor()
public IndicatorElement SetColor(IndicatorElement.IndicatorColor color)
public IndicatorElement Light()
public IndicatorElement Dark()
public IndicatorElement ToggleLight()
public void Validate()
public void ResizePixels(int width, int height)
```


## ItemTooltipHandler

`class ItemTooltipHandler : EmptyActionHandler`

**字段/属性**

```csharp
public static ItemTooltipHandler current;
public KeyCode key;
public Vector2 tooltipTranslation;
public const int BASE_TOOLTIP_CHAR_WIDTH = 60;
public const float MACHINE_TOOLTIP_WIDTH_MULT = 1.2;
```

**属性**

```csharp
public override List<KeyCode> keyListeners { get; }
```

**方法**

```csharp
public override List<KeyCode> get_keyListeners()
protected override void OnEventPress(ISet<KeyCode> keysPressed)
protected override void OnEventRelease(ISet<KeyCode> keysReleased)
protected override bool OnEventUpdate()
protected override void OnEventReset()
public void .ctor()
```


## LabelerElement

`class LabelerElement : MonoBehaviour`

**字段/属性**

```csharp
public TextMeshProUGUI featureName;
public TextMeshProUGUI currentFeatureDisplay;
public TMP_Dropdown dropdown;
```

**方法**

```csharp
public void InitElement(ItemFeature itemFeature)
public List<TMP_Dropdown.OptionData> CreateOptionList(string featureCategory)
public void SetCurrentlySelectedToDropdownSelection(ItemFeature itemFeature)
public string GetCategory()
public string GetSelectedConditionIdentifier()
public void OnDropdownValueChanged()
public void .ctor()
```


## ListView

`class ListView : BindableElement.UxmlTraits`

**方法**

```csharp
public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
public void .ctor()
```


## ListViewDragger

`struct ListViewDragger : IEquatable<ListViewDragger.DragPosition>`

**字段/属性**

```csharp
public int insertAtIndex;
public int parentId;
public int childIndex;
public ReusableCollectionItem recycledItem;
public DragAndDropPosition dropPosition;
```

**方法**

```csharp
public bool Equals(ListViewDragger.DragPosition other)
public override bool Equals(object obj)
public override int GetHashCode()
```


## MainMenuUIController

`class MainMenuUIController : MonoBehaviour`

**字段/属性**

```csharp
public const string DISCORD_LINK = "https:
public const string STORE_LINK = "steam:
public const string DEMO_LINK = "steam:
public const string SURVEY_LINK = "https:
public const string PRIVACY_POLICY_LINK = "https:
public const string CREATOR_LINK = "https:
public const bool IS_PLAYTEST_APP = False;
public AudioSource audioSource;
public AudioSource themeSource;
public GameObject newGamePanel;
public TextMeshProUGUI title;
public TextMeshProUGUI body;
public TextMeshProUGUI startingEquipment;
public TextMeshProUGUI note;
public GameObject comingsoon;
public TextMeshProUGUI unavailableInDemo;
public Button startGameButton;
public Image blackscreen;
public Toggle skipToggle;
public Toggle hardToggle;
public Toggle endlessToggle;
public TextMeshProUGUI versionTMP;
public GameObject creditPanel;
public GameObject welcomePanel;
public GameObject modPanel;
public GameObject skipIntroLock;
public ConfirmPanelUIManager confirmPanelUIManager;
public SaveUIManager saveUIManager;
public TMP_Dropdown localeDropdown;
public GameObject creditButton;
public GameObject discordButton;
public GameObject creatorButton;
public TextMeshProUGUI showcaseTMP;
public Sprite logoSprite;
public Sprite logolessSprite;
public SpriteRenderer logoImage;
public Sprite tab;
public Sprite tabSelected;
public Image tabWelcome;
public Image tabGeneralist;
public Image tabJunker;
public Image tabWaterMerchant;
public Image tabFence;
public Image tabFarmer;
public Image tabGunsmith;
public Image tabChemist;
public Image tabOrganTrader;
public Image tabRockBottom;
public Image tabStreetFoodVendor;
public Image tabRancher;
public Image darknessOverlay;
public Image darknessOverlay2;
public Toggle welcomePanelDataConsentToggle;
public GameObject moddingButton;
public GameObject playtestNotice;
public GameObject playtestEndedPanel;
```

**属性**

```csharp
public static MainMenuUIController Instance { get; set; }
```

**方法**

```csharp
public static MainMenuUIController get_Instance()
public void OnAnyButtonClick()
public bool IsPreviousSaveExist()
public void OnNewGameClick()
public void OnPlaytestEndedClick()
public void ClosePlaytestEndedPanel()
public void OnDemoLink()
public void OnContinueClick()
public void OpenPerkInterface()
public void OnPerkFinishedClick()
public void StartNewGameClick()
public void CloseNewGameMenu()
public void OnLoadGameClick()
public void OnOptionClick()
public void OnExitClick()
public void OpenModPanel()
public void CloseModPanel()
public void SetAvailableInDemo()
public void SetUnavailableInDemo()
public void OnWelcomeClick()
public void OnAnyStartClick()
public void UpdateDemoAvailability()
public void OnWaterMerchantClick()
public void OnGeneralistClick()
public void OnScavengerClick()
public void OnFenceClick()
public void OnMoonshinerClick()
public void OnGunsmithClick()
public void OnRockBottomClick()
public void OnDrugDealer()
public void OnOrganTrader()
public void OnPharmacist()
public void OnStreetFood()
public void OnFarmer()
public void OnRancher()
public void StartBlackscreenSequence(bool playFullSequence = True, Action actionDuringBlack, Action actionAfterBlack, float waitTime = 0.25, float sequenceTime = 0.2)
public void OnSkipIntroToggled()
public void OnHardToggled()
public void OnEndlessToggled()
public void DiscordLink()
public void OnWishlistLink()
public void OnSurveyLink()
public void OnCreatorLink()
public void OpenCreditPanel()
public void CloseCreditPanel()
public void OpenWelcomePanel()
public void CloseWelcomePanel()
public void HandleWelcomePanel()
public void ResetAllTab()
public void OnLocaleChanged(int localeIndex)
public void PreLoadLocalizationTable()
public void HandleLocaleDropdown()
public void OnPrivacyPolicyClick()
public void OnJoinDemoClick()
public void .ctor()
```


## MenuManager

`class MenuManager`

**字段/属性**

```csharp
public string selectedFaction;
public Scenario selectedScenario;
```

**方法**

```csharp
public void .ctor(MenuScene menuScene)
public void StartMenu()
public List<Scenario> GetAvailableFactionScenarios()
```


## MenuScene

`class MenuScene`

**字段/属性**

```csharp
public static int DEFAULT_PADDING;
public PixelWindow rightBarWindow;
public PixelWindow nextButtonWindow;
public PixelWindow prevButtonWindow;
public PixelWindow topBarWindow;
public RichTextElement topBarText;
public PixelWindow difficultySelectWindow;
public PixelWindow factionSelectWindow;
public GameItemElement shipElement;
public PixelWindow scenarioDescriptionWindow;
public ScrollableRichTextElement scenarioDescription;
public static int defaultShipButtonWidth;
public static int defaultShipButtonHeight;
public PixelWindow rightCharacterSelectWindow;
public PixelWindow shipWindow;
public StealPixelElement stealShipElement;
public static int defaultButtonWidth;
public static int defaultButtonHeight;
public SpritePixelElement spaceBackground;
```

**方法**

```csharp
public void .ctor()
public void Validate(bool force = True)
public void Reset()
public void StartMenu()
public void StartFactionSelect()
public void StartScenarioSelect()
public void StartCharSelect()
public void StartEquipmentSelect()
public void EndAllAnimations()
```


## ModElement

`class ModElement : MonoBehaviour, IBeginDragHandler, IEventSystemHandler, IDragHandler, IEndDragHandler`

**字段/属性**

```csharp
public TextMeshProUGUI modNameTMP;
```

**属性**

```csharp
public string ModID { get; set; }
```

**方法**

```csharp
public string get_ModID()
public void Init(string id, string name)
public void OnClick()
public void OnBeginDrag(PointerEventData eventData)
public void OnDrag(PointerEventData eventData)
public void OnEndDrag(PointerEventData eventData)
public void .ctor()
```


## MotionBlurRenderer

`enum MotionBlurRenderer`

**字段/属性**

```csharp
public int value__;
public const MotionBlurRenderer.Pass VelocitySetup = 0;
public const MotionBlurRenderer.Pass TileMax1 = 1;
public const MotionBlurRenderer.Pass TileMax2 = 2;
public const MotionBlurRenderer.Pass TileMaxV = 3;
public const MotionBlurRenderer.Pass NeighborMax = 4;
public const MotionBlurRenderer.Pass Reconstruction = 5;
```


## NetworkElement

`class NetworkElement : MonoBehaviour`

**字段/属性**

```csharp
public Button button;
public GameObject upgradeContent;
public Sprite unlockedSprite;
public Image image;
public Image icon;
public bool isUnlocked;
public string id;
public GameObject repeatableIcon;
public TextMeshProUGUI costTMP;
public GameObject checkIcon;
public GameObject padlockIcon;
public Material greyMaterial;
public Image costArea;
public Sprite costAreaNormal;
public Sprite costAreaBought;
```

**方法**

```csharp
public void OnHover()
public void OnPressed()
public void OnReleased()
public void Unlock()
public void UpdateElement()
public void OnEnable()
public void .ctor()
```


## NotebookElement

`class NotebookElement : MonoBehaviour`

**字段/属性**

```csharp
public TextMeshProUGUI tmp;
public ItemFeature itemFeature;
```

**方法**

```csharp
public void ElementClicked()
public void .ctor()
```


## PanelRaycaster

`struct PanelRaycaster`

**字段/属性**

```csharp
public float f;
public int i;
```


## PhoneButton

`class PhoneButton : MonoBehaviour`

**字段/属性**

```csharp
public int number;
```

**方法**

```csharp
public void OnPressed()
public void SimulateClick()
public void .ctor()
```


## PixelElement

`interface PixelElement`

**属性**

```csharp
public abstract bool canResize { get; }
public abstract int widthPixels { get; }
public abstract int heightPixels { get; }
public abstract TreeNodeRender handler { get; }
public abstract TreeNodeRender inner { get; }
```

**方法**

```csharp
public abstract bool get_canResize();
public abstract int get_widthPixels();
public abstract int get_heightPixels();
public abstract TreeNodeRender get_handler();
public abstract TreeNodeRender get_inner();
public abstract void ResizePixels(int width, int height);
public abstract void Validate();
public virtual bool SetRenderingParent(PixelElement parent)
public virtual bool IsRenderingChildOf(PixelElement parent)
public virtual bool RemoveRenderingParent(PixelElement parent)
```


## PixelWindow

`class PixelWindow : PixelElement, GraphNodeWindow, GraphNodeStorage`

**字段/属性**

```csharp
public readonly bool canMove;
public bool forceRaycast;
public bool isLocked;
```

**属性**

```csharp
public List<GraphNodeStorage> parents { get; }
public List<GraphNodeStorage> children { get; }
public List<GameItem> parentItems { get; set; }
public GraphNodeStorage child { get; }
public bool canResize { get; }
public int widthPixels { get; set; }
public int heightPixels { get; set; }
public TreeNodeRender handler { get; set; }
public RectTransform rectTransform { get; }
public TreeNodeRender inner { get; set; }
public PixelElement childElement { get; set; }
public int titleHeight { get; set; }
public int xPadding { get; set; }
public int yPadding { get; set; }
public bool hasTitle { get; }
public string titleString { get; set; }
public float transparency { get; set; }
public bool isImportant { get; set; }
public Action closeCallback { get; set; }
public Action openCallback { get; set; }
public long focusStamp { get; set; }
public Action stateChangeActionFunc { get; set; }
```

**方法**

```csharp
public List<GraphNodeStorage> get_parents()
public List<GraphNodeStorage> get_children()
public List<GameItem> get_parentItems()
public GraphNodeStorage get_child()
public bool get_canResize()
public int get_widthPixels()
public int get_heightPixels()
public TreeNodeRender get_handler()
public RectTransform get_rectTransform()
public TreeNodeRender get_inner()
public PixelElement get_childElement()
public int get_titleHeight()
public int get_xPadding()
public void set_xPadding(int value)
public int get_yPadding()
public void set_yPadding(int value)
public bool get_hasTitle()
public string get_titleString()
public float get_transparency()
public bool get_isImportant()
public void set_isImportant(bool value)
public Action get_closeCallback()
public void set_closeCallback(Action value)
public Action get_openCallback()
public void set_openCallback(Action value)
public void .ctor(bool canMove = False, string title)
public void .ctor(int width, int height, bool canMove, string title)
public void .ctor(int width, int height, bool canMove, int xPadding, int yPadding, string title)
public PixelWindow SetTitle(string title)
public PixelWindow SetTransparency(float transparency)
public void Validate()
public void ResizePixels(int width, int height)
public static void ReclampDockedWindows()
public void FixBounds()
public static long NextFocusStamp()
public long get_focusStamp()
public PixelWindow Show()
public PixelWindow Show(Vector2 mousePosition, Vector2 worldTranslation, bool fixTooltipBounds = False)
public PixelWindow Center()
public PixelWindow Hide()
public PixelWindow Lock()
public PixelWindow Unlock()
public bool IsVisible()
public bool Attach(PixelElement element)
public bool Detach()
public bool Detach(PixelElement element)
public void ToFront(bool force = False)
public void ToBack(bool force = False)
public bool AddParent(GraphNodeStorage node)
public bool RemoveParent(GraphNodeStorage node)
public bool MayHaveValidInventorySlot(GameItem item)
public SlotMarker TryFindOneValidInventorySlot(GameItem item)
public Action get_stateChangeActionFunc()
public PixelWindow AddInventoryStateChangeAction(Action action)
public void HideBorder()
```


## PopupWindow

`class PopupWindow : TextElement.UxmlTraits`

**方法**

```csharp
public void .ctor()
```


## PushButtonElement

`class PushButtonElement : PixelElement`

**字段/属性**

```csharp
public Action onDown;
public Action onUp;
```

**属性**

```csharp
public bool canResize { get; }
public int widthPixels { get; set; }
public int heightPixels { get; set; }
public TreeNodeRender handler { get; set; }
public TreeNodeRender inner { get; }
public string text { get; }
public bool hasText { get; }
```

**方法**

```csharp
public bool get_canResize()
public int get_widthPixels()
public int get_heightPixels()
public TreeNodeRender get_handler()
public TreeNodeRender get_inner()
public string get_text()
public bool get_hasText()
public void .ctor()
public PushButtonElement SetColor(IndicatorElement.IndicatorColor color)
public PushButtonElement Light()
public PushButtonElement Dark()
public PushButtonElement ToggleLight()
public PushButtonElement Activate()
public PushButtonElement Reset()
public PushButtonElement SetToggle(bool isToggle)
public PushButtonElement Lock()
public PushButtonElement Unlock()
public void Validate()
public void ResizePixels(int width, int height)
```


## RadioButton

`class RadioButton : BaseFieldTraits<bool, UxmlBoolAttributeDescription>`

**方法**

```csharp
public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
public void .ctor()
```


## RadioButtonGroup

`class RadioButtonGroup : BaseFieldTraits<int, UxmlIntAttributeDescription>`

**方法**

```csharp
public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
public void .ctor()
```


## RenderChain

`class RenderChain`

**字段/属性**

```csharp
public static readonly RenderChain.<>c <>9;
public static Func<RenderChainCommand> <>9__37_0;
public static Action<RenderChainCommand> <>9__37_1;
```

**方法**

```csharp
public void .ctor()
```


## RenderHandler

`class RenderHandler : MonoBehaviour`

**字段/属性**

```csharp
public static RenderHandler current;
public GameObject windowUICanvasObject;
public Canvas windowUICanvas;
public GameObject renderNodePrefab;
public GameObject renderNodeTextPrefab;
public GameObject windowRootObject;
public GameObject tempRootObject;
public GameObject overlayRootObject;
public TreeNodeRender overlayRoot;
public TreeNodeRender tempRoot;
public TreeNodeRender windowRoot;
public TreeNodeRender scenesRoot;
public Color textColorWhite;
public Color textColorRed;
public Color textColorDarkRed;
public static Sprite unknownSprite;
public Sprite healthBarOutline;
public Sprite healthBarBackground;
public Sprite healthBar;
public Sprite healthIcon;
public Sprite healthIconLow;
public Sprite moraleBarOutline;
public Sprite moraleBarBackground;
public Sprite moraleBar;
public Sprite moraleIcon;
public Sprite moraleIconLow;
public Sprite fill;
public Sprite redSquare;
public Sprite greenSquare;
public Sprite blueSquare;
public Sprite whiteSquare;
public Sprite blackSquare;
public Sprite tanSquare;
public Sprite red;
public Sprite green;
public Sprite blue;
public Sprite white;
public Sprite black;
public Sprite tan;
public Sprite redBorder;
public Sprite greenBorder;
public Sprite blueBorder;
public Sprite whiteBorder;
public Sprite blackBorder;
public Sprite tanBorder;
public Sprite inside;
public Sprite outside;
public Sprite outsideCrop;
public Sprite outsideBottomRight;
public Sprite outsideBottomLeft;
public Sprite outsideBottom;
public Sprite outsideTopRight;
public Sprite outsideTopLeft;
public Sprite outsideTop;
public Sprite outsideLeft;
public Sprite outsideRight;
public Sprite borderOutsideTopRounded;
public Sprite borderOutsideTopRoundedCrop;
public List<Color32> color32Palette;
```

**方法**

```csharp
public RectTransform GetTempTransform()
public void ResetTempTransform()
public static Sprite LoadFromAtlas(string atlasPath, string name)
public static Sprite LoadFromFile(string spritePath)
public void SetGridFillBackground(GridShape shape, int x, int y, TreeNodeRender background, TreeNodeRender r0, TreeNodeRender r1, TreeNodeRender r2, TreeNodeRender r3)
public static T RaycastFindParentElement<T>(Vector2 screenPosition, TreeNodeRender ignore)
public static T RaycastElement<T>(Vector2 screenPosition, TreeNodeRender ignore)
public static string GetStringFromColorPalette(RenderHandler.ColorPalette color)
public static Color32 GetColorFromPalette(RenderHandler.ColorPalette color)
public static string ApplyRichTextColorTag(string text, RenderHandler.ColorPalette color)
public void SetHighlightColor(Image image, RenderHandler.BackgroundColor color, RenderHandler.BackgroundType type)
public void .ctor()
```


## RenderTargetUtility

`struct RenderTargetUtility`

**字段/属性**

```csharp
public readonly RenderTextureDescriptor Descriptor;
public readonly FilterMode FilterMode;
```

**方法**

```csharp
public void .ctor(RenderTextureDescriptor descriptor, FilterMode filterMode)
```


## RepeatButton

`class RepeatButton : TextElement.UxmlTraits`

**方法**

```csharp
public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
public void .ctor()
```


## RTSController

`class RTSController : MonoBehaviour`

**字段/属性**

```csharp
public Vector3 worldPosition;
public static List<GameObject> teamPawnBuffer;
public static List<GameObject> selectedObjects;
public static List<GameObject> previousSelectedObjects;
public static int orderGiven;
```

**方法**

```csharp
public static Vector3 GetMousePositionGroundLevel()
public static Vector3 GetMousePositionCollider()
public static void RemoveObjectFromSelections(GameObject gameObject)
public static void UpdatePawnReceivedOrderCount()
public static bool IsDeselectOnClick()
public void .ctor()
```


## ScreenCapture

`enum ScreenCapture`

**字段/属性**

```csharp
public int value__;
public const ScreenCapture.StereoScreenCaptureMode LeftEye = 1;
public const ScreenCapture.StereoScreenCaptureMode RightEye = 2;
public const ScreenCapture.StereoScreenCaptureMode BothEyes = 3;
```


## ScreenCommands

`class ScreenCommands : IEnumerable<Resolution>, IEnumerable, IEnumerator<Resolution>, IEnumerator, IDisposable`

**方法**

```csharp
public void .ctor(int <>1__state)
```


## ScreenSpaceReflectionsRenderer

`enum ScreenSpaceReflectionsRenderer`

**字段/属性**

```csharp
public int value__;
public const ScreenSpaceReflectionsRenderer.Pass Test = 0;
public const ScreenSpaceReflectionsRenderer.Pass Resolve = 1;
public const ScreenSpaceReflectionsRenderer.Pass Reproject = 2;
public const ScreenSpaceReflectionsRenderer.Pass Composite = 3;
```


## ScrollView

`enum ScrollView`

**字段/属性**

```csharp
public int value__;
public const ScrollView.TouchScrollingResult Apply = 0;
public const ScrollView.TouchScrollingResult Forward = 1;
public const ScrollView.TouchScrollingResult Block = 2;
```


## SecurityElement

`class SecurityElement`

**方法**

```csharp
public void .ctor(string name, string value)
```


## SelectableElement

`class SelectableElement : PixelElement, TooltipListener`

**属性**

```csharp
public bool canResize { get; }
public int widthPixels { get; set; }
public int heightPixels { get; set; }
public TreeNodeRender handler { get; set; }
public TreeNodeRender inner { get; }
protected SelectableElement.PreStatus currentPreStatus { get; set; }
public bool isSelected { get; set; }
public bool requiresHold { get; }
public float holdDuration { get; }
public bool isDisabled { get; set; }
public bool isHidden { get; set; }
```

**方法**

```csharp
public bool get_canResize()
public int get_widthPixels()
protected void set_widthPixels(int value)
public int get_heightPixels()
protected void set_heightPixels(int value)
public TreeNodeRender get_handler()
public TreeNodeRender get_inner()
public void .ctor(string treeNodeName)
public abstract void Validate();
public virtual void ResizePixels(int width, int height)
protected abstract void OnDefocus();
protected abstract void OnHover();
protected abstract void OnPress();
protected abstract void OnSelect();
protected abstract void OnDeselect();
protected abstract void OnReset();
protected abstract void OnDisable();
protected abstract void OnEnable();
protected abstract void OnHide();
protected abstract void OnShow();
protected SelectableElement.PreStatus get_currentPreStatus()
public bool get_isSelected()
public bool get_requiresHold()
public float get_holdDuration()
public bool Hover()
public bool Press()
public bool Defocus()
public bool Select()
public bool Deselect()
public void Reset()
public bool get_isDisabled()
public bool get_isHidden()
public bool Disable()
public bool Enable()
public bool Hide()
public bool Show()
public virtual bool HasTooltip()
public virtual RichText GetTooltip()
public virtual RichText GetTooltipAdvanced()
```


## SelectableElementHandler

`class SelectableElementHandler : EmptyActionHandler`

**字段/属性**

```csharp
public static SelectableElementHandler current;
public KeyCode key;
```

**属性**

```csharp
public override List<KeyCode> keyListeners { get; }
```

**方法**

```csharp
public override List<KeyCode> get_keyListeners()
protected override void OnEventPress(ISet<KeyCode> keysPressed)
protected override void OnEventRelease(ISet<KeyCode> keysPressed)
protected override bool OnEventUpdate()
protected override void OnEventReset()
public void .ctor()
```


## ServiceElement

`class ServiceElement : MonoBehaviour`

**字段/属性**

```csharp
public TextMeshProUGUI titleTMP;
public Toggle toggle;
public TextMeshProUGUI costTMP;
public string serviceID;
```

**方法**

```csharp
public void OnToggled()
public void Init(string id)
public void .ctor()
```


## SimpleSpriteAnimator

`class SimpleSpriteAnimator : MonoBehaviour`

**字段/属性**

```csharp
public Sprite[] frames;
public float frameRate;
```

**方法**

```csharp
public void .ctor()
```


## SpriteAnimationHelper

`class SpriteAnimationHelper`

**方法**

```csharp
public static void SyncSpriteAnimation(GameItemElement item)
public static void Tick()
```


## SpriteAnimationManager

`class SpriteAnimationManager : MonoBehaviour`

**方法**

```csharp
public void .ctor()
```


## SpriteAsset

`class SpriteAsset`

**字段/属性**

```csharp
public static readonly SpriteAsset.<>c <>9;
public static Func<SpriteGlyph, uint> <>9__37_0;
public static Func<SpriteCharacter, uint> <>9__38_0;
```

**方法**

```csharp
public void .ctor()
```


## SpriteDict

`class SpriteDict : MonoBehaviour`

**字段/属性**

```csharp
public Dictionary<string, Sprite> spriteDictionary;
```

**属性**

```csharp
public static SpriteDict Instance { get; set; }
```

**方法**

```csharp
public static SpriteDict get_Instance()
public void ProcessEntriesToDict(List<SpriteDict.SpriteEntry> entries)
public Sprite GetSprite(string key)
public static string GetRandomUpperSpriteName()
public static string GetRandomUpperMaleSpriteName()
public static string GetRandomUpperFemaleSpriteName()
public static string GetRandomShadySpriteName()
public static string GetRandomBMMaleSpriteName()
public static string GetRandomBMFemaleSpriteName()
public static string GetRandomRevSpriteName()
public static string GetRandomRevMaleSpriteName()
public static string GetRandomRevFemaleSpriteName()
public static string GetRandomLowerLevelSpriteName()
public static string GetRandomLowerMaleSpriteName()
public static string GetRandomLowerFemaleSpriteName()
public static string GetRandomScavSpriteName()
public static string GetRandomScavMaleSpriteName()
public static string GetRandomScavFemaleSpriteName()
public static string GetRandomSecSpriteName()
public static string GetRandomSecMaleSpriteName()
public static string GetRandomSecFemaleSpriteName()
public static string GetRandomSecCorpoSpriteName()
public static string GetRandomSecCorpoMaleSpriteName()
public static string GetRandomSecCorpoFemaleSpriteName()
public static string GetRandomFarmerSpriteName()
public static string GetRandomFarmerMaleSpriteName()
public static string GetRandomFarmerFemaleSpriteName()
public static string GetRandomChefSpriteName()
public static string GetRandomChefMaleSpriteName()
public static string GetRandomChefFemaleSpriteName()
public static string GetRandomMedSpriteName()
public static string GetRandomMedMaleSpriteName()
public static string GetRandomMedFemaleSpriteName()
public static string GetRandomEngSpriteName()
public static string GetRandomEngMaleSpriteName()
public static string GetRandomEngFemaleSpriteName()
public static string GetRandomMinerSpriteName()
public static string GetRandomMercMale()
public static string GetRandomMercFemale()
public static string GetRandomMercSpriteName()
public static string GetRandomSciMaleSpriteName()
public static string GetRandomSciFemaleSpriteName()
public static string GetRandomSciSpriteName()
public static string GetRandomSecOffMaleSpriteName()
public static string GetRandomSecOffFemaleSpriteName()
public static string GetRandomSecOffSpriteName()
public static string GetRandomAugLowerMaleSpriteName()
public static string GetRandomAugLowerFemaleSpriteName()
public static string GetRandomAugLowerSpriteName()
public static string GetRandomAugBMMaleSpriteName()
public static string GetRandomAugBMFemaleSpriteName()
public static string GetRandomAugBMSpriteName()
public static string GetRandomAugUpperMaleSpriteName()
public static string GetRandomAugUpperFemaleSpriteName()
public static string GetRandomAugUpperSpriteName()
public static string GetRandomAugSpriteName()
public static string GetLoanSharkSpriteName()
public static string GetRandomScientistSpriteName()
public static string GetRandomOffDutySecSpriteName()
public static string GetRandomMerchantSpriteName()
public static string GetShadyMerchantSpriteName()
public static string GetClerkSpriteName()
public static string GetGreytideSpriteName()
public static string getRandomPilotSpriteName()
public static string GetRandomElderSpriteName()
public static string GetFoodieSpriteName()
public static string GetCaseEnjoyerSpriteName()
public static string GetAshishSpriteName()
public static string GetUniqueEleanorNormal()
public static string GetUniqueEleanorNoJewel()
public static string GetUniqueEleanorSad()
public static string GetCourrierSpriteName()
public static string GetInventorSpriteName()
public void .ctor()
```


## SpriteHelper

`class SpriteHelper`

**方法**

```csharp
public static void UpdateWaterContainerSprite(GameItem gameItem)
public static string MapCapacity(int currentCapacity)
public static void UpdateDossierSprite(GameGridInventory gridInventory, GameItem dossier)
public static void UpdateTrashcanSprite(GameGridInventory gridInventory, GameItem trashcan)
public static int GetInventoryCapacity(GameGridInventory gridInventory)
```


## SpritePixelElement

`class SpritePixelElement : PixelElement`

**字段/属性**

```csharp
public readonly TreeNodeRender background;
```

**属性**

```csharp
public bool canResize { get; }
public int widthPixels { get; set; }
public int heightPixels { get; set; }
public TreeNodeRender handler { get; set; }
public RectTransform rectTransform { get; }
public TreeNodeRender inner { get; }
```

**方法**

```csharp
public bool get_canResize()
public int get_widthPixels()
public int get_heightPixels()
public TreeNodeRender get_handler()
public RectTransform get_rectTransform()
public TreeNodeRender get_inner()
public void .ctor(string atlasPath, string spriteName)
public void .ctor(string spritePath)
public void Hide()
public void Show()
public void ToBack()
public void ResizePixels(int width, int height)
public void SetSprite(string atlasPath, string spriteName)
public void SetSprite(string spritePath)
public void SetSprite(Sprite sprite)
public void Validate()
```


## SpriteSkin

`class SpriteSkin`

**字段/属性**

```csharp
public string boneHash;
```

**方法**

```csharp
public void .ctor()
```


## SpriteSkinComposite

`class SpriteSkinComposite`

**字段/属性**

```csharp
public static readonly ProfilerMarker prepareData;
public static readonly ProfilerMarker validateSpriteSkinData;
public static readonly ProfilerMarker transformAccessJob;
public static readonly ProfilerMarker getSpriteSkinBatchData;
public static readonly ProfilerMarker resizeBuffers;
public static readonly ProfilerMarker prepare;
public static readonly ProfilerMarker scheduleJobs;
public static readonly ProfilerMarker setBatchDeformableBufferAndLocalAABB;
public static readonly ProfilerMarker deactivateDeformableBuffer;
```


## StealPixelElement

`class StealPixelElement : PixelElement, GraphNodeStorage`

**字段/属性**

```csharp
public bool centered;
```

**属性**

```csharp
public List<GraphNodeStorage> parents { get; }
public List<GraphNodeStorage> children { get; }
public bool canResize { get; }
public int widthPixels { get; set; }
public int heightPixels { get; set; }
public TreeNodeRender handler { get; set; }
public TreeNodeRender inner { get; set; }
```

**方法**

```csharp
public List<GraphNodeStorage> get_parents()
public List<GraphNodeStorage> get_children()
public bool get_canResize()
public int get_widthPixels()
public int get_heightPixels()
public TreeNodeRender get_handler()
public TreeNodeRender get_inner()
public void .ctor(bool centered = True)
public int WidthPixelsClamp(int width)
public int HeightPixelsClamp(int height)
public void ResizePixels(int width, int height)
public void Validate()
public bool StealItem(GameItem item)
public bool UnStealItem()
public bool UnStealItem(GameItem item)
public bool Attach(PixelElement element)
public bool Detach(PixelElement element)
public bool MayHaveValidInventorySlot(GameItem item)
public SlotMarker TryFindOneValidInventorySlot(GameItem item)
public bool AddParent(GraphNodeStorage node)
public bool RemoveParent(GraphNodeStorage node)
```


## SupportedRenderingFeatures

`enum SupportedRenderingFeatures`

**字段/属性**

```csharp
public int value__;
public const SupportedRenderingFeatures.LightmapMixedBakeModes None = 0;
public const SupportedRenderingFeatures.LightmapMixedBakeModes IndirectOnly = 1;
public const SupportedRenderingFeatures.LightmapMixedBakeModes Subtractive = 2;
public const SupportedRenderingFeatures.LightmapMixedBakeModes Shadowmask = 4;
```


## TMP_SpriteAnimator

`class TMP_SpriteAnimator : IEnumerator<object>, IEnumerator, IDisposable`

**字段/属性**

```csharp
public TMP_SpriteAnimator <>4__this;
public int start;
public int end;
public int currentCharacter;
public int framerate;
```

**属性**

```csharp
public TMP_SpriteAsset spriteAsset; // 0x30
```

**方法**

```csharp
public void .ctor(int <>1__state)
```


## TMP_SpriteAsset

`class TMP_SpriteAsset`

**字段/属性**

```csharp
public static readonly TMP_SpriteAsset.<>c <>9;
public static Func<TMP_SpriteGlyph, uint> <>9__40_0;
public static Func<TMP_SpriteCharacter, uint> <>9__41_0;
```

**方法**

```csharp
public void .ctor()
```


## TMPIconHover

`class TMPIconHover : MonoBehaviour, IPointerMoveHandler, IEventSystemHandler, IPointerExitHandler`

**方法**

```csharp
public void OnPointerMove(PointerEventData eventData)
public void OnPointerExit(PointerEventData eventData)
public void .ctor()
```


## TooltipContainer

`class TooltipContainer : MonoBehaviour, IPointerEnterHandler, IEventSystemHandler, IPointerExitHandler`

**字段/属性**

```csharp
public string content;
public static bool isPaused;
```

**方法**

```csharp
public void OnPointerEnter(PointerEventData eventData)
public void OnPointerExit(PointerEventData eventData)
public void UpdateTooltip()
public void OnDisable()
public void .ctor()
```


## TooltipInterface

`class TooltipInterface : MonoBehaviour`

**方法**

```csharp
public abstract string GetTooltip();
public abstract bool IsTooltipEnabled();
protected void .ctor()
```


## TooltipListener

`interface TooltipListener : PixelElement`

**方法**

```csharp
public abstract bool HasTooltip();
public abstract RichText GetTooltip();
public abstract RichText GetTooltipAdvanced();
```


## TooltipManager

`class TooltipManager : MonoBehaviour`

**字段/属性**

```csharp
public static TooltipManager _instance;
public TextMeshProUGUI content;
public GameObject tooltipGameobject;
```

**方法**

```csharp
public void SetAndShowTooltip(string message)
public void SetAndShowTooltip(string message, Component owner)
public void HideTooltip()
public void UpdateTooltipPosition()
public void .ctor()
```


## TooltipUseHelper

`class TooltipUseHelper`

**方法**

```csharp
public static void HandleUseTooltip(GameItem sourceItem, GameItem targetItem, RichTextBuilder customBuilder)
public static bool HasUseTooltip(GameItem sourceItem, GameItem targetItem)
```


## TouchScreenKeyboard

`enum TouchScreenKeyboard`

**字段/属性**

```csharp
public int value__;
public const TouchScreenKeyboard.Status Visible = 0;
public const TouchScreenKeyboard.Status Done = 1;
public const TouchScreenKeyboard.Status Canceled = 2;
public const TouchScreenKeyboard.Status LostFocus = 3;
```


## TreeNodeRender

`class TreeNodeRender : MonoBehaviour, IDragHandler, IEventSystemHandler, IBeginDragHandler, IEndDragHandler, IDropHandler, IInitializePotentialDragHandler, IPointerClickHandler, IPointerUpHandler, IPointerDownHandler, IPointerMoveHandler, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, IUpdateSelectedHandler, IScrollHandler`

**字段/属性**

```csharp
public MultiEventHandler eventHandler;
```

**属性**

```csharp
public PixelWindow window { get; set; }
public PixelElement element { get; set; }
public TreeNodeRender parent { get; set; }
public RectTransform rectTransform { get; set; }
public Image image { get; set; }
public RectMask2D mask { get; set; }
public TextMeshProUGUI text { get; set; }
```

**方法**

```csharp
public PixelWindow get_window()
public PixelElement get_element()
public TreeNodeRender get_parent()
public RectTransform get_rectTransform()
public Image get_image()
public RectMask2D get_mask()
public TextMeshProUGUI get_text()
public static TreeNodeRender InstantiateAsRoot(Transform transform)
public static TreeNodeRender InstantiateAsRoot(Transform transform, bool isText)
public static TreeNodeRender Instantiate()
public static TreeNodeRender InstantiateText()
public static TreeNodeRender Instantiate(PixelElement element, string editorObjectName = "Element")
public static TreeNodeRender Instantiate(PixelWindow window, string editorObjectName = "Window")
public TreeNodeRender InitEventHandler()
public TreeNodeRender SetParentOverlayRoot()
public TreeNodeRender SetParentTempRoot()
public TreeNodeRender SetParentWindowRoot()
public TreeNodeRender SetParent(TreeNodeRender parent)
public TreeNodeRender SetParent(TreeNodeRender parent, bool worldPositionStays)
public TreeNodeRender ResetParent()
public TreeNodeRender RemoveParent()
public PixelWindow FindParentWindow()
public PixelElement FindParentElement()
public T FindParentElement<T>()
public void OnPointerClick(PointerEventData eventData)
public void OnPointerMove(PointerEventData eventData)
public void OnPointerUp(PointerEventData eventData)
public void OnPointerDown(PointerEventData eventData)
public void OnInitializePotentialDrag(PointerEventData eventData)
public void OnDrag(PointerEventData eventData)
public void OnBeginDrag(PointerEventData eventData)
public void OnEndDrag(PointerEventData eventData)
public void OnDrop(PointerEventData eventData)
public void OnPointerEnter(PointerEventData eventData)
public void OnPointerExit(PointerEventData eventData)
public void OnScroll(PointerEventData eventData)
public void OnPointerDoubleClick(PointerEventData eventData)
public void OnSelect(BaseEventData eventData)
public void OnDeselect(BaseEventData eventData)
public void OnUpdateSelected(BaseEventData eventData)
public void .ctor()
```


## TreeView

`class TreeView : IEnumerable<ITreeViewItem>, IEnumerable, IEnumerator<ITreeViewItem>, IEnumerator, IDisposable`

**字段/属性**

```csharp
public IEnumerable<ITreeViewItem> <>3__rootItems;
```

**方法**

```csharp
public void .ctor(int <>1__state)
```


## TreeViewReorderableDragAndDropController

`struct TreeViewReorderableDragAndDropController`

**字段/属性**

```csharp
public int parentId;
public int childIndex;
```

**方法**

```csharp
public void .ctor(int parentId, int childIndex)
```


## TwoPaneSplitView

`class TwoPaneSplitView : VisualElement.UxmlTraits`

**方法**

```csharp
public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
public void .ctor()
```


## TypeElement

`class TypeElement : MonoBehaviour`

**字段/属性**

```csharp
public TextMeshProUGUI typeTMP;
public Image bgImage;
```

**方法**

```csharp
public void .ctor()
```


## UIController

`class UIController : MonoBehaviour`

**方法**

```csharp
public void .ctor()
```


## UIElementsRuntimeUtility

`class UIElementsRuntimeUtility`

**字段/属性**

```csharp
public static readonly UIElementsRuntimeUtility.<>c <>9;
public static Comparison<Panel> <>9__47_0;
```

**方法**

```csharp
public void .ctor()
```


## UIRenderDevice

`class UIRenderDevice`

**字段/属性**

```csharp
public static readonly UIRenderDevice.<>c <>9;
public static Func<MeshHandle> <>9__53_0;
public static Action<MeshHandle> <>9__53_1;
```

**方法**

```csharp
public void .ctor()
```


## VectorImageRenderInfoPool

`class VectorImageRenderInfoPool`

**字段/属性**

```csharp
public static readonly VectorImageRenderInfoPool.<>c <>9;
public static Func<VectorImageRenderInfo> <>9__0_0;
public static Action<VectorImageRenderInfo> <>9__0_1;
```

**方法**

```csharp
public void .ctor()
```


## VerticalVirtualizationController

`class VerticalVirtualizationController`

**字段/属性**

```csharp
public static readonly VerticalVirtualizationController.<>c<T> <>9;
public static Func<T> <>9__30_0;
public static Action<T> <>9__30_1;
```

**方法**

```csharp
public void .ctor()
```


## VisualElement

`class VisualElement`

**字段/属性**

```csharp
public VisualElement <>4__this;
public StyleValues to;
```

**方法**

```csharp
public void .ctor()
```


## VisualElementFocusRing

`class VisualElementFocusRing`

**字段/属性**

```csharp
public int m_AutoIndex;
public Focusable m_Focusable;
public bool m_IsSlot;
public List<VisualElementFocusRing.FocusRingRecord> m_ScopeNavigationOrder;
```

**方法**

```csharp
public void .ctor()
```


## WantedElement

`class WantedElement : MonoBehaviour`

**字段/属性**

```csharp
public string identifier;
public GameObject cross;
```

**方法**

```csharp
public void OnEnable()
public void OnArrested()
public void .ctor()
```


## Windows

`struct Windows : IComparable, IComparable<Windows.HMODULE>, IEquatable<Windows.HMODULE>, IFormattable`

**字段/属性**

```csharp
public readonly void* Value;
```

**属性**

```csharp
public static Windows.HMODULE INVALID_VALUE { get; }
public static Windows.HMODULE NULL { get; }
```

**方法**

```csharp
public void .ctor(void* value)
public static Windows.HMODULE get_INVALID_VALUE()
public static Windows.HMODULE get_NULL()
public static bool op_Inequality(Windows.HMODULE left, Windows.HMODULE right)
public static IntPtr op_Implicit(Windows.HMODULE value)
public int CompareTo(object obj)
public int CompareTo(Windows.HMODULE other)
public override bool Equals(object obj)
public bool Equals(Windows.HMODULE other)
public override int GetHashCode()
public override string ToString()
public string ToString(string format, IFormatProvider formatProvider)
```


## WindowsHandler

`class WindowsHandler : MonoBehaviour, Cancelable`

**字段/属性**

```csharp
public static WindowsHandler current;
public int _maxVisibleFloatingWindows;
```

**属性**

```csharp
public List<PixelWindow> visibleWindows { get; set; }
```

**方法**

```csharp
public List<PixelWindow> get_visibleWindows()
public static void CloseWindow(PixelWindow window)
public static bool TryAddWindow(PixelWindow window)
public static bool TryRemoveWindow(PixelWindow window)
public static bool ContainsWindow(PixelWindow window)
public static void HideAllMovable()
public static void HideAllNonImportantMovable()
public bool Cancel()
public void .ctor()
```


## WindowsRuntimeProjections

`struct WindowsRuntimeProjections`

**字段/属性**

```csharp
public readonly string WinRTNamespace;
public readonly string ClrNamespace;
public readonly string ClrName;
public readonly string ClrAssembly;
public readonly bool Attribute;
```

**方法**

```csharp
public void .ctor(string winrt_namespace, string clr_namespace, string clr_name, string clr_assembly, bool attribute = False)
```


## XElement

`class XElement : IEnumerable<XAttribute>, IEnumerable, IEnumerator<XAttribute>, IDisposable, IEnumerator`

**字段/属性**

```csharp
public XElement <>4__this;
public XName <>3__name;
```

**方法**

```csharp
public void .ctor(int <>1__state)
```
