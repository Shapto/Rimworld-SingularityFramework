using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace SingularityFramework
{
    public class SingularityMod : Mod
    {
        public SingularityMod(ModContentPack content) : base(content)
        {
            new Harmony("shapto.shinandmang").PatchAll();
        }
    }
}
