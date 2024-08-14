#if TOOLS
using Godot;
using System.Collections.Generic;
using System.IO;

[Tool]
public partial class ExternalFolderExporter : EditorExportPlugin
{
    static List<string> excludeFiles = new()
    {
        ".gdignore",
        "Banjo",
    };

    public override void _ExportBegin(string[] features, bool isDebug, string path, uint flags)
    {
        string exportFolderPath = "res://"+path.Split("/")[..^1].Join("/");
        using (var projectExternalFolder = DirAccess.Open("res://External"))
        {
            if (projectExternalFolder is not null)
            {
                if (!DirAccess.DirExistsAbsolute(exportFolderPath + "/External"))
                    DirAccess.MakeDirAbsolute(exportFolderPath + "/External");
                projectExternalFolder.ListDirBegin();
                string currentEntry = projectExternalFolder.GetNext();
                while (currentEntry != "")
                {
                    if (!excludeFiles.Contains(currentEntry))
                    {
                        if (projectExternalFolder.CurrentIsDir())
                        {
                            CopyFolder("res://External/" + currentEntry, exportFolderPath + "/External/" + currentEntry);
                        }
                        else
                        {
                            DirAccess.CopyAbsolute("res://External/" + currentEntry, exportFolderPath + "/External/" + currentEntry);
                        }
                    }
                    currentEntry = projectExternalFolder.GetNext();
                }
            }
        }
    }

    static void CopyFolder(string fromPath, string toPath)
    {
        if (!DirAccess.DirExistsAbsolute(toPath))
            DirAccess.MakeDirAbsolute(toPath);
        using var fromFolder = DirAccess.Open(fromPath);
        fromFolder.ListDirBegin();
        string currentEntry = fromFolder.GetNext();
        while (currentEntry != "")
        {
            if (!excludeFiles.Contains(currentEntry))
            {
                if (fromFolder.CurrentIsDir())
                {
                    CopyFolder(fromPath + "/" + currentEntry, toPath + "/" + currentEntry);
                }
                else
                {
                    DirAccess.CopyAbsolute(fromPath + "/" + currentEntry, toPath + "/" + currentEntry);
                }
            }
            currentEntry = fromFolder.GetNext();
        }
    }
}
#endif
