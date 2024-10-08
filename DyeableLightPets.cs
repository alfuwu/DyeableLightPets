using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.Reflection;
using Terraria;
using Terraria.GameContent;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace DyeableLightPets;

public class DyeableLightPets : Mod {

}

public class DyeableLightPet : GlobalProjectile {
    static RenderTarget2D target = null;
    Color previousColor;

    public override bool InstancePerEntity => true;

    public override bool AppliesToEntity(Projectile proj, bool lateInstantiation) => ProjectileID.Sets.LightPet[proj.type];

    public override void Load() {
        IL_Projectile.AI_026 += AI_026;
        IL_Projectile.AI_067_FreakingPirates += AI_067_FreakingPirates;
        IL_Projectile.AI_144_DD2Pet += AI_144_DD2Pet;
        IL_Projectile.ProjLight += ProjLight;
        IL_Projectile.VanillaAI += VanillaAI;

        On_Lighting.AddLight_Vector2_float_float_float += OnAddLight;
    }

    public override void Unload() {
        IL_Projectile.AI_026 -= AI_026;
        IL_Projectile.AI_067_FreakingPirates -= AI_067_FreakingPirates;
        IL_Projectile.AI_144_DD2Pet -= AI_144_DD2Pet;
        IL_Projectile.ProjLight -= ProjLight;
        IL_Projectile.VanillaAI -= VanillaAI;

        On_Lighting.AddLight_Vector2_float_float_float -= OnAddLight;
    }

    private static Color GetDyeColor(Projectile proj, int dyeID, float r, float g, float b) { // aint no way this is the optimal way to do this
        int t = Main.player[proj.owner].miscDyes[1].type;
        ref Color prev = ref proj.GetGlobalProjectile<DyeableLightPet>().previousColor; // linear interpolation for smoother colors
        Color outputColor;
        // we do a lil hardcoding
        if (Main.player[proj.owner].miscDyes[1].type >= ItemID.RedDye && Main.player[proj.owner].miscDyes[1].type <= ItemID.PinkandBlackDye || Main.player[proj.owner].miscDyes[1].type >= ItemID.BrightRedDye && Main.player[proj.owner].miscDyes[1].type <= ItemID.BlackDye) {
            outputColor = new(GameShaders.Armor.GetShaderFromItemId(t).Shader.Parameters["uColor"].GetValueVector3());
        } else {
            target ??= new(Main.graphics.GraphicsDevice, 1, 1); // instantiate render target if it's null
            Main.graphics.GraphicsDevice.SetRenderTarget(target); // set render target
            Main.graphics.GraphicsDevice.Clear(Color.Transparent); // clear image

            // begin spritebatch
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, null, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            GameShaders.Armor.Apply(dyeID, proj); // apply armor shader
            Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, Vector2.Zero, Color.White); // draw white to the spritebatch
            Main.spriteBatch.End(); // end spritebatch
            Main.graphics.GraphicsDevice.SetRenderTarget(null); // clear render target

            Color[] pixelData = new Color[1]; // output array
            target.GetData(pixelData); // populate output array
            outputColor = pixelData[0]; // get color of the pixel we applied the armor shader to
        }
        //if (prev.A <= 0)
        //    prev = new Color(r, g, b);
        if (prev != outputColor && prev.A > 0)
            outputColor = Color.Lerp(prev, outputColor, 0.05f);
        prev = outputColor;
        return outputColor;
    }

    // scuffed mod projectile support
    private void OnAddLight(On_Lighting.orig_AddLight_Vector2_float_float_float orig, Vector2 position, float r, float g, float b) {
        foreach (Projectile proj in Main.projectile) {
            if (proj.Center == position && ProjectileID.Sets.LightPet[proj.type] && proj.ModProjectile != null && proj.active) {
                int dyeID = Main.player[proj.owner].miscDyes[1].dye;
                if (dyeID > 0) {
                    Color outputColor = GetDyeColor(proj, dyeID, r, g, b);
                    r = outputColor.R / 255f;
                    g = outputColor.G / 255f;
                    b = outputColor.B / 255f;
                }
                return;
            }
        }
        orig(position, r, g, b);
    }

    private void AI_026(ILContext il) {
        try {
            ILCursor c = new(il);
            c.GotoNext(MoveType.After, i => i.MatchLdarg0(),
                i => i.MatchLdfld<Projectile>("type"),
                i => i.MatchLdcI4(ProjectileID.GolemPet),
                i => i.MatchBneUn(out _),
                i => i.MatchLdsfld<Main>("player"),
                i => i.MatchLdarg0(),
                i => i.MatchLdfld<Projectile>("owner"),
                i => i.MatchLdelemRef(),
                i => i.MatchPop(),
                i => i.MatchLdcR4(out _),
                i => i.MatchLdcR4(out _),
                i => i.MatchLdcR4(out _),
                i => i.MatchNewobj<Vector3>());
            ILLabel vanilla = il.DefineLabel();
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate((Projectile proj) => ProjectileID.Sets.LightPet[proj.type]);
            c.Emit(OpCodes.Brfalse_S, vanilla);
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate((Vector3 vec, Projectile proj) => {
                int dyeID = Main.player[proj.owner].miscDyes[1].dye;
                return dyeID > 0 ? GetDyeColor(proj, dyeID, vec.X, vec.Y, vec.Z).ToVector3() : vec;
            });
            c.MarkLabel(vanilla);
        } catch (Exception e) {
            MonoModHooks.DumpIL(Mod, il);
            throw new ILPatchFailureException(Mod, il, e);
        }
    }

    private void AI_067_FreakingPirates(ILContext il) {
        try {
            ILCursor c = new(il);
            c.GotoNext(MoveType.After, i => i.MatchLdarg0(),
                i => i.MatchLdfld<Projectile>("type"),
                i => i.MatchLdcI4(ProjectileID.CrimsonHeart),
                i => i.MatchBneUn(out _),
                i => i.MatchLdarg0(),
                i => i.MatchCall<Entity>("get_Center"),
                i => i.MatchLdcR4(out float r),
                i => i.MatchLdcR4(out float g),
                i => i.MatchLdcR4(out float b));
            ILLabel vanilla = il.DefineLabel();
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate((Projectile proj) => ProjectileID.Sets.LightPet[proj.type]);
            c.Emit(OpCodes.Brfalse_S, vanilla);
            for (int i = 0; i < 3; i++)
                c.Emit(OpCodes.Pop); // pop the three floats off the stack
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate((Vector2 center, Projectile proj) => {
                int dyeID = Main.player[proj.owner].miscDyes[1].dye;
                if (dyeID > 0) {
                    Color col = GetDyeColor(proj, dyeID, 0.9f, 0.1f, 0.3f);
                    Lighting.AddLight(center, col.ToVector3());
                } else {
                    Lighting.AddLight(center, 0.9f, 0.1f, 0.3f);
                }
            });
            ILLabel skipAddLight = il.DefineLabel();
            c.Emit(OpCodes.Br_S, skipAddLight);
            c.MarkLabel(vanilla);
            c.GotoNext(MoveType.After, i => i.MatchCall<Lighting>("AddLight"));
            c.MarkLabel(skipAddLight);
        } catch (Exception e) {
            MonoModHooks.DumpIL(Mod, il);
            throw new ILPatchFailureException(Mod, il, e);
        }
    }

    private void AI_144_DD2Pet(ILContext il) {
        try {
            ILCursor c = new(il);
            c.GotoNext(MoveType.After, i => i.MatchLdcR4(out _),
                i => i.MatchLdcR4(out _),
                i => i.MatchLdcR4(out _),
                i => i.MatchNewobj<Vector3>());
            ILLabel vanilla = il.DefineLabel();
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate((Projectile proj) => ProjectileID.Sets.LightPet[proj.type]);
            c.Emit(OpCodes.Brfalse_S, vanilla);
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate((Vector3 vec, Projectile proj) => {
                int dyeID = Main.player[proj.owner].miscDyes[1].dye;
                return dyeID > 0 ? GetDyeColor(proj, dyeID, vec.X, vec.Y, vec.Z).ToVector3() : vec;
            });
            c.MarkLabel(vanilla);
        } catch (Exception e) {
            MonoModHooks.DumpIL(Mod, il);
            throw new ILPatchFailureException(Mod, il, e);
        }
    }

    private void ProjLight(ILContext il) {
        try {
            ILCursor c = new(il);
            c.Index = c.Instrs.Count - 1;
            for (int i = 0; i < 2; i++) {
                c.GotoPrev(i => i.MatchLdloc0(),
                    i => i.MatchLdloc1(),
                    i => i.MatchLdloc2());
                ILLabel vanilla = il.DefineLabel();
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate((Projectile proj) => ProjectileID.Sets.LightPet[proj.type]);
                c.Emit(OpCodes.Brfalse_S, vanilla);
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloca_S, (byte)0);
                c.Emit(OpCodes.Ldloca_S, (byte)1);
                c.Emit(OpCodes.Ldloca_S, (byte)2);
                c.EmitDelegate((Projectile proj, ref float r, ref float g, ref float b) => {
                    int dyeID = Main.player[proj.owner].miscDyes[1].dye; // light pet dye item
                    if (dyeID > 0) { // check if the item is a valid dye
                        Color outputColor = GetDyeColor(proj, dyeID, r, g, b);
                        r = outputColor.R / 255f; // normalize to float value
                        g = outputColor.G / 255f;
                        b = outputColor.B / 255f;
                    }
                });
                c.MarkLabel(vanilla);
            }
        } catch (Exception e) {
            MonoModHooks.DumpIL(Mod, il);
            throw new ILPatchFailureException(Mod, il, e);
        }
    }

    private void VanillaAI(ILContext il) {
        try {
            ILCursor c = new(il);
            c.GotoNext(MoveType.After, i => i.MatchLdarg0(),
                i => i.MatchLdfld<Projectile>("aiStyle"),
                i => i.MatchLdcI4(124));
            for (int i = 0; i < 3; i++) {
                c.GotoNext(MoveType.After, i => i.MatchNewobj<Vector3>());
                ILLabel vanilla = il.DefineLabel();
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate((Projectile proj) => ProjectileID.Sets.LightPet[proj.type]);
                c.Emit(OpCodes.Brfalse_S, vanilla);
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate((Vector3 vec, Projectile proj) => {
                    int dyeID = Main.player[proj.owner].miscDyes[1].dye;
                    return dyeID > 0 ? GetDyeColor(proj, dyeID, vec.X, vec.Y, vec.Z).ToVector3() : vec;
                });
                c.MarkLabel(vanilla);
            }
        } catch (Exception e) {
            MonoModHooks.DumpIL(Mod, il);
            throw new ILPatchFailureException(Mod, il, e);
        }
    }
}