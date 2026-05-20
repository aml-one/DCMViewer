using System.Windows.Controls;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;

namespace DCMViewer.Controls;

public partial class DcmViewerCanvasComponent : UserControl
{
    public DcmViewerCanvasComponent()
    {
        InitializeComponent();
    }

    public HelixViewport3D Viewport3D => Viewport;

    public CuttingPlaneGroup CuttingPlaneGroup => SectionCutGroup;

    public ModelVisual3D SectionPlaneModel => SectionPlaneVisual;

    public LinesVisual3D SectionPlaneOutline => SectionPlaneOutlineVisual;

    public LinesVisual3D MeasurementLineVisual => MeasurementLine;

    public BillboardTextVisual3D MeasurementTextVisual => MeasurementText;
}
