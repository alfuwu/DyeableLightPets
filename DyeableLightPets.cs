using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
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
        IL_Projectile.ProjLight += ProjLight;
        IL_Projectile.VanillaAI += VanillaAI;
    }

    public override void Unload() {
        IL_Projectile.ProjLight -= ProjLight;
        IL_Projectile.VanillaAI -= VanillaAI;
    }

    private static Color GetDyeColor(Projectile proj, int dyeID, float r, float g, float b) {
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
        if (prev.A <= 0)
            prev = new Color(r, g, b);
        if (prev != outputColor)
            outputColor = Color.Lerp(prev, outputColor, 0.05f);
        prev = outputColor;
        return outputColor;
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
                    if (dyeID > 0)
                        return GetDyeColor(proj, dyeID, vec.X, vec.Y, vec.Z).ToVector3();
                    return vec;
                });
                c.MarkLabel(vanilla);
            }
        } catch (Exception e) {
            MonoModHooks.DumpIL(Mod, il);
            throw new ILPatchFailureException(Mod, il, e);
        }
    }
}