﻿#region Using Directives
using System.Linq;
using UndertaleModLib;
using UndertaleModLib.Models;
using UndertaleModLib.Decompiler;
using UndertaleModLib.Util;
#endregion

#region Fields
string ogGameDataPath = args[0];
string moddedGameDataPath = args[1];
string exportPath = args[2]; // .gmmod folder
Scripts.ExportFolder = exportPath;

UndertaleData ogGameData;
UndertaleData moddedGameData;
#endregion

#region Program

Console.WriteLine("Reading original game data...");
using (FileStream stream = new FileStream(ogGameDataPath, FileMode.Open))
    ogGameData = UndertaleIO.Read(stream);
Console.WriteLine("Reading modded game data...");
using (FileStream stream = new FileStream(moddedGameDataPath, FileMode.Open))
    moddedGameData = UndertaleIO.Read(stream);
Scripts.Data = moddedGameData;

#region Code
Console.WriteLine("Exporting code...");
for (int i = 0; i < ogGameData.Code.Count; i++)
{
    if (ogGameData.Code[i].ParentEntry != null)
        continue;

    string ogAsm = (ogGameData.Code[i] != null ? ogGameData.Code[i].Disassemble(ogGameData.Variables, ogGameData.CodeLocals.For(ogGameData.Code[i])) : "");
    string moddedAsm = (moddedGameData.Code[i] != null ? moddedGameData.Code[i].Disassemble(moddedGameData.Variables, moddedGameData.CodeLocals.For(moddedGameData.Code[i])) : "");
    
    if (ogAsm == moddedAsm)
        continue;

    Console.WriteLine("Exporting " + moddedGameData.Code[i].Name.Content + ".asm...");
    
    //Scripts.ExportASM(moddedGameData.Code[i]);
    Scripts.ExportASM(moddedAsm, moddedGameData.Code[i].Name.Content);
}
for (int i = ogGameData.Code.Count; i < moddedGameData.Code.Count; i++)
    if (moddedGameData.Code[i].ParentEntry is null)
    {
        Console.WriteLine("Exporting " + moddedGameData.Code[i].Name.Content + ".asm...");
        Scripts.ExportASM(moddedGameData.Code[i]);
    }
#endregion

#region Textures
TextureWorker worker = new();
Dictionary<string, bool> checkedPages = new(); // OIA momento
// Sprites
Console.WriteLine("Exporting sprite textures...");
for (int i = 0; i < ogGameData.Sprites.Count; i++)
{
    if (ogGameData.Sprites[i].Textures.Count == moddedGameData.Sprites[i].Textures.Count)
    {
        bool export = false;
        for (int j = 0; j < ogGameData.Sprites[i].Textures.Count; j++)
        {
            var ogTexture = ogGameData.Sprites[i].Textures[j].Texture;
            var moddedTexture = moddedGameData.Sprites[i].Textures[j].Texture;

            if (ogTexture.TexturePage.Name.Content != moddedTexture.TexturePage.Name.Content)
            {
                export = true;
                break;
            }

            if (!checkedPages.ContainsKey(moddedTexture.TexturePage.Name.Content))
            {
                // TODO: compare pages
            }

            if (checkedPages[moddedTexture.TexturePage.Name.Content])
                break;

            if (!Scripts.TextureEquals(ogTexture, moddedTexture))
            {
                export = true;
                break;
            }
        }
        if (!export)
            continue;
    }

    Console.WriteLine("Exporting " + moddedGameData.Sprites[i].Name.Content + "...");
    Scripts.DumpSprite(moddedGameData.Sprites[i]);
}
for (int i = ogGameData.Sprites.Count; i < moddedGameData.Sprites.Count; i++)
{
    Console.WriteLine("Exporting " + moddedGameData.Sprites[i].Name.Content + "...");
    Scripts.DumpSprite(moddedGameData.Sprites[i]);
}

// Fonts
Console.WriteLine("Exporting font textures...");
for (int i = 0; i < ogGameData.Fonts.Count; i++)
{
    if (Scripts.TextureEquals(ogGameData.Fonts[i].Texture, moddedGameData.Fonts[i].Texture))
        continue;
    
    Console.WriteLine("Exporting " + moddedGameData.Fonts[i].Name.Content + "...");
    Scripts.DumpFont(moddedGameData.Fonts[i]);
}
for (int i = ogGameData.Fonts.Count; i < moddedGameData.Fonts.Count; i++)
{
    Console.WriteLine("Exporting " + moddedGameData.Fonts[i].Name.Content + "...");
    Scripts.DumpFont(moddedGameData.Fonts[i]);
}

// Backgrounds
Console.WriteLine("Exporting background textures...");
for (int i = 0; i < ogGameData.Backgrounds.Count; i++)
{
    if (Scripts.TextureEquals(ogGameData.Backgrounds[i].Texture, moddedGameData.Backgrounds[i].Texture))
        continue;
    
    Console.WriteLine("Exporting " + moddedGameData.Backgrounds[i].Name.Content + "...");
    Scripts.DumpBackground(moddedGameData.Backgrounds[i]);
}
for (int i = ogGameData.Backgrounds.Count; i < moddedGameData.Backgrounds.Count; i++)
{
    Console.WriteLine("Exporting " + moddedGameData.Backgrounds[i].Name.Content + "...");
    Scripts.DumpBackground(moddedGameData.Backgrounds[i]);
}
#endregion

Console.WriteLine("Done!");
Console.ReadKey();
#endregion