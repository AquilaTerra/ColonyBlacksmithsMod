﻿using System;
using System.IO;
using System.Collections.Generic;
using Pipliz;
using Pipliz.Chatting;
using Pipliz.JSON;
using Pipliz.Threading;
using Pipliz.APIProvider.Recipes;
using Pipliz.APIProvider.Jobs;
using NPC;

namespace ScarabolMods
{
  [ModLoader.ModManager]
  public static class BlacksmithsModEntries
  {
    public static string MOD_PREFIX = "mods.scarabol.blacksmiths.";
    public static string JOB_ITEM_KEY = MOD_PREFIX + "anvil";
    public static string JOB_TOOL_KEY = MOD_PREFIX + "sledge";
    private static string AssetsDirectory;
    private static string RelativeTexturesPath;
    private static string RelativeIconsPath;
    private static string RelativeMeshesPath;
    private static Recipe sledgeRecipe;

    [ModLoader.ModCallback(ModLoader.EModCallbackType.OnAssemblyLoaded, "scarabol.blacksmiths.assemblyload")]
    public static void OnAssemblyLoaded(string path)
    {
      AssetsDirectory = Path.Combine(Path.GetDirectoryName(path), "assets");
      ModLocalizationHelper.localize(Path.Combine(AssetsDirectory, "localization"), MOD_PREFIX, false);
      // TODO this is really hacky (maybe better in future ModAPI)
      RelativeTexturesPath = new Uri(MultiPath.Combine(Path.GetFullPath("gamedata"), "textures", "materials", "blocks", "albedo", "dummyfile")).MakeRelativeUri(new Uri(MultiPath.Combine(AssetsDirectory, "textures", "albedo"))).OriginalString;
      RelativeIconsPath = new Uri(MultiPath.Combine(Path.GetFullPath("gamedata"), "textures", "icons", "dummyfile")).MakeRelativeUri(new Uri(Path.Combine(AssetsDirectory, "icons"))).OriginalString;
      RelativeMeshesPath = new Uri(MultiPath.Combine(Path.GetFullPath("gamedata"), "meshes", "dummyfile")).MakeRelativeUri(new Uri(Path.Combine(AssetsDirectory, "meshes"))).OriginalString;
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterStartup, "scarabol.blacksmiths.registercallbacks")]
    public static void AfterStartup()
    {
      Pipliz.Log.Write("Loaded Blacksmiths Mod 1.1 by Scarabol");
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterDefiningNPCTypes, "scarabol.blacksmiths.registerjobs")]
    [ModLoader.ModCallbackProvidesFor("pipliz.apiprovider.jobs.resolvetypes")]
    public static void AfterDefiningNPCTypes()
    {
      BlockJobManagerTracker.Register<BlacksmithJob>(JOB_ITEM_KEY);
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterAddingBaseTypes, "scarabol.blacksmiths.addrawtypes")]
    public static void AfterAddingBaseTypes()
    {
      ItemTypesServer.AddTextureMapping(JOB_ITEM_KEY, new JSONNode()
        .SetAs("albedo", Path.Combine(RelativeTexturesPath, "anvil"))
        .SetAs("normal", "neutral")
        .SetAs("emissive", "neutral")
        .SetAs("height", "neutral")
      );
      ItemTypes.AddRawType(JOB_ITEM_KEY, new JSONNode(NodeType.Object)
                           .SetAs("npcLimit", 0)
                           .SetAs("icon", Path.Combine(RelativeIconsPath, "anvil.png"))
                           .SetAs("sideall", "SELF")
                           .SetAs("isRotatable", true)
                           .SetAs("rotatablex+", JOB_ITEM_KEY + "x+")
                           .SetAs("rotatablex-", JOB_ITEM_KEY + "x-")
                           .SetAs("rotatablez+", JOB_ITEM_KEY + "z+")
                           .SetAs("rotatablez-", JOB_ITEM_KEY + "z-")
      );
      foreach (string xz in new string[] { "x+", "x-", "z+", "z-" }) {
        ItemTypes.AddRawType(JOB_ITEM_KEY + xz, new JSONNode(NodeType.Object)
                             .SetAs("parentType", JOB_ITEM_KEY)
                             .SetAs("mesh", Path.Combine(RelativeMeshesPath, "anvil" + xz + ".obj"))
        );
      }
      ItemTypes.AddRawType(JOB_TOOL_KEY,
        new JSONNode(NodeType.Object)
          .SetAs<int>("npcLimit", 1)
          .SetAs("icon", Path.Combine(RelativeIconsPath, "sledge.png"))
          .SetAs<bool>("isPlaceable", false)
      );
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterItemTypesDefined, "scarabol.blacksmiths.loadrecipes")]
    [ModLoader.ModCallbackDependsOn("pipliz.blocknpcs.loadrecipes")]
    [ModLoader.ModCallbackProvidesFor("pipliz.apiprovider.registerrecipes")]
    public static void AfterItemTypesDefined()
    {
      sledgeRecipe = new Recipe(new JSONNode()
        .SetAs("results", new JSONNode(NodeType.Array).AddToArray(new JSONNode().SetAs("type", JOB_TOOL_KEY)))
        .SetAs("requires", new JSONNode(NodeType.Array).AddToArray(new JSONNode().SetAs("type", "ironingot"))));
      RecipeManager.AddRecipes("pipliz.crafter", new List<Recipe>() { sledgeRecipe });
      List<Recipe> blacksmithRecipes = new List<Recipe>();
      Recipe[] crafterRecipes;
      if (RecipeManager.RecipeStorage.TryGetValue("pipliz.crafter", out crafterRecipes)) {
        ushort ironingotType = ItemTypes.IndexLookup.GetIndex("ironingot");
        foreach (Recipe recipe in crafterRecipes) {
          int ironNeeded = 0;
          foreach (InventoryItem ingredient in recipe.Requirements) {
            if (ingredient.Type == ironingotType) {
              ironNeeded++;
            } else {
              ironNeeded--;
            }
          }
          if (ironNeeded >= 0) {
            blacksmithRecipes.Add(recipe);
          }
        }
      } else {
        Pipliz.Log.WriteError("Could not find any crafter recipes for blacksmiths");
      }
      RecipeManager.AddRecipes("scarabol.blacksmith", blacksmithRecipes);
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterWorldLoad, "scarabol.blacksmiths.addplayercrafts")]
    public static void AfterWorldLoad()
    {
      // add recipes here, otherwise they're inserted before vanilla recipes in player crafts
      RecipePlayer.AllRecipes.Add(new Recipe(new JSONNode()
        .SetAs("results", new JSONNode(NodeType.Array).AddToArray(new JSONNode().SetAs("type", JOB_ITEM_KEY)))
        .SetAs("requires", new JSONNode(NodeType.Array).AddToArray(new JSONNode().SetAs("type", "ironingot").SetAs("amount", 4)))));
      RecipePlayer.AllRecipes.Add(sledgeRecipe);
    }
  }

  public class BlacksmithJob : CraftingJobBase, IBlockJobBase, INPCTypeDefiner
  {
    public override string NPCTypeKey { get { return "scarabol.blacksmith"; } }

    public override float TimeBetweenJobs { get { return 4f; } }

    public override InventoryItem RecruitementItem { get { return new InventoryItem(ItemTypes.IndexLookup.GetIndex(BlacksmithsModEntries.JOB_TOOL_KEY), 1); } }

    public override int MaxRecipeCraftsPerHaul { get { return 5; } }

    public override List<string> GetCraftingLimitsTriggers ()
    {
      return new List<string>()
      {
        BlacksmithsModEntries.JOB_ITEM_KEY + "x+",
        BlacksmithsModEntries.JOB_ITEM_KEY + "x-",
        BlacksmithsModEntries.JOB_ITEM_KEY + "z+",
        BlacksmithsModEntries.JOB_ITEM_KEY + "z-"
      };
    }

    NPCTypeSettings INPCTypeDefiner.GetNPCTypeDefinition ()
    {
      NPCTypeSettings def = NPCTypeSettings.Default;
      def.keyName = NPCTypeKey;
      def.printName = "Blacksmith";
      def.maskColor1 = new UnityEngine.Color32(0, 0, 0, 255);
      def.type = NPCTypeID.GetNextID();
      return def;
    }
  }

}
