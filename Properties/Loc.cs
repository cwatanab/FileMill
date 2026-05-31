using System.Resources;

namespace FileMill.Properties;

/// <summary>
/// XAML から {x:Static} で参照するための静的プロパティを提供。
/// 内部では ResourceManager を使って現在のカルチャに応じた文字列を返す。
/// </summary>
public static class Loc
{
    private static readonly ResourceManager RM = new("FileMill.Properties.Resources", typeof(Loc).Assembly);

    // Menu
    public static string MenuFile => RM.GetString("MenuFile") ?? "";
    public static string MenuAddImages => RM.GetString("MenuAddImages") ?? "";
    public static string MenuAddFolder => RM.GetString("MenuAddFolder") ?? "";
    public static string MenuSelectOutput => RM.GetString("MenuSelectOutput") ?? "";
    public static string MenuExit => RM.GetString("MenuExit") ?? "";
    public static string MenuList => RM.GetString("MenuList") ?? "";
    public static string MenuClearAll => RM.GetString("MenuClearAll") ?? "";
    public static string MenuRemoveSelected => RM.GetString("MenuRemoveSelected") ?? "";
    public static string MenuSettings => RM.GetString("MenuSettings") ?? "";
    public static string MenuOptions => RM.GetString("MenuOptions") ?? "";
    public static string MenuConvert => RM.GetString("MenuConvert") ?? "";
    public static string MenuConvertStart => RM.GetString("MenuConvertStart") ?? "";
    public static string MenuHelp => RM.GetString("MenuHelp") ?? "";
    public static string MenuAbout => RM.GetString("MenuAbout") ?? "";

    // Tabs
    public static string TabImageConversion => RM.GetString("TabImageConversion") ?? "";
    public static string TabFileOptimization => RM.GetString("TabFileOptimization") ?? "";

    // Toolbar
    public static string ToolbarOutput => RM.GetString("ToolbarOutput") ?? "";
    public static string ToolbarConvert => RM.GetString("ToolbarConvert") ?? "";
    public static string ToolbarOptimize => RM.GetString("ToolbarOptimize") ?? "";

    // Sidebar
    public static string SidebarProcessingOptions => RM.GetString("SidebarProcessingOptions") ?? "";
    public static string SidebarOptimizeOptions => RM.GetString("SidebarOptimizeOptions") ?? "";

    // Steps
    public static string StepExifAutoRotate => RM.GetString("StepExifAutoRotate") ?? "";
    public static string StepCrop => RM.GetString("StepCrop") ?? "";
    public static string StepRotate => RM.GetString("StepRotate") ?? "";
    public static string StepResize => RM.GetString("StepResize") ?? "";
    public static string StepPadding => RM.GetString("StepPadding") ?? "";
    public static string StepGrayscale => RM.GetString("StepGrayscale") ?? "";
    public static string StepSharpen => RM.GetString("StepSharpen") ?? "";
    public static string StepColorAdjust => RM.GetString("StepColorAdjust") ?? "";
    public static string StepToneCurve => RM.GetString("StepToneCurve") ?? "";
    public static string StepPosterize => RM.GetString("StepPosterize") ?? "";
    public static string StepComposite => RM.GetString("StepComposite") ?? "";
    public static string StepFormatConvert => RM.GetString("StepFormatConvert") ?? "";
    public static string StepOptimize => RM.GetString("StepOptimize") ?? "";

    // File optimization options
    public static string OptOfficeOptimize => RM.GetString("OptOfficeOptimize") ?? "";
    public static string OptImageOptimize => RM.GetString("OptImageOptimize") ?? "";
    public static string OptMediaOptimize => RM.GetString("OptMediaOptimize") ?? "";

    // Columns
    public static string ColName => RM.GetString("ColName") ?? "";
    public static string ColDimensions => RM.GetString("ColDimensions") ?? "";
    public static string ColSize => RM.GetString("ColSize") ?? "";
    public static string ColType => RM.GetString("ColType") ?? "";
    public static string ColDateModified => RM.GetString("ColDateModified") ?? "";
    public static string ColDateTaken => RM.GetString("ColDateTaken") ?? "";
    public static string ColPath => RM.GetString("ColPath") ?? "";
    public static string ColOriginalSize => RM.GetString("ColOriginalSize") ?? "";
    public static string ColOptimizedSize => RM.GetString("ColOptimizedSize") ?? "";
    public static string ColSavings => RM.GetString("ColSavings") ?? "";
    public static string ColStatus => RM.GetString("ColStatus") ?? "";

    // GroupBox
    public static string GroupImageFiles => RM.GetString("GroupImageFiles") ?? "";
    public static string GroupOptimizeFiles => RM.GetString("GroupOptimizeFiles") ?? "";

    // Placeholder
    public static string PlaceholderImageFiles => RM.GetString("PlaceholderImageFiles") ?? "";
    public static string PlaceholderOptimizeFiles => RM.GetString("PlaceholderOptimizeFiles") ?? "";

    // Status
    public static string StatusReady => RM.GetString("StatusReady") ?? "";
    public static string StatusNoFiles => RM.GetString("StatusNoFiles") ?? "";
    public static string StatusNoOptions => RM.GetString("StatusNoOptions") ?? "";
    public static string StatusProcessing => RM.GetString("StatusProcessing") ?? "";
    public static string StatusError => RM.GetString("StatusError") ?? "";
    public static string StatusDone => RM.GetString("StatusDone") ?? "";

    // Settings
    public static string SettingsTheme => RM.GetString("SettingsTheme") ?? "";
    public static string SettingsThemeLight => RM.GetString("SettingsThemeLight") ?? "";
    public static string SettingsThemeDark => RM.GetString("SettingsThemeDark") ?? "";
    public static string SettingsThemeAuto => RM.GetString("SettingsThemeAuto") ?? "";
    public static string SettingsLanguage => RM.GetString("SettingsLanguage") ?? "";
    public static string SettingsNotifySound => RM.GetString("SettingsNotifySound") ?? "";
    public static string SettingsConfirmClear => RM.GetString("SettingsConfirmClear") ?? "";
    public static string SettingsOutputFilename => RM.GetString("SettingsOutputFilename") ?? "";
    public static string SettingsOutputFilenameHint => RM.GetString("SettingsOutputFilenameHint") ?? "";
    public static string SettingsExternalTools => RM.GetString("SettingsExternalTools") ?? "";

    // Debug
    public static string DebugClear => RM.GetString("DebugClear") ?? "";
    public static string DebugConsole => RM.GetString("DebugConsole") ?? "";


    // Common
    public static string ButtonOK => RM.GetString("ButtonOK") ?? "";
    public static string ButtonCancel => RM.GetString("ButtonCancel") ?? "";
    public static string ButtonRemove => RM.GetString("ButtonRemove") ?? "";

    // About
    public static string AboutTitle => RM.GetString("AboutTitle") ?? "";
    public static string AboutMessage => RM.GetString("AboutMessage") ?? "";

    // Modal titles
    public static string ModalTitleGrayscale => RM.GetString("ModalTitleGrayscale") ?? "";
    public static string ModalTitleExifAutoRotate => RM.GetString("ModalTitleExifAutoRotate") ?? "";
    public static string ModalTitleCrop => RM.GetString("ModalTitleCrop") ?? "";
    public static string ModalTitleResize => RM.GetString("ModalTitleResize") ?? "";
    public static string ModalTitlePadding => RM.GetString("ModalTitlePadding") ?? "";
    public static string ModalTitleSharpen => RM.GetString("ModalTitleSharpen") ?? "";
    public static string ModalTitleColorAdjust => RM.GetString("ModalTitleColorAdjust") ?? "";
    public static string ModalTitleToneCurve => RM.GetString("ModalTitleToneCurve") ?? "";
    public static string ModalTitleFormat => RM.GetString("ModalTitleFormat") ?? "";
    public static string ModalTitleOptimize => RM.GetString("ModalTitleOptimize") ?? "";
    public static string ModalTitlePosterize => RM.GetString("ModalTitlePosterize") ?? "";
    public static string ModalTitleRotate => RM.GetString("ModalTitleRotate") ?? "";
    public static string ModalTitleComposite => RM.GetString("ModalTitleComposite") ?? "";
    public static string ModalTitleOfficeOptimize => RM.GetString("ModalTitleOfficeOptimize") ?? "";
    public static string ModalTitleImageOptimize => RM.GetString("ModalTitleImageOptimize") ?? "";
    public static string ModalTitleMediaOptimize => RM.GetString("ModalTitleMediaOptimize") ?? "";
    public static string ModalTitleOptions => RM.GetString("ModalTitleOptions") ?? "";
    public static string ModalTitleDefault => RM.GetString("ModalTitleDefault") ?? "";
}
