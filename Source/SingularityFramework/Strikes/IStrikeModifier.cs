using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SingularityFramework.Strikes
{
    /// <summary>
    /// Implemented by anything that offers strike modifiers: a weapon comp, a hediff comp, and so on.
    /// The tracker asks the attacker's weapon and hediffs for these before every attack.
    /// </summary>
    public interface IStrikeModifierProvider
    {
        /// <summary>
        /// The modifiers this provider offers for the next attack.
        /// </summary>
        IEnumerable<StrikeModifier> GetStrikeModifiers();
    }
}
