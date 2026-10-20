using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SingularityFramework.Equipment
{
    /// <summary>
    /// Moves an ideology relic's identity from one weapon form to the next,
    /// so switching forms is not treated as losing or destroying the relic.
    /// </summary>
    public static class RelicTransfer
    {
        /// <summary>
        /// Detaches the relic precept from the old form and attaches it to the new form.
        /// Must be called before the old form is destroyed.
        /// </summary>
        public static void Transfer(ThingWithComps oldForm, ThingWithComps newForm)
        {
            if (!ModsConfig.IdeologyActive) return;
            if (!(oldForm.StyleSourcePrecept is Precept_Relic relicPrecept)) return;

            // Unlinking first means Destroy() no longer sees a relic and won't call Notify_ThingLost
            oldForm.StyleSourcePrecept = null;
            newForm.StyleSourcePrecept = relicPrecept;

            // The precept also holds its own reference to the relic Thing; repoint it at the new form
            for (Type precepType = relicPrecept.GetType(); precepType != null && precepType != typeof(object); precepType = precepType.BaseType)
            {
                foreach (FieldInfo field in precepType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.FieldType.IsAssignableFrom(newForm.GetType()) && ReferenceEquals(field.GetValue(relicPrecept), oldForm)) field.SetValue(relicPrecept, newForm);
                }
            }
        }
    }
}
