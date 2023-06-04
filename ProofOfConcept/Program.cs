﻿// This program will be run from cmd as a proof of concept
// The first argument is the data.win location
// The second argument is the mod folder location

#region Using directives
using Microsoft.Extensions.Configuration;
using UndertaleModLib;
using UndertaleModLib.Models;
using UndertaleModLib.Compiler;
using System.Drawing;
using System.Collections;
using System.Linq;
#endregion

#region Fields
string gameDataPath = args[0];
string modsFolder = args[1];
string loaderConfigPath = $"{modsFolder}/loader.ini";
var loaderConfig = new ConfigurationBuilder().AddIniFile(loaderConfigPath).Build().GetSection("Loader");

Dictionary<string, int> moddedCodes = new Dictionary<string, int>();
Dictionary<string, int> moddedSprites = new Dictionary<string, int>();
Dictionary<string, int> moddedScripts = new Dictionary<string, int>();
List<ReplacedAssetInfo> replacedAssets = new List<ReplacedAssetInfo>();

string[] forbiddenFiles = {"data.win"};

UndertaleData gameData;
string gameFolderPath = Path.GetDirectoryName(gameDataPath)!;

#endregion

#region Program
Console.WriteLine("Loading game data...");

try
{
    using (var stream = new FileStream(gameDataPath, FileMode.Open, FileAccess.ReadWrite))
        gameData = UndertaleIO.Read(stream);
} catch 
{
    Console.WriteLine("Error reading game data");
    return;
}

Console.WriteLine($"Loaded game {gameData.GeneralInfo.Name.Content}");

foreach (string folder in Directory.GetDirectories(modsFolder))
{
    var modConfig = new ConfigurationBuilder().AddIniFile($"{folder}/mod.ini").Build().GetSection("Loader");
    var modPriority = Convert.ToInt32(modConfig["priority"]);

    if (!Convert.ToBoolean(modConfig["enabled"]) || modConfig["game"] != gameData.GeneralInfo.Name.Content)
        continue;

    /*
    if (modConfig["gameVersion"] != gameData.GeneralInfo.Release.ToString() && !Convert.ToBoolean(loaderConfig["ignoreVersionMismatch"]))
    {
        Console.WriteLine($"Mod version does not match game version, skipping {folder}.");
        continue;
    }
    */
    
    Console.WriteLine($"Loading mod {modConfig["name"]}");

    if (Path.Exists($"{folder}/sprites.ini"))
    {
        Console.WriteLine("Loading sprites...");
        var spritesIni = new ConfigurationBuilder().AddIniFile($"{folder}/sprites.ini").Build();
        foreach (IConfigurationSection sprite in spritesIni.GetChildren())
        {
            Console.WriteLine($"Loading sprite {sprite.Key}");
            ReplaceSprite(sprite, modPriority);
        }
    }
    
    if (Directory.Exists($"{folder}/Textures"))
    {
        Console.WriteLine("Loading texture...");
        foreach (string sprite in Directory.GetFiles($"{folder}/Textures").Where(x => !x.EndsWith(".ini")))
        {
            Console.WriteLine($"Loading texture {sprite}");
            ReplaceTexture(sprite, modPriority);
        }
    }

    if (Directory.Exists($"{folder}/Code"))
    {
        Console.WriteLine("Loading code...");
        foreach (string code in Directory.GetFiles($"{folder}/Code").Where(x => x.EndsWith(".gml")))
        {
            Console.WriteLine($"Loading code {code}");
            ReplaceCode(code, modPriority);
        }
    }

    if (Path.Exists($"{folder}/scripts.ini"))
    {
        Console.WriteLine("Loading scripts...");
        var scriptsIni = new ConfigurationBuilder().AddIniFile($"{folder}/scripts.ini").Build();
        foreach (IConfigurationSection pair in scriptsIni.GetSection("Scripts").GetChildren())
        {
            Console.WriteLine($"Loading script {pair.Key}");
            ReplaceScript(pair.Key, pair.Value!, false, modPriority);
        }
        foreach (IConfigurationSection pair in scriptsIni.GetSection("Constructors").GetChildren())
        {
            Console.WriteLine($"Loading global script {pair.Key}");
            ReplaceScript(pair.Key, pair.Value!, true, Convert.ToInt32(modConfig["priority"]));
        }
    }

    if (Path.Exists($"{folder}/globalInit.ini"))
    {
        Console.WriteLine("Adding global init scripts...");
        var scriptsIni = new ConfigurationBuilder().AddIniFile($"{folder}/globalInit.ini").Build();
        foreach (var section in scriptsIni.GetChildren())
            AddScriptToSequence(section.Key, gameData.GlobalInitScripts);
    }

    if (Path.Exists($"{folder}/gameEndScripts.ini"))
    {
        Console.WriteLine("Adding game end scripts...");
        var scriptsIni = new ConfigurationBuilder().AddIniFile($"{folder}/gameEndScripts.ini").Build();
        foreach (var section in scriptsIni.GetChildren())
            AddScriptToSequence(section.Key, gameData.GameEndScripts);
    }

    if (Path.Exists($"{folder}/objects.ini"))
    {
        Console.WriteLine("Modifying objects...");
        var objectsIni = new ConfigurationBuilder().AddIniFile($"{folder}/objects.ini").Build();
        foreach (IConfigurationSection objectSection in objectsIni.GetChildren())
        {
            Console.WriteLine($"Modifying object {objectSection.Key}");
            ModifyObject(objectSection, modPriority);
        }
    }

    if (Directory.Exists($"{folder}/ObjectPhysicsShapeVertices"))
    {
        Console.WriteLine("Modifying object physics shape vertices...");
        foreach (string objectIniPath in Directory.GetFiles($"{folder}/ObjectPhysicsShapeVertices").Where(x => x.EndsWith(".ini")))
        {
            string objectName = Path.GetFileNameWithoutExtension(objectIniPath);
            Console.WriteLine($"Modifying object {objectName}");
            var fileConfig = new ConfigurationBuilder().AddIniFile(objectIniPath).Build();
            ModifyObjectPhysicsShapeVertices(Path.GetFileNameWithoutExtension(objectName), fileConfig.GetSection("Remove"), true);
            ModifyObjectPhysicsShapeVertices(Path.GetFileNameWithoutExtension(objectName), fileConfig.GetSection("Add"), false);
        }
    }

    if (Directory.Exists($"{folder}/RemoveObjectEvents"))
    {
        Console.WriteLine("Removing object events...");
        foreach (string objectIniPath in Directory.GetFiles($"{folder}/RemoveObjectEvents").Where(x => x.EndsWith(".ini")))
        {
            string objectName = Path.GetFileNameWithoutExtension(objectIniPath);
            Console.WriteLine($"Modifying object {objectName}");
            var fileConfig = new ConfigurationBuilder().AddIniFile(objectIniPath).Build();
            ModifyObjectEvents(objectName, fileConfig, true);
        }
    }

    if (Directory.Exists($"{folder}/AddObjectEvents"))
    {
        Console.WriteLine("Adding object events...");
        foreach (string objectIniPath in Directory.GetFiles($"{folder}/AddObjectEvents").Where(x => x.EndsWith(".ini")))
        {
            string objectName = Path.GetFileNameWithoutExtension(objectIniPath);
            Console.WriteLine($"Modifying object {objectName}");
            var fileConfig = new ConfigurationBuilder().AddIniFile(objectIniPath).Build();
            ModifyObjectEvents(objectName, fileConfig, false);
        }
    }

    if (Directory.Exists($"{folder}/ExternalAssets") && Path.Exists($"{folder}/ExternalAssets/externalAssets.ini"))
    {
        Console.WriteLine("Copying external assets...");
        var iniFile = new ConfigurationBuilder().AddIniFile($"{folder}/ExternalAssets/externalAssets.ini").Build().GetSection("Locations");
        foreach (string file in Directory.GetFiles($"{folder}/ExternalAssets").Where(x => !x.EndsWith("externalAssets.ini")))
        {
            Console.WriteLine($"Copying {file}");
            string fileName = Path.GetFileName(file);
            string destination = $"{gameFolderPath}/{iniFile[fileName]!}{(iniFile[fileName]!.EndsWith("/") ? "" : "/")}{fileName}";
            if (destination.Contains("..") || destination.Contains("%") || destination.Contains(".exe") || destination.Contains(".dll"))
            {
                Console.WriteLine("Invalid destination, skipping");
                continue;
            }

            CopyExternalAsset(file, destination, modPriority);
        }
    }
}

using (var stream = new FileStream($"{gameFolderPath}/modded.win", FileMode.Create, FileAccess.ReadWrite))
    UndertaleIO.Write(stream, gameData);

Console.WriteLine("Done");

Console.ReadKey();
#endregion

#region Methods
bool IsAssetUnavailable(Type assetType, string assetName, int modPriority, string? property = null)
{
    bool isUnavailable = replacedAssets.Any(x => x.assetName == assetName && x.modPriority < modPriority && x.assetType == assetType && x.propertyName == property);

    if (isUnavailable)
        Console.WriteLine($"Asset {assetName} of type {assetType.Name} already replaced with a higher priority mod, pain ahead.");
    else
        replacedAssets.Add(new ReplacedAssetInfo() {
            assetName = assetName,
            modPriority = modPriority,
            assetType = assetType,
            propertyName = property
        });

    return isUnavailable;
}

void ReplaceCode(string codePath, int modPriority)
{
    string codeName = Path.GetFileNameWithoutExtension(codePath);

    if (IsAssetUnavailable(typeof(UndertaleCode), codeName, modPriority))
        return;

    UndertaleCode? codeToReplace = gameData.Code.FirstOrDefault((x => x!.Name.Content == codeName), null);

    if (codeToReplace is null)
    {
        Console.WriteLine($"Code {codeName} not found, creating new code.");
        codeToReplace = new UndertaleCode() {
            Name = new UndertaleString(codeName)
        };
        gameData.Code.Add(codeToReplace);
    }

    CompileContext context = Compiler.CompileGMLText(File.ReadAllText(codePath), gameData, codeToReplace);
    codeToReplace.Replace(context.ResultAssembly);

    // TODO: Fix this path not pointing correctly and adapt to single .ini model
    if (Path.Exists(Path.GetDirectoryName(codePath) + "/code.ini"))
    {
        IConfigurationSection fileConfig = new ConfigurationBuilder().AddIniFile(Path.GetDirectoryName(codePath) + "./code.ini").Build().GetSection(codeName);
        codeToReplace.LocalsCount = fileConfig["localsCount"] is not null ? Convert.ToUInt32(fileConfig["localsCount"]) : codeToReplace.LocalsCount;
        codeToReplace.ArgumentsCount = fileConfig["argumentsCount"] is not null ? Convert.ToUInt16(fileConfig["argumentsCount"]) : codeToReplace.ArgumentsCount;
        codeToReplace.Offset = fileConfig["offset"] is not null ? Convert.ToUInt32(fileConfig["offset"]) : codeToReplace.Offset;
    }
}

void ReplaceTexture(string texturePath, int modPriority)
{
    string textureName = Path.GetFileNameWithoutExtension(texturePath);
    string spriteName = textureName.Remove(textureName.LastIndexOf('_'));
    int textureIndex = Convert.ToInt32(textureName.Remove(0, textureName.LastIndexOf('_') + 1));

    if (IsAssetUnavailable(typeof(UndertaleSprite), spriteName, modPriority))
        return;

    UndertaleSprite? textureSprite = gameData.Sprites.FirstOrDefault((x => x!.Name.Content == spriteName), null);

    if (textureSprite is null)
    {
        Console.WriteLine("Sprite not found, creating new sprite...");
        textureSprite = new UndertaleSprite() {
            Name = new UndertaleString(spriteName)
        };
        gameData.Sprites.Add(textureSprite);
    }

    Image imageToUse;
    try {
        imageToUse = Image.FromFile(texturePath);
    } catch (Exception e) {
        Console.WriteLine($"Error replacing texture {textureName}: {e.Message}. Pain ahead.");
        return;
    }

    UndertaleTexturePageItem textureToReplace = textureSprite.Textures[textureIndex].Texture;

    ushort width = Convert.ToUInt16(imageToUse.Width);
    ushort height = Convert.ToUInt16(imageToUse.Height);

    if (textureToReplace.SourceWidth < width || textureToReplace.SourceHeight < height)
    {
        Console.WriteLine($"Texture {textureName} has different size than original, creating it's own embedded texture.");

        textureToReplace.SourceX = 0;
        textureToReplace.SourceY = 0;
        var embeddedTexture = new UndertaleEmbeddedTexture(){
            Name = new UndertaleString($"Texture {gameData.EmbeddedTextures.Count}"),
            TextureWidth = width,
            TextureHeight = height,
            Scaled = 1,
            GeneratedMips = 0,
            TextureData = new UndertaleEmbeddedTexture.TexData() {
                TextureBlob = File.ReadAllBytes(texturePath)
            }
        };

        gameData.EmbeddedTextures.Add(embeddedTexture);
        textureToReplace.TexturePage = embeddedTexture;

    }

    textureToReplace.SourceWidth = width;
    textureToReplace.TargetWidth = width;
    textureToReplace.SourceHeight = height;
    textureToReplace.TargetHeight = height;

    if (Path.Exists(Path.GetDirectoryName(texturePath) + "/texture.ini"))
    {
        var fileConfig = new ConfigurationBuilder().AddIniFile(Path.GetDirectoryName(texturePath) + "./textures.ini").Build().GetSection(textureName);
        textureToReplace.TargetX = fileConfig["targetX"] is not null ? Convert.ToUInt16(fileConfig["targetX"]) : textureToReplace.TargetX;
        textureToReplace.TargetY = fileConfig["targetY"] is not null ? Convert.ToUInt16(fileConfig["targetY"]) : textureToReplace.TargetY;
        textureToReplace.BoundingWidth = fileConfig["boundingWidth"] is not null ? Convert.ToUInt16(fileConfig["boundingWidth"]) : textureToReplace.BoundingWidth;
        textureToReplace.BoundingHeight = fileConfig["boundingHeight"] is not null ? Convert.ToUInt16(fileConfig["boundingHeight"]) : textureToReplace.BoundingHeight;
    }

    textureToReplace.ReplaceTexture(imageToUse);
}

void ReplaceScript(string scriptName, string codeName, bool isConstructor, int modPriority)
{
    if (IsAssetUnavailable(typeof(UndertaleScript), scriptName, modPriority))
        return;

    UndertaleCode codeToUse = gameData.Code.First(x => x.Name.Content == codeName);

    if (codeToUse is null)
    {
        Console.WriteLine($"Code {codeName} not found, skipping script {scriptName}, pain head.");
        return;
    }

    UndertaleScript? scriptToReplace = gameData.Scripts.FirstOrDefault((x => x!.Name.Content == scriptName), null);

    if (scriptToReplace is null)
    {
        Console.WriteLine($"Script {scriptName} not found, creating new script.");
        scriptToReplace = new UndertaleScript() {
            Name = new UndertaleString(scriptName),
            IsConstructor = isConstructor
        };
        gameData.Scripts.Add(scriptToReplace);
    }

    scriptToReplace.Code = codeToUse;
}

void ReplaceSprite(IConfigurationSection section, int modPriority)
{
    string spriteName = section.Key;

    if (IsAssetUnavailable(typeof(UndertaleSprite), spriteName, modPriority))
        return;
    
    UndertaleSprite? spriteToReplace = gameData.Sprites.FirstOrDefault((x => x!.Name.Content == spriteName), null);

    if (spriteToReplace is null)
    {
        Console.WriteLine($"Sprite {spriteName} not found, creating new sprite.");
        spriteToReplace = new UndertaleSprite() {
            Name = new UndertaleString(spriteName)
        };
        gameData.Sprites.Add(spriteToReplace);
    }

    spriteToReplace.Width = section["width"] is not null ? Convert.ToUInt32(section["width"]) : spriteToReplace.Width;
    spriteToReplace.Height = section["height"] is not null ? Convert.ToUInt32(section["height"]) : spriteToReplace.Height;

    spriteToReplace.MarginLeft = section["marginLeft"] is not null ? Convert.ToInt32(section["marginLeft"]) : spriteToReplace.MarginLeft;
    spriteToReplace.MarginRight = section["marginRight"] is not null ? Convert.ToInt32(section["marginRight"]) : spriteToReplace.MarginRight;
    spriteToReplace.MarginTop = section["marginTop"] is not null ? Convert.ToInt32(section["marginTop"]) : spriteToReplace.MarginTop;
    spriteToReplace.MarginBottom = section["marginBottom"] is not null ? Convert.ToInt32(section["marginBottom"]) : spriteToReplace.MarginBottom;

    spriteToReplace.Transparent = section["transparent"] is not null ? Convert.ToBoolean(section["transparent"]) : spriteToReplace.Transparent;
    spriteToReplace.Smooth = section["smooth"] is not null ? Convert.ToBoolean(section["smooth"]) : spriteToReplace.Smooth;
    spriteToReplace.Preload = section["preload"] is not null ? Convert.ToBoolean(section["preload"]) : spriteToReplace.Preload;

    spriteToReplace.BBoxMode = section["bboxMode"] is not null ? Convert.ToUInt32(section["bboxMode"]) : spriteToReplace.BBoxMode;

    spriteToReplace.SepMasks = section["sepMasks"] is not null ? (UndertaleSprite.SepMaskType)Convert.ToUInt32(section["sepMasks"]) : spriteToReplace.SepMasks;

    spriteToReplace.OriginX = section["originX"] is not null ? Convert.ToInt32(section["originX"]) : spriteToReplace.OriginX;
    spriteToReplace.OriginY = section["originY"] is not null ? Convert.ToInt32(section["originY"]) : spriteToReplace.OriginY;

    //spriteToReplace.CollisionMasks = section["collisionMasks"] is not null ? gameData.Sprites.First(x => x.Name.Content == section["collisionMasks"]).CollisionMasks : spriteToReplace.CollisionMasks;

    spriteToReplace.IsSpecialType = section["isSpecialType"] is not null ? Convert.ToBoolean(section["isSpecialType"]) : spriteToReplace.IsSpecialType;

    spriteToReplace.SVersion = section["version"] is not null ? Convert.ToUInt32(section["version"]) : spriteToReplace.SVersion;
    spriteToReplace.SSpriteType = section["spriteType"] is not null ? (UndertaleSprite.SpriteType)Convert.ToUInt16(section["spriteType"]) : spriteToReplace.SSpriteType;

    spriteToReplace.GMS2PlaybackSpeed = section["gms2PlaybackSpeed"] is not null ? Convert.ToSingle(section["gms2PlaybackSpeed"]) : spriteToReplace.GMS2PlaybackSpeed;
    spriteToReplace.GMS2PlaybackSpeedType = section["gms2PlaybackSpeedType"] is not null ? (AnimSpeedType)Convert.ToUInt32(section["gms2PlaybackSpeedType"]) : spriteToReplace.GMS2PlaybackSpeedType;
}

/*
void ModifyRoomValues()
{

}

void ReplaceTiles()
{
    
}

void ModifyInstances()
{

}
*/

void ModifyObject(IConfigurationSection section, int modPriority)
{
    string objectName = section.Key;

    if (IsAssetUnavailable(typeof(UndertaleObject), objectName, modPriority))
        return;

    UndertaleGameObject? objectToModify = gameData.GameObjects.FirstOrDefault((x => x!.Name.Content == objectName), null);

    if (objectToModify is null)
    {
        Console.WriteLine($"Object {objectName} not found, skipping object, pain head.");
        return;
    }

    objectToModify.Sprite = section["sprite"] is not null ? gameData.Sprites.First(x => x.Name.Content == section["sprite"]) : objectToModify.Sprite;
    objectToModify.Visible = section["visible"] is not null ? Convert.ToBoolean(section["visible"]) : objectToModify.Visible;
    objectToModify.Solid = section["solid"] is not null ? Convert.ToBoolean(section["solid"]) : objectToModify.Solid;
    objectToModify.Persistent = section["persistent"] is not null ? Convert.ToBoolean(section["persistent"]) : objectToModify.Persistent;
    objectToModify.ParentId = section["parentId"] is not null ? gameData.GameObjects.First(x => x.Name.Content == section["parentId"]) : objectToModify.ParentId;
    objectToModify.TextureMaskId = section["textureMaskId"] is not null ? gameData.Sprites.First(x => x.Name.Content == section["textureMaskId"]) : objectToModify.TextureMaskId;
    objectToModify.UsesPhysics = section["usesPhysics"] is not null ? Convert.ToBoolean(section["usesPhysics"]) : objectToModify.UsesPhysics;
    objectToModify.IsSensor = section["isSensor"] is not null ? Convert.ToBoolean(section["isSensor"]) : objectToModify.IsSensor;
    objectToModify.CollisionShape = section["collisionShape"] is not null ? (CollisionShapeFlags)Convert.ToUInt16(section["collisionShape"]) : objectToModify.CollisionShape;
    objectToModify.Density = section["density"] is not null ? Convert.ToSingle(section["density"]) : objectToModify.Density;
    objectToModify.Restitution = section["restitution"] is not null ? Convert.ToSingle(section["restitution"]) : objectToModify.Restitution;
    objectToModify.Group = section["group"] is not null ? Convert.ToUInt32(section["group"]) : objectToModify.Group;
    objectToModify.LinearDamping = section["linearDamping"] is not null ? Convert.ToSingle(section["linearDamping"]) : objectToModify.LinearDamping;
    objectToModify.AngularDamping = section["angularDamping"] is not null ? Convert.ToSingle(section["angularDamping"]) : objectToModify.AngularDamping;
    objectToModify.Friction = section["friction"] is not null ? Convert.ToSingle(section["friction"]) : objectToModify.Friction;
    objectToModify.Awake = section["isAwake"] is not null ? Convert.ToBoolean(section["isAwake"]) : objectToModify.Awake;
    objectToModify.Kinematic = section["isKinematic"] is not null ? Convert.ToBoolean(section["isKinematic"]) : objectToModify.Kinematic;
}

void ModifyObjectPhysicsShapeVertices(string objectName, IConfigurationSection section, bool isRemove)
{
    var objectToModify = gameData.GameObjects.First(x => x.Name.Content == objectName);

    if (objectToModify is null)
    {
        Console.WriteLine($"Object {objectName} not found, skipping object, pain head.");
        return;
    }

    foreach (var pair in section.GetChildren())
    {
        if (isRemove)
            objectToModify.PhysicsVertices.Remove(objectToModify.PhysicsVertices.First(x => x.X == Convert.ToSingle(pair.Key) && x.Y == Convert.ToSingle(pair.Value)));
        else
            objectToModify.PhysicsVertices.Add(new UndertaleGameObject.UndertalePhysicsVertex(){
                X = Convert.ToSingle(pair.Key),
                Y = Convert.ToSingle(pair.Value)
            });
    }
}

void ModifyObjectEvents(string objectName, IConfigurationRoot section, bool isRemove)
{
    var objectWhosEventsAreWishedToBeModified = gameData.GameObjects.First(x => x.Name.Content == objectName);

    if (objectWhosEventsAreWishedToBeModified is null)
    {
        Console.WriteLine($"Object {objectName} not found, skipping object, pain head.");
        return;
    }

    foreach (var eventSection in section.GetChildren())
    {
        var eventToModify = objectWhosEventsAreWishedToBeModified.Events[Convert.ToInt32(eventSection.Key)];

        if (eventToModify is null)
        {
            Console.WriteLine($"Event {eventSection.Key} not found, skipping event, pain head.");
            continue;
        }

        foreach (var subtypeSections in eventSection.GetChildren())
        {
            if (subtypeSections.Value is null)
                continue;

            if (isRemove)
            {
                eventToModify.Remove(eventToModify.First(x => x.EventSubtype == Convert.ToUInt16(subtypeSections.Key) && x.Actions[0].CodeId.Name.Content == subtypeSections.Value));
                continue;
            }

            var subtypeToModify = new UndertaleGameObject.Event(){
                EventSubtype = Convert.ToUInt16(subtypeSections.Key)
            };

            subtypeToModify.Actions.Add(new UndertaleGameObject.EventAction(){
                CodeId = gameData.Code.First(x => x.Name.Content == subtypeSections.Value)
            });

            eventToModify.Add(subtypeToModify);
        }
    }
}

void AddScriptToSequence(string codeName, IList<UndertaleGlobalInit> list)
{
    var codeToAdd = gameData.Code.First(x => x.Name.Content == codeName);

    if (codeToAdd is null)
    {
        Console.WriteLine($"Code {codeName} not found, skipping script, pain head.");
        return;
    }

    list.Add(new UndertaleGlobalInit(){
        Code = codeToAdd
    });
}

void CopyExternalAsset(string fileToCopyPath, string destinationPath, int modPriority)
{
    if (IsAssetUnavailable(typeof(File), fileToCopyPath, modPriority))
        return;

    File.Copy(fileToCopyPath, destinationPath, true);
}
#endregion