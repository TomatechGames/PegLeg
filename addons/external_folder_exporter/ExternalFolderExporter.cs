#if TOOLS
using Godot;
using System.Collections.Generic;
using System.IO;

[Tool]
public partial class ExternalFolderExporter : EditorExportPlugin
{
    static List<string> excludeFiles =
    [
        ".gdignore",
        "Backup",
    ];

    public override string _GetName() => "ExternalExporter";

    //this class will soon be completely unneccecary
    public override void _ExportBegin(string[] features, bool isDebug, string path, uint flags)
    {
        string exportFolderPath = "res://"+path.Split("/")[..^1].Join("/");
        GD.Print($"Exporting resources to \"{exportFolderPath + "/PegLegResources.pck"}\"");
        var err = DirAccess.CopyAbsolute("res://PegLegResources.pck", exportFolderPath + "/PegLegResources.pck");
        if (err != Error.Ok)
            GD.Print(err);
        //CopyFolder("res://External", exportFolderPath + "/External");
    }

    static void CopyFolder(string fromPath, string toPath)
    {
        if (!DirAccess.DirExistsAbsolute(toPath))
            DirAccess.MakeDirAbsolute(toPath);
        using var fromFolder = DirAccess.Open(fromPath);
        if (fromFolder is null)
        {
            GD.PushWarning($"missing folder: \"{fromPath}\"");
            return;
        }
        //GD.Print($"copying \"{fromPath}\"");
        fromFolder.ListDirBegin();
        string currentEntry = fromFolder.GetNext();
        while (currentEntry != "")
        {
            if (!excludeFiles.Contains(currentEntry))
            {
                if (fromFolder.CurrentIsDir())
                {
                    //GD.Print($"\"{currentEntry}\" is folder");
                    CopyFolder(fromPath + "/" + currentEntry, toPath + "/" + currentEntry);
                }
                else
                {
                    //GD.Print($"\"{currentEntry}\" is file");
                    DirAccess.CopyAbsolute(fromPath + "/" + currentEntry, toPath + "/" + currentEntry);
                }
            }
            currentEntry = fromFolder.GetNext();
        }
    }
}
#endif
