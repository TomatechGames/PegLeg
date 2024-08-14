#if TOOLS
using Godot;
using System.Collections.Generic;

[Tool]
public partial class ExternalFolderExporterPlugin : EditorPlugin
{
    ExternalFolderExporter exporter = new();
    public override void _EnterTree()
    {
        AddExportPlugin(exporter);
    }
    public override void _ExitTree()
    {
        RemoveExportPlugin(exporter);
    }
}
#endif
