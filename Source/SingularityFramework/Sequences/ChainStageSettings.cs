using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SingularityFramework.Sequences
{
    /// <summary>
    /// Extra settings for one stage of a chained ability. Stages without an entry have none of these.
    /// </summary>
    public class ChainStageSettings
    {
        /// <summary>
        /// How long the caster stands charging before this stage's action happens. 0 for no charge-up.
        /// The caster can be hit while charging.
        /// </summary>
        public int chargeUpTicks;

        /// <summary>
        /// Played when the charge-up starts.
        /// </summary>
        public SoundDef chargeUpSound;
    }

    /// <summary>
    /// A comp that does the action of some stages of a chained ability (a dash, a jump).
    /// Lets a charge-up perform the right action once it finishes.
    /// </summary>
    public interface IChainStageAction
    {
        bool ActsOnStage(int stage);
        void PerformStageAction(LocalTargetInfo target);
    }
}
