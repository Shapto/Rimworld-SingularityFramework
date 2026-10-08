using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SingularityFramework.Equipment
{
    /// <summary>
    /// A weapon comp that shows gizmos for its wielder while equipped (status boxes, bars).
    /// Vanilla only asks one comp for equipment gizmos, so the framework asks every comp that implements this.
    /// </summary>
    public interface IEquippedGizmoProvider
    {
        IEnumerable<Gizmo> GetEquippedGizmos(Pawn wielder);
    }
}
