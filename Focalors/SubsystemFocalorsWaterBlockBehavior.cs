namespace Game {
    public class SubsystemFocalorsWaterBlockBehavior : SubsystemFocalorsFluidBlockBehavior, IUpdateable {
        public Random m_random = new();

        public float m_soundVolume;

        public override int[] HandledBlocks => [FocalorsWaterBlock.Index];

        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        public SubsystemFocalorsWaterBlockBehavior() : base(FocalorsWaterBlock.Index) { }

        public void Update(float dt) {
            if (SubsystemTime.PeriodicGameTimeEvent(0.25, 0.0)) {
                SpreadFluid();
            }
        }

        public override void OnItemHarvested(int x, int y, int z, int blockValue, ref BlockDropValue dropValue, ref int newBlockValue) {
            if (y > 80
                && SubsystemWeather.IsPlaceFrozen(SubsystemTerrain.Terrain.GetSeasonalTemperature(x, z), y)) {
                dropValue.Value = Terrain.MakeBlockValue(IceBlock.Index);
            }
            else {
                base.OnItemHarvested(
                    x,
                    y,
                    z,
                    blockValue,
                    ref dropValue,
                    ref newBlockValue
                );
            }
        }
    }
}