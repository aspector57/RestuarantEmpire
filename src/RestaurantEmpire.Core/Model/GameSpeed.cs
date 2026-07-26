namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// How fast the player has asked live play to run (design doc Phase 5, "live play at
    /// adjustable speed — the Sims model").
    ///
    /// Held here as DATA ONLY, per M0's scope. The simulation core knows what speed is
    /// selected and what it multiplies; it does not run a real-time loop. Turning a
    /// multiplier into actual wall-clock pacing is a presentation concern and belongs to
    /// the Unity layer at M1, not in here.
    ///
    /// Pizza Connection 2 is the cautionary tale the design cites: a management sim whose
    /// clock the player could not accelerate. Speed control is load-bearing, not a nicety.
    /// </summary>
    public enum GameSpeed
    {
        Paused = 0,
        Normal = 1,
        Fast = 2,
        Fastest = 3
    }

    public static class GameSpeedExtensions
    {
        /// <summary>Game-minutes elapsed per unit of real time. Paused is 0; the rest are 1x/2x/3x.</summary>
        public static int Multiplier(this GameSpeed speed)
        {
            return (int)speed;
        }

        public static bool IsPaused(this GameSpeed speed)
        {
            return speed == GameSpeed.Paused;
        }
    }
}
