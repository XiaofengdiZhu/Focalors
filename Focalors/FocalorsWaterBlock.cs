using System.Collections.Generic;
using Engine;
using Engine.Graphics;

namespace Game {
    public class FocalorsWaterBlock : FocalorsFluidBlock {
        public const int Index = 798;

        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z) {
            Color sideColor;
            Color topColor = sideColor = BlockColorsMap.WaterColorsMap.Lookup(generator.Terrain, x, y, z);
            sideColor.A = byte.MaxValue;
            topColor.A = 0;
            GenerateFluidTerrainVertices(
                generator,
                value,
                x,
                y,
                z,
                sideColor,
                topColor,
                geometry.TransparentSubsetsByFace
            );
        }

        public override IEnumerable<int> GetCreativeValues() {
            for (int i = 0; i < 8; i++) {
                yield return Terrain.MakeBlockValue(BlockIndex, 0, SetIsTop(SetLevel(0, i / 8f), true));
            }
        }

        public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData) {
            DrawFocalorsFluidBlock(
                primitivesRenderer,
                value,
                color * BlockColorsMap.WaterColorsMap.Lookup(12, 12),
                size,
                ref matrix,
                environmentData
            );
        }

        public override int GetEmittedLightAmount(int value) => 8 + (int)((1 - GetLevel(value)) * 7);
    }
}