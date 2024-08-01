using System;
using Engine;
using Engine.Graphics;

namespace Game {
    public abstract class FocalorsFluidBlock : CubeBlock {
        public readonly BoundingBox[][] m_boundingBoxesByLevel = new BoundingBox[16][];

        public FocalorsFluidBlock() {
            for (int i = 0; i < 16; i++) {
                float num = 0.999f * MathUtils.Saturate(1f - i / 16f);
                m_boundingBoxesByLevel[i] = [new BoundingBox(new Vector3(0f, 0f, 0f), new Vector3(1f, num, 1f))];
            }
        }

        public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value) => m_boundingBoxesByLevel[(int)(GetLevel(Terrain.ExtractData(value)) * 15f)];

        public static float GetLevelHeight(float level) => 1f - level;

        public void GenerateFluidTerrainVertices(BlockGeometryGenerator generator, int value, int x, int y, int z, Color sideColor, Color topColor, TerrainGeometrySubset[] subset) {
            int data = Terrain.ExtractData(value);
            if (GetIsTop(data)) {
                Terrain terrain = generator.Terrain;
                int cellValueFast = terrain.GetCellValueFast(x - 1, y, z - 1);
                int cellValueFast2 = terrain.GetCellValueFast(x, y, z - 1);
                int cellValueFast3 = terrain.GetCellValueFast(x + 1, y, z - 1);
                int cellValueFast4 = terrain.GetCellValueFast(x - 1, y, z);
                int cellValueFast5 = terrain.GetCellValueFast(x + 1, y, z);
                int cellValueFast6 = terrain.GetCellValueFast(x - 1, y, z + 1);
                int cellValueFast7 = terrain.GetCellValueFast(x, y, z + 1);
                int cellValueFast8 = terrain.GetCellValueFast(x + 1, y, z + 1);
                float h = CalculateNeighborHeight(cellValueFast);
                float num = CalculateNeighborHeight(cellValueFast2);
                float h2 = CalculateNeighborHeight(cellValueFast3);
                float num2 = CalculateNeighborHeight(cellValueFast4);
                float num3 = CalculateNeighborHeight(cellValueFast5);
                float h3 = CalculateNeighborHeight(cellValueFast6);
                float num4 = CalculateNeighborHeight(cellValueFast7);
                float h4 = CalculateNeighborHeight(cellValueFast8);
                float levelHeight = GetLevelHeight(GetLevel(data));
                float height = CalculateFluidVertexHeight(h, num, num2, levelHeight);
                float height2 = CalculateFluidVertexHeight(num, h2, levelHeight, num3);
                float height3 = CalculateFluidVertexHeight(levelHeight, num3, num4, h4);
                float height4 = CalculateFluidVertexHeight(num2, levelHeight, h3, num4);
                float x2 = ZeroSubst(num3, levelHeight) - ZeroSubst(num2, levelHeight);
                float x3 = ZeroSubst(num4, levelHeight) - ZeroSubst(num, levelHeight);
                int overrideTopTextureSlot = DefaultTextureSlot - MathF.Sign(x2) - 16 * Math.Sign(x3);
                generator.GenerateCubeVertices(
                    this,
                    value,
                    x,
                    y,
                    z,
                    height,
                    height2,
                    height3,
                    height4,
                    sideColor,
                    topColor,
                    topColor,
                    topColor,
                    topColor,
                    overrideTopTextureSlot,
                    subset
                );
            }
            else {
                generator.GenerateCubeVertices(
                    this,
                    value,
                    x,
                    y,
                    z,
                    sideColor,
                    subset
                );
            }
        }

        public static float ZeroSubst(float v, float subst) {
            if (v != 0f) {
                return v;
            }
            return subst;
        }

        public static float CalculateFluidVertexHeight(float h1, float h2, float h3, float h4) => Math.Min(MathUtils.Max(h1, h2, h3, h4), 1f);

        public override int GetFaceTextureSlot(int face, int value) => face >= 4 ? DefaultTextureSlot : DefaultTextureSlot + 16;

        public override bool ShouldGenerateFace(SubsystemTerrain subsystemTerrain, int face, int value, int neighborValue) => Terrain.ExtractContents(neighborValue) != BlockIndex && base.ShouldGenerateFace(subsystemTerrain, face, value, neighborValue);


        public void DrawFocalorsFluidBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData) {
            BlocksManager.DrawCubeBlock(
                primitivesRenderer,
                value,
                new Vector3(size, size * (1f - GetLevel(Terrain.ExtractData(value))), size),
                ref matrix,
                color,
                color,
                environmentData
            );
        }

        public float CalculateNeighborHeight(int value) {
            int num = Terrain.ExtractContents(value);
            if (num == BlockIndex) {
                int data = Terrain.ExtractData(value);
                return GetIsTop(data) ? GetLevelHeight(GetLevel(data)) : 1f;
            }
            return 0f;
        }

        public override bool IsHeatBlocker(int value) => true;

        public static float GetLevel(int data) => ((data >> 2) & 0xFFFF) / (float)0xFFFF;

        public static int SetLevel(int data, float level) => (data & -4) | ((int)(Math.Min(level, 1f) * 0xFFFF) << 2);

        public static bool GetIsTop(int data) => (data & 1) != 0;

        public static int SetIsTop(int data, bool isTop) => isTop ? data | 1 : data & -2;

        //public static bool GetIsBottom(int data) => (data & 2) != 0;

        //public static int SetIsBottom(int data, bool isBottom) => isBottom ? data | 2 : data & -3;
    }
}