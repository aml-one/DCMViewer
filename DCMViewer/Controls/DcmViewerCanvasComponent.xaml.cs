using System.Windows.Controls;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;

namespace DCMViewer.Controls;

public partial class DcmViewerCanvasComponent : UserControl
{
    private static readonly ImageSource DefaultLogoSource =
        new BitmapImage(new Uri("pack://application:,,,/AmL.DCMViewer;component/Images/logo.png", UriKind.Absolute));

    public static readonly DependencyProperty GradientModeProperty =
        DependencyProperty.Register(
            nameof(GradientMode),
            typeof(ViewerBackgroundGradientMode),
            typeof(DcmViewerCanvasComponent),
            new PropertyMetadata(ViewerBackgroundGradientMode.Radial, OnAppearancePropertyChanged));

    public static readonly DependencyProperty GradientStartColorProperty =
        DependencyProperty.Register(
            nameof(GradientStartColor),
            typeof(Color),
            typeof(DcmViewerCanvasComponent),
            new PropertyMetadata(Color.FromRgb(255, 255, 255), OnAppearancePropertyChanged));

    public static readonly DependencyProperty GradientMidColorProperty =
        DependencyProperty.Register(
            nameof(GradientMidColor),
            typeof(Color),
            typeof(DcmViewerCanvasComponent),
            new PropertyMetadata(Color.FromRgb(243, 246, 249), OnAppearancePropertyChanged));

    public static readonly DependencyProperty GradientMidOuterColorProperty =
        DependencyProperty.Register(
            nameof(GradientMidOuterColor),
            typeof(Color),
            typeof(DcmViewerCanvasComponent),
            new PropertyMetadata(Color.FromRgb(211, 215, 218), OnAppearancePropertyChanged));

    public static readonly DependencyProperty GradientOuterColorProperty =
        DependencyProperty.Register(
            nameof(GradientOuterColor),
            typeof(Color),
            typeof(DcmViewerCanvasComponent),
            new PropertyMetadata(Color.FromRgb(176, 180, 184), OnAppearancePropertyChanged));

    public static readonly DependencyProperty IsLogoVisibleProperty =
        DependencyProperty.Register(
            nameof(IsLogoVisible),
            typeof(bool),
            typeof(DcmViewerCanvasComponent),
            new PropertyMetadata(true, OnAppearancePropertyChanged));

    public static readonly DependencyProperty LogoSourceProperty =
        DependencyProperty.Register(
            nameof(LogoSource),
            typeof(ImageSource),
            typeof(DcmViewerCanvasComponent),
            new PropertyMetadata(null, OnAppearancePropertyChanged));

    public static readonly DependencyProperty WatermarkTextProperty =
        DependencyProperty.Register(
            nameof(WatermarkText),
            typeof(string),
            typeof(DcmViewerCanvasComponent),
            new PropertyMetadata("AmL", OnAppearancePropertyChanged));

    public static readonly DependencyProperty WatermarkTextColorProperty =
        DependencyProperty.Register(
            nameof(WatermarkTextColor),
            typeof(Color),
            typeof(DcmViewerCanvasComponent),
            new PropertyMetadata(Color.FromRgb(184, 163, 92), OnAppearancePropertyChanged));

    public static readonly DependencyProperty WatermarkTextFontSizeProperty =
        DependencyProperty.Register(
            nameof(WatermarkTextFontSize),
            typeof(double),
            typeof(DcmViewerCanvasComponent),
            new PropertyMetadata(80.0, OnAppearancePropertyChanged));

    public DcmViewerCanvasComponent()
    {
        InitializeComponent();
        UpdateBackgroundBrush();
        UpdateWatermarkVisuals();
    }

    public ViewerBackgroundGradientMode GradientMode
    {
        get => (ViewerBackgroundGradientMode)GetValue(GradientModeProperty);
        set => SetValue(GradientModeProperty, value);
    }

    public Color GradientStartColor
    {
        get => (Color)GetValue(GradientStartColorProperty);
        set => SetValue(GradientStartColorProperty, value);
    }

    public Color GradientMidColor
    {
        get => (Color)GetValue(GradientMidColorProperty);
        set => SetValue(GradientMidColorProperty, value);
    }

    public Color GradientMidOuterColor
    {
        get => (Color)GetValue(GradientMidOuterColorProperty);
        set => SetValue(GradientMidOuterColorProperty, value);
    }

    public Color GradientOuterColor
    {
        get => (Color)GetValue(GradientOuterColorProperty);
        set => SetValue(GradientOuterColorProperty, value);
    }

    public bool IsLogoVisible
    {
        get => (bool)GetValue(IsLogoVisibleProperty);
        set => SetValue(IsLogoVisibleProperty, value);
    }

    public ImageSource? LogoSource
    {
        get => (ImageSource?)GetValue(LogoSourceProperty);
        set => SetValue(LogoSourceProperty, value);
    }

    public string WatermarkText
    {
        get => (string)GetValue(WatermarkTextProperty);
        set => SetValue(WatermarkTextProperty, value);
    }

    public Color WatermarkTextColor
    {
        get => (Color)GetValue(WatermarkTextColorProperty);
        set => SetValue(WatermarkTextColorProperty, value);
    }

    public double WatermarkTextFontSize
    {
        get => (double)GetValue(WatermarkTextFontSizeProperty);
        set => SetValue(WatermarkTextFontSizeProperty, value);
    }

    public HelixViewport3D Viewport3D => Viewport;

    public CuttingPlaneGroup CuttingPlaneGroup => SectionCutGroup;

    public ModelVisual3D SectionPlaneModel => SectionPlaneVisual;

    public LinesVisual3D SectionPlaneOutline => SectionPlaneOutlineVisual;

    public LinesVisual3D MeasurementLineVisual => MeasurementLine;

    public BillboardTextVisual3D MeasurementTextVisual => MeasurementText;

    private static void OnAppearancePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DcmViewerCanvasComponent component)
        {
            return;
        }

        component.UpdateBackgroundBrush();
        component.UpdateWatermarkVisuals();
    }

    private void UpdateBackgroundBrush()
    {
        GradientBrush brush = GradientMode switch
        {
            ViewerBackgroundGradientMode.LinearHorizontal => new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5)
            },
            ViewerBackgroundGradientMode.LinearVertical => new LinearGradientBrush
            {
                StartPoint = new Point(0.5, 0),
                EndPoint = new Point(0.5, 1)
            },
            _ => new RadialGradientBrush
            {
                GradientOrigin = new Point(0.5, 0.5),
                Center = new Point(0.5, 0.5),
                RadiusX = 0.8,
                RadiusY = 0.8
            }
        };

        brush.GradientStops.Add(new GradientStop(GradientStartColor, 0.0));
        brush.GradientStops.Add(new GradientStop(GradientMidColor, 0.35));
        brush.GradientStops.Add(new GradientStop(GradientMidOuterColor, 0.7));
        brush.GradientStops.Add(new GradientStop(GradientOuterColor, 1.0));

        WatermarkCanvas.Background = brush;
    }

    private void UpdateWatermarkVisuals()
    {
        LogoImage.Source = LogoSource ?? DefaultLogoSource;
        LogoImage.Visibility = IsLogoVisible ? Visibility.Visible : Visibility.Collapsed;

        WatermarkTextBlock.Text = WatermarkText;
        WatermarkTextBlock.Foreground = new SolidColorBrush(WatermarkTextColor);
        WatermarkTextBlock.FontSize = WatermarkTextFontSize;
    }
}
