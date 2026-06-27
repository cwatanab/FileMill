using System.Resources;

namespace FileMill.Properties;

/// <summary>
/// XAML から {x:Static} で参照するための静的プロパティを提供。
/// 内部では ResourceManager を使って現在のカルチャに応じた文字列を返す。
/// </summary>
public static class Loc
{
    private static readonly ResourceManager RM = new("FileMill.Properties.Resources", typeof(Loc).Assembly);

    private static string GetSafeString(string key, string defaultValue = "")
    {
        try
        {
            return RM.GetString(key) ?? defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    // Menu
    public static string MenuFile => GetSafeString("MenuFile");
    public static string MenuAddImages => GetSafeString("MenuAddImages");
    public static string MenuAddFolder => GetSafeString("MenuAddFolder");
    public static string MenuSelectOutput => GetSafeString("MenuSelectOutput");
    public static string MenuExit => GetSafeString("MenuExit");
    public static string MenuList => GetSafeString("MenuList");
    public static string MenuClearAll => GetSafeString("MenuClearAll");
    public static string MenuRemoveSelected => GetSafeString("MenuRemoveSelected");
    public static string MenuSettings => GetSafeString("MenuSettings");
    public static string MenuOptions => GetSafeString("MenuOptions");
    public static string MenuConvert => GetSafeString("MenuConvert");
    public static string MenuConvertStart => GetSafeString("MenuConvertStart");
    public static string MenuHelp => GetSafeString("MenuHelp");
    public static string MenuAbout => GetSafeString("MenuAbout");

    // Tabs
    public static string TabImageConversion => GetSafeString("TabImageConversion");
    public static string TabFileOptimization => GetSafeString("TabFileOptimization");

    // Toolbar
    public static string ToolbarOutput => GetSafeString("ToolbarOutput");
    public static string ToolbarConvert => GetSafeString("ToolbarConvert");
    public static string ToolbarOptimize => GetSafeString("ToolbarOptimize");

    // Sidebar
    public static string SidebarProcessingOptions => GetSafeString("SidebarProcessingOptions");
    public static string SidebarOptimizeOptions => GetSafeString("SidebarOptimizeOptions");

    // Steps
    public static string StepExifAutoRotate => GetSafeString("StepExifAutoRotate");
    public static string StepCrop => GetSafeString("StepCrop");
    public static string StepRotate => GetSafeString("StepRotate");
    public static string StepResize => GetSafeString("StepResize");
    public static string StepPadding => GetSafeString("StepPadding");
    public static string StepGrayscale => GetSafeString("StepGrayscale");
    public static string StepSharpen => GetSafeString("StepSharpen");
    public static string StepColorAdjust => GetSafeString("StepColorAdjust");
    public static string StepToneCurve => GetSafeString("StepToneCurve");
    public static string StepPosterize => GetSafeString("StepPosterize");
    public static string StepComposite => GetSafeString("StepComposite");
    public static string StepFormatConvert => GetSafeString("StepFormatConvert");
    public static string StepOptimize => GetSafeString("StepOptimize");

    // File optimization options
    public static string OptOfficeOptimize => GetSafeString("OptOfficeOptimize");
    public static string OptConvertToPdf => GetSafeString("OptConvertToPdf");
    public static string OptConvertToPdfA => GetSafeString("OptConvertToPdfA");
    public static string OptImageOptimize => GetSafeString("OptImageOptimize");
    public static string OptMediaOptimize => GetSafeString("OptMediaOptimize");

    // Columns
    public static string ColName => GetSafeString("ColName");
    public static string ColDimensions => GetSafeString("ColDimensions");
    public static string ColSize => GetSafeString("ColSize");
    public static string ColType => GetSafeString("ColType");
    public static string ColDateModified => GetSafeString("ColDateModified");
    public static string ColDateTaken => GetSafeString("ColDateTaken");
    public static string ColPath => GetSafeString("ColPath");
    public static string ColOriginalSize => GetSafeString("ColOriginalSize");
    public static string ColOptimizedSize => GetSafeString("ColOptimizedSize");
    public static string ColSavings => GetSafeString("ColSavings");
    public static string ColStatus => GetSafeString("ColStatus");

    // GroupBox
    public static string GroupImageFiles => GetSafeString("GroupImageFiles");
    public static string GroupOptimizeFiles => GetSafeString("GroupOptimizeFiles");

    // Placeholder
    public static string PlaceholderImageFiles => GetSafeString("PlaceholderImageFiles");
    public static string PlaceholderOptimizeFiles => GetSafeString("PlaceholderOptimizeFiles");

    // Status
    public static string StatusReady => GetSafeString("StatusReady");
    public static string StatusNoFiles => GetSafeString("StatusNoFiles");
    public static string StatusNoOptions => GetSafeString("StatusNoOptions");
    public static string StatusProcessing => GetSafeString("StatusProcessing");
    public static string StatusError => GetSafeString("StatusError");
    public static string StatusDone => GetSafeString("StatusDone");

    // Settings
    public static string SettingsTheme => GetSafeString("SettingsTheme");
    public static string SettingsThemeLight => GetSafeString("SettingsThemeLight");
    public static string SettingsThemeDark => GetSafeString("SettingsThemeDark");
    public static string SettingsThemeAuto => GetSafeString("SettingsThemeAuto");
    public static string SettingsLanguage => GetSafeString("SettingsLanguage");
    public static string SettingsNotifySound => GetSafeString("SettingsNotifySound");
    public static string SettingsConfirmClear => GetSafeString("SettingsConfirmClear");
    public static string SettingsOutputFilename => GetSafeString("SettingsOutputFilename");
    public static string SettingsOutputFilenameHint => GetSafeString("SettingsOutputFilenameHint");
    public static string SettingsExternalTools => GetSafeString("SettingsExternalTools");

    // Debug
    public static string DebugClear => GetSafeString("DebugClear");
    public static string DebugConsole => GetSafeString("DebugConsole");


    // Common
    public static string ButtonOK => GetSafeString("ButtonOK");
    public static string ButtonCancel => GetSafeString("ButtonCancel");
    public static string ButtonRemove => GetSafeString("ButtonRemove");

    // About
    public static string AboutTitle => GetSafeString("AboutTitle");
    public static string AboutMessage => GetSafeString("AboutMessage");

    // Modal titles
    public static string ModalTitleGrayscale => GetSafeString("ModalTitleGrayscale");
    public static string ModalTitleExifAutoRotate => GetSafeString("ModalTitleExifAutoRotate");
    public static string ModalTitleCrop => GetSafeString("ModalTitleCrop");
    public static string ModalTitleResize => GetSafeString("ModalTitleResize");
    public static string ModalTitlePadding => GetSafeString("ModalTitlePadding");
    public static string ModalTitleSharpen => GetSafeString("ModalTitleSharpen");
    public static string ModalTitleColorAdjust => GetSafeString("ModalTitleColorAdjust");
    public static string ModalTitleToneCurve => GetSafeString("ModalTitleToneCurve");
    public static string ModalTitleFormat => GetSafeString("ModalTitleFormat");
    public static string ModalTitleOptimize => GetSafeString("ModalTitleOptimize");
    public static string ModalTitlePosterize => GetSafeString("ModalTitlePosterize");
    public static string ModalTitleRotate => GetSafeString("ModalTitleRotate");
    public static string ModalTitleComposite => GetSafeString("ModalTitleComposite");
    public static string ModalTitleOfficeOptimize => GetSafeString("ModalTitleOfficeOptimize");
    public static string ModalTitleOfficePdf => GetSafeString("ModalTitleOfficePdf");
    public static string ModalTitlePdfConvert => GetSafeString("ModalTitlePdfConvert");
    public static string ModalTitlePdfImage => GetSafeString("ModalTitlePdfImage");
    public static string ModalTitlePdfStream => GetSafeString("ModalTitlePdfStream");
    public static string ModalTitlePdfStructure => GetSafeString("ModalTitlePdfStructure");
    public static string ModalTitlePdfCompatibility => GetSafeString("ModalTitlePdfCompatibility");
    public static string ModalTitlePdfRestrictions => GetSafeString("ModalTitlePdfRestrictions");
    public static string ModalTitleImageOptimize => GetSafeString("ModalTitleImageOptimize");
    public static string ModalTitleMediaOptimize => GetSafeString("ModalTitleMediaOptimize");
    public static string ModalTitleOptions => GetSafeString("ModalTitleOptions");
    public static string ModalTitleDefault => GetSafeString("ModalTitleDefault");

    // Summaries
    public static string SummaryOutput => GetSafeString("SummaryOutput");
    public static string SummaryOptimizeOutput => GetSafeString("SummaryOptimizeOutput");

    // Added Status & Dialog localization keys
    public static string StatusProcessingProgress => GetSafeString("StatusProcessingProgress");
    public static string StatusErrorMsg => GetSafeString("StatusErrorMsg");
    public static string StatusDoneMsg => GetSafeString("StatusDoneMsg");
    public static string StatusCancelled => GetSafeString("StatusCancelled");
    public static string StatusOptimizingPackage => GetSafeString("StatusOptimizingPackage");
    public static string StatusConvertingToPdf => GetSafeString("StatusConvertingToPdf");
    public static string StatusOptimizingPdf => GetSafeString("StatusOptimizingPdf");
    public static string StatusOfficePdfInteropUnavailable => GetSafeString("StatusOfficePdfInteropUnavailable");
    public static string StatusOfficePdfAUnsupportedForExcel => GetSafeString("StatusOfficePdfAUnsupportedForExcel");
    public static string StatusCompleted => GetSafeString("StatusCompleted");
    public static string StatusErrorState => GetSafeString("StatusErrorState");
    public static string StatusOptimizingProgress => GetSafeString("StatusOptimizingProgress");
    public static string StatusOptimizeCancelled => GetSafeString("StatusOptimizeCancelled");
    public static string StatusOptimizeDone => GetSafeString("StatusOptimizeDone");
    public static string StatusPdfOptimizeCancelled => GetSafeString("StatusPdfOptimizeCancelled");
    public static string StatusPdfOptimizeDone => GetSafeString("StatusPdfOptimizeDone");
    public static string MsgConfirmClearImageList => GetSafeString("MsgConfirmClearImageList");
    public static string MsgConfirmClearList => GetSafeString("MsgConfirmClearList");
    public static string TitleConfirm => GetSafeString("TitleConfirm");
    public static string StatusWaiting => GetSafeString("StatusWaiting");
    public static string StatusCancelling => GetSafeString("StatusCancelling");

    public static string DlgTitleSelectImages => GetSafeString("DlgTitleSelectImages");
    public static string DlgFilterImages => GetSafeString("DlgFilterImages");
    public static string DlgTitleAddImageFolder => GetSafeString("DlgTitleAddImageFolder");
    public static string DlgTitleSelectOutputFolder => GetSafeString("DlgTitleSelectOutputFolder");
    public static string DlgTitleSelectToolExecutable => GetSafeString("DlgTitleSelectToolExecutable");
    public static string DlgFilterExecutable => GetSafeString("DlgFilterExecutable");
    public static string DlgTitleSelectCompositeImage => GetSafeString("DlgTitleSelectCompositeImage");
    public static string DlgFilterCompositeImages => GetSafeString("DlgFilterCompositeImages");
    public static string DlgTitleSelectOfficeFiles => GetSafeString("DlgTitleSelectOfficeFiles");
    public static string DlgFilterOfficeFiles => GetSafeString("DlgFilterOfficeFiles");
    public static string DlgTitleSelectPdfFiles => GetSafeString("DlgTitleSelectPdfFiles");
    public static string DlgFilterPdfFiles => GetSafeString("DlgFilterPdfFiles");
    public static string DlgTitleAddFolder => GetSafeString("DlgTitleAddFolder");
    public static string TooltipOfficePdfAvailable => GetSafeString("TooltipOfficePdfAvailable");
    public static string TooltipOfficePdfUnavailable => GetSafeString("TooltipOfficePdfUnavailable");

    // Update check
    public static string TitleUpdateCheck => GetSafeString("TitleUpdateCheck");
    public static string MsgUpdateUpToDate => GetSafeString("MsgUpdateUpToDate");
    public static string MsgUpdateAvailable => GetSafeString("MsgUpdateAvailable");
    public static string MsgUpdateNoPackage => GetSafeString("MsgUpdateNoPackage");
    public static string MsgUpdateCheckFailed => GetSafeString("MsgUpdateCheckFailed");

    // Language
    public static string MsgLanguageRestart => GetSafeString("MsgLanguageRestart");

    // Presets
    public static string StatusPresetNameRequired => GetSafeString("StatusPresetNameRequired");
    public static string StatusPresetApplied => GetSafeString("StatusPresetApplied");
    public static string StatusPresetSaved => GetSafeString("StatusPresetSaved");
    public static string PresetTypeImage => GetSafeString("PresetTypeImage");
    public static string PresetTypeOffice => GetSafeString("PresetTypeOffice");
    public static string PresetTypePdf => GetSafeString("PresetTypePdf");

    // Wizard
    public static string WizardNext => System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "ja" ? "次へ ➔" : "Next ➔";
    public static string WizardBack => System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "ja" ? "⬅ 戻る" : "⬅ Back";
    public static string WizardStep1Label => System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "ja" ? "ファイル追加" : "Add Files";
    public static string WizardStep2Label => System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "ja" ? "オプション設定" : "Configure";
    public static string WizardStep3Label => System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "ja" ? "変換・結果" : "Results";
    public static string WizardStep1Title => System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "ja" ? "ファイルを変換リストに追加" : "Add files to conversion list";
    public static string WizardStep2Title => System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "ja" ? "変換オプションを設定" : "Configure conversion options";
    public static string WizardStep3Title => System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "ja" ? "変換処理と結果レポート" : "Conversion processing & results";
    public static string WizardRun => System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "ja" ? "一括変換を実行 ⚡" : "Start Conversion ⚡";
    public static string WizardAdjustSettings => System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "ja" ? "設定の調整へ戻る" : "Adjust Settings";
    public static string WizardStartOver => System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "ja" ? "最初から始める (リストをクリア)" : "Start Over (Clear List)";
    public static string WizardTotalSize => System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "ja" ? "変換前の合計サイズ" : "Total Original Size";
    public static string WizardTotalSaved => System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "ja" ? "削減後の合計サイズ" : "Total Optimized Size";
    public static string WizardSavingRatio => System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "ja" ? "削減率" : "Saving Ratio";
    public static string WizardAddFilesBtn => System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "ja" ? "ファイルを追加" : "Add Files";
    public static string WizardAddFolderBtn => System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "ja" ? "フォルダを追加" : "Add Folder";
}

