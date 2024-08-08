using System;
using System.Collections.Generic;
using System.Linq;
using Engine;
using Engine.Graphics;
using TemplatesDatabase;

namespace Game {
    public abstract class SubsystemFocalorsFluidBlockBehavior : SubsystemBlockBehavior, IDrawable {
        public class SpreadData {
            public float Volume;
            public bool Destroy;

            public SpreadData(float volume, bool destroy) {
                Volume = volume;
                Destroy = destroy;
            }
        }

        public static readonly Point2[] m_sideNeighbors = [new Point2(-1, 0), new Point2(1, 0), new Point2(0, -1), new Point2(0, 1)];

        public SubsystemTime SubsystemTime;

        public readonly int m_handleBlock;
        public readonly HashSet<Point3> m_toUpdate = [];
        public readonly Dictionary<Point3, SpreadData> m_toSpread = [];
        public readonly Dictionary<Point3, float> m_toDraw = [];
        public SubsystemFocalorsFluidBlockBehavior(int handleBlock) => m_handleBlock = handleBlock;

        public override void Load(ValuesDictionary valuesDictionary) {
            base.Load(valuesDictionary);
            SubsystemTime = Project.FindSubsystem<SubsystemTime>(true);
            m_fontBatch3D = m_primitivesRenderer3D.FontBatch(
                LabelWidget.BitmapFont,
                1,
                DepthStencilState.DepthRead,
                RasterizerState.CullNoneScissor,
                BlendState.AlphaBlend,
                SamplerState.LinearClamp
            );
        }

        public void SpreadFluid() {
            Terrain terrain = SubsystemTerrain.Terrain;
            foreach (Point3 centerPosition in m_toUpdate) {
                int centerValue = terrain.GetCellValue(centerPosition.X, centerPosition.Y, centerPosition.Z);
                if (Terrain.ExtractContents(centerValue) != m_handleBlock) {
                    m_toSpread.Remove(centerPosition);
                    continue;
                }
                float centerLeftVolume;
                bool centerSpreadDataInit = false;
                if (m_toSpread.TryGetValue(centerPosition, out SpreadData centerSpreadData)) {
                    centerLeftVolume = centerSpreadData.Volume;
                    centerSpreadDataInit = true;
                }
                else {
                    centerLeftVolume = 1 - FocalorsFluidBlock.GetLevel(Terrain.ExtractData(centerValue));
                }
                if (centerLeftVolume < 0.01f) {
                    continue;
                }
                float centerOriginalVolume = centerLeftVolume;
                if (centerPosition.Y > 0) {
                    Point3 downPosition = new() { X = centerPosition.X, Y = centerPosition.Y - 1, Z = centerPosition.Z };
                    int downValue = terrain.GetCellValueFast(centerPosition.X, centerPosition.Y - 1, centerPosition.Z);
                    int downContents = Terrain.ExtractContents(downValue);
                    if (m_toSpread.TryGetValue(downPosition, out SpreadData downSpreadData)) {
                        float downCapacity = 1 - downSpreadData.Volume;
                        if (downCapacity >= centerLeftVolume) {
                            if (centerSpreadDataInit) {
                                centerSpreadData.Volume = 0f;
                            }
                            else {
                                m_toSpread.Add(centerPosition, new SpreadData(0, false));
                            }
                            downSpreadData.Volume += centerLeftVolume;
                            continue;
                        }
                        centerLeftVolume -= downCapacity;
                        downSpreadData.Volume = 1f;
                    }
                    else if (downContents == AirBlock.Index) {
                        // TODO: 更好的FluidDropDown
                        //m_toSpread.Add(downPosition, new SpreadData(FluidDropDown(centerPosition.X, centerPosition.Y, centerPosition.Z, centerValue), false));
                        if (centerSpreadDataInit) {
                            centerSpreadData.Volume = 0f;
                        }
                        else {
                            m_toSpread.Add(centerPosition, new SpreadData(0, false));
                        }
                        m_toSpread.Add(downPosition, new SpreadData(centerLeftVolume, false));
                        continue;
                    }
                    else if (downContents == m_handleBlock) {
                        float downCapacity = FocalorsFluidBlock.GetLevel(Terrain.ExtractData(downValue));
                        if (downCapacity >= centerLeftVolume) {
                            if (centerSpreadDataInit) {
                                centerSpreadData.Volume = 0f;
                            }
                            else {
                                m_toSpread.Add(centerPosition, new SpreadData(0, false));
                            }
                            m_toSpread.Add(downPosition, new SpreadData(1 - downCapacity + centerLeftVolume, false));
                            continue;
                        }
                        if (downCapacity > 0) {
                            m_toSpread.Add(downPosition, new SpreadData(1, false));
                            centerLeftVolume -= downCapacity;
                        }
                    }
                    else if (!BlocksManager.Blocks[downContents].IsFluidBlocker_(downValue)) {
                        //m_toSpread.Add(downPosition, new SpreadData(FluidDropDown(centerPosition.X, centerPosition.Y, centerPosition.Z, centerValue), true));
                        if (centerSpreadDataInit) {
                            centerSpreadData.Volume = 0f;
                        }
                        else {
                            m_toSpread.Add(centerPosition, new SpreadData(0, false));
                        }
                        m_toSpread.Add(downPosition, new SpreadData(centerLeftVolume, true));
                        continue;
                    }
                }
                //四周的下面
                if (centerLeftVolume > 0.01f) {
                    Dictionary<Point3, float> toFluidVolume = [];
                    HashSet<Point3> toDestroy = [];
                    foreach (Point2 sideDirection in m_sideNeighbors) {
                        Point3 sidePosition = new() { X = centerPosition.X + sideDirection.X, Y = centerPosition.Y - 1, Z = centerPosition.Z + sideDirection.Y };
                        if (m_toSpread.TryGetValue(sidePosition, out SpreadData sideSpreadData)) {
                            if (sideSpreadData.Volume < 1) {
                                toFluidVolume.Add(sidePosition, sideSpreadData.Volume);
                            }
                            continue;
                        }
                        int sideValue = terrain.GetCellValue(sidePosition.X, sidePosition.Y, sidePosition.Z);
                        int sideContents = Terrain.ExtractContents(sideValue);
                        if (sideContents == AirBlock.Index) {
                            toFluidVolume.Add(sidePosition, 0);
                            continue;
                        }
                        if (sideContents == m_handleBlock) {
                            float sideVolume = 1 - FocalorsFluidBlock.GetLevel(Terrain.ExtractData(sideValue));
                            if (sideVolume < 1f) {
                                toFluidVolume.Add(sidePosition, sideVolume);
                            }
                            continue;
                        }
                        if (!BlocksManager.Blocks[sideContents].IsFluidBlocker_(sideValue)) {
                            toFluidVolume.Add(sidePosition, 0);
                            toDestroy.Add(sidePosition);
                        }
                    }
                    if (toFluidVolume.Count > 0) {
                        while (toFluidVolume.Count > 0) {
                            float maxVolume = toFluidVolume.Max(pair => pair.Value);
                            float minCapacity = 1 - maxVolume;
                            float requireVolume = minCapacity * toFluidVolume.Count;
                            if (centerLeftVolume >= requireVolume) {
                                HashSet<Point3> toRemove = [];
                                foreach ((Point3 sidePosition, float sideVolume) in toFluidVolume) {
                                    if (m_toSpread.TryGetValue(sidePosition, out SpreadData sideSpreadData)) {
                                        sideSpreadData.Volume += minCapacity;
                                        if (sideSpreadData.Volume >= 0.99f) {
                                            toRemove.Add(sidePosition);
                                        }
                                    }
                                    else {
                                        m_toSpread.Add(sidePosition, new SpreadData(sideVolume + minCapacity, false));
                                    }
                                }
                                foreach (Point3 point in toRemove) {
                                    toFluidVolume.Remove(point);
                                }
                                centerLeftVolume -= requireVolume;
                            }
                            else {
                                float averageFluidVolume = centerLeftVolume / toFluidVolume.Count;
                                foreach ((Point3 sidePosition, float sideVolume) in toFluidVolume) {
                                    if (m_toSpread.TryGetValue(sidePosition, out SpreadData sideSpreadData)) {
                                        sideSpreadData.Volume += averageFluidVolume;
                                    }
                                    else if (toDestroy.Contains(sidePosition)) {
                                        m_toSpread.Add(sidePosition, new SpreadData(averageFluidVolume, true));
                                    }
                                    else {
                                        m_toSpread.Add(sidePosition, new SpreadData(sideVolume + averageFluidVolume, false));
                                    }
                                }
                                toFluidVolume.Clear();
                                centerLeftVolume = 0f;
                            }
                        }
                    }
                }
                //四周
                if (centerLeftVolume > 0.01f) {
                    Dictionary<Point3, float> toFluidVolume = [];
                    HashSet<Point3> toDestroy = [];
                    foreach (Point2 sideDirection in m_sideNeighbors) {
                        Point3 sidePosition = new() { X = centerPosition.X + sideDirection.X, Y = centerPosition.Y, Z = centerPosition.Z + sideDirection.Y };
                        if (m_toSpread.TryGetValue(sidePosition, out SpreadData sideSpreadData)) {
                            if (sideSpreadData.Volume < centerLeftVolume) {
                                toFluidVolume.Add(sidePosition, sideSpreadData.Volume);
                            }
                            continue;
                        }
                        int sideValue = terrain.GetCellValue(sidePosition.X, sidePosition.Y, sidePosition.Z);
                        int sideContents = Terrain.ExtractContents(sideValue);
                        if (sideContents == AirBlock.Index) {
                            toFluidVolume.Add(sidePosition, 0);
                            continue;
                        }
                        if (sideContents == m_handleBlock) {
                            float sideVolume = 1 - FocalorsFluidBlock.GetLevel(Terrain.ExtractData(sideValue));
                            if (sideVolume < centerLeftVolume) {
                                toFluidVolume.Add(sidePosition, sideVolume);
                            }
                            continue;
                        }
                        if (!BlocksManager.Blocks[sideContents].IsFluidBlocker_(sideValue)) {
                            toFluidVolume.Add(sidePosition, 0);
                            toDestroy.Add(sidePosition);
                        }
                    }
                    if (toFluidVolume.Count > 0) {
                        float averageVolume = (toFluidVolume.Sum(pair => pair.Value) + centerLeftVolume) / (toFluidVolume.Count + 1);
                        if (averageVolume > 0.005f) {
                            centerLeftVolume = averageVolume;
                            foreach ((Point3 sidePosition, float sideVolume) in toFluidVolume) {
                                if (Math.Abs(sideVolume - averageVolume) > 0.005f) {
                                    if (m_toSpread.TryGetValue(sidePosition, out SpreadData sideSpreadData)) {
                                        sideSpreadData.Volume = averageVolume;
                                    }
                                    else if (toDestroy.Contains(sidePosition)) {
                                        m_toSpread.Add(sidePosition, new SpreadData(averageVolume, true));
                                    }
                                    else {
                                        m_toSpread.Add(sidePosition, new SpreadData(averageVolume, false));
                                    }
                                }
                            }
                        }
                        else {
                            centerLeftVolume = 0f;
                        }
                    }
                }
                if (Math.Abs(centerOriginalVolume - centerLeftVolume) > 0.005f) {
                    if (centerSpreadDataInit) {
                        centerSpreadData.Volume = centerLeftVolume > 0.01f ? centerLeftVolume : 0f;
                    }
                    else {
                        m_toSpread.Add(centerPosition, new SpreadData(centerLeftVolume > 0.01f ? centerLeftVolume : 0f, false));
                    }
                }
            }
            m_toUpdate.Clear();
            foreach ((Point3 position, SpreadData spreadData) in m_toSpread) {
                int newValue = spreadData.Volume < 0.01f ? AirBlock.Index : Terrain.MakeBlockValue(m_handleBlock, 0, FocalorsFluidBlock.SetLevel(0, spreadData.Volume > 0.99 ? 0 : 1 - spreadData.Volume));
                if (spreadData.Destroy) {
                    SubsystemTerrain.DestroyCell(
                        0,
                        position.X,
                        position.Y,
                        position.Z,
                        newValue,
                        false,
                        false
                    );
                }
                else {
                    SubsystemTerrain.ChangeCell(position.X, position.Y, position.Z, newValue);
                }
            }
            m_toSpread.Clear();
        }

        public float FluidDropDown(int x, int y, int z, int startValue) {
            int lastValue = startValue;
            Terrain terrain = SubsystemTerrain.Terrain;
            while (y < 255) {
                int upValue = y == 254 ? 0 : terrain.GetCellValueFast(x, y + 1, z);
                if (y == 254
                    || Terrain.ExtractContents(upValue) != m_handleBlock
                    || (m_toSpread.TryGetValue(new Point3(x, y + 1, z), out SpreadData upSpreadData) && upSpreadData.Volume < 0.01f)) {
                    Point3 lastPosition = new() { X = x, Y = y, Z = z };
                    float lastLeftVolume;
                    if (m_toSpread.TryGetValue(lastPosition, out SpreadData lastSpreadData)) {
                        lastLeftVolume = lastSpreadData.Volume;
                        lastSpreadData.Volume = 0f;
                    }
                    else {
                        lastLeftVolume = 1 - FocalorsFluidBlock.GetLevel(Terrain.ExtractData(lastValue));
                        m_toSpread.Add(lastPosition, new SpreadData(0, false));
                    }
                    return lastLeftVolume;
                }
                lastValue = upValue;
                y++;
            }
            return 0f;
        }

        public override void OnBlockAdded(int value, int oldValue, int x, int y, int z) {
            m_toUpdate.Add(new Point3 { X = x, Y = y, Z = z });
            UpdateIsTop(value, x, y, z);
            m_toDraw.Add(new Point3(x, y, z), FocalorsFluidBlock.GetLevel(Terrain.ExtractData(value)));
        }

        public override void OnBlockRemoved(int value, int newValue, int x, int y, int z) {
            m_toDraw.Remove(new Point3(x, y, z));
        }

        public override void OnBlockModified(int value, int oldValue, int x, int y, int z) {
            UpdateIsTop(value, x, y, z);
            m_toDraw[new Point3(x, y, z)] = FocalorsFluidBlock.GetLevel(Terrain.ExtractData(value));
        }

        public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ) {
            m_toUpdate.Add(new Point3 { X = x, Y = y, Z = z });
            if (neighborY == y + 1
                || neighborY == y - 1) {
                UpdateIsTop(SubsystemTerrain.Terrain.GetCellValueFast(x, y, z), x, y, z);
            }
        }

        public override void OnItemHarvested(int x, int y, int z, int blockValue, ref BlockDropValue dropValue, ref int newBlockValue) {
            newBlockValue = m_handleBlock;
            dropValue.Value = 0;
            dropValue.Count = 0;
        }

        public void UpdateIsTop(int value, int x, int y, int z) {
            Terrain terrain = SubsystemTerrain.Terrain;
            TerrainChunk chunkAtCell = terrain.GetChunkAtCell(x, z);
            if (chunkAtCell != null) {
                int oldData = Terrain.ExtractData(value);
                bool isTop = y >= 255 || terrain.GetCellContentsFast(x, y + 1, z) != m_handleBlock || FocalorsFluidBlock.GetLevel(oldData) > 0f;
                int newData = FocalorsFluidBlock.SetIsTop(oldData, isTop);
                terrain.SetCellValueFast(x, y, z, Terrain.ReplaceData(value, newData));
            }
        }

        public PrimitivesRenderer3D m_primitivesRenderer3D = new();
        public FontBatch3D m_fontBatch3D;

        public void Draw(Camera camera, int drawOrder) {
            Vector3 viewPosition = camera.ViewPosition;
            //Vector3 viewDirection = camera.ViewDirection;
            foreach ((Point3 point, float level) in m_toDraw) {
                Vector3 position = new(point.X + 0.5f, point.Y + 1.5f, point.Z + 0.5f);
                Vector3 direction = Vector3.Normalize(position - viewPosition);
                m_fontBatch3D.QueueText(
                    (1 - level).ToString("0.00"),
                    position,
                    Vector3.Cross(direction, Vector3.UnitY) * 0.01f,
                    -Vector3.UnitY * 0.01f,
                    Color.White,
                    TextAnchor.HorizontalCenter
                );
            }
            m_primitivesRenderer3D.Flush(camera.ViewProjectionMatrix);
        }

        public int[] DrawOrders => [2000];
    }
}