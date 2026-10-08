# Singularity Framework

A shared RimWorld mod framework for authoring combat, ability, and visual effects in other mods.

## Overview

`Singularity Framework` is a framework mod built for RimWorld 1.6. It exists to centralize common patterns used by me, especially around:

- equipment form switching
- ability chains and movement sequences
- charged melee strikes and damage modifiers
- visual effect helpers and aura components
- weapon/body alignment for animated equipment rendering
- geometric helpers for strike paths and line-based attacks

## Mod purpose

This mod is intended as a dependency for other mods. It is lightweight on its own.

## Project structure

- `1.6/` - RimWorld 1.6 content, including defs and compatibility files
- `About/` - mod metadata and preview image
- `Languages/` - localization files
- `Source/SingularityFramework/` - compiled mod source code
- `Source/SingularityFramework/Properties/` - assembly metadata
- `Source/SingularityFramework/Equipment/` - equipment-related utilities and patches
- `Source/SingularityFramework/Geometry/` - geometric strike helpers
- `Source/SingularityFramework/Relics/` - relic-related systems and effects
- `Source/SingularityFramework/Sequences/` - ability sequencing systems such as chain abilities, dashes, and slam behavior
- `Source/SingularityFramework/Strikes/` - charged-strike and on-hit effect logic
- `Source/SingularityFramework/Visuals/` - aura and visual effect components
- `Source/SingularityFramework/WeaponAnchor/` - weapon/body alignment and draw calculations for equipment rendering

## Key systems

### Equipment form switching

The framework includes support for weapons that change form while equipped, such as transforming between different weapon states or configurations.

Relevant code:

- `CompProperties_WeaponForm`
- `CompWeaponForm`
- `Patch_PawnEquipmentTracker_GetGizmos`

This system preserves the weapon's material, quality, durability ratio, biocode data, and remaining charges when switching to another form.

### Ability chains and motion sequences

The sequence system supports multi-stage abilities and movement patterns such as chained casts, line dashes, and slam-style effects.

Examples:

- `CompProperties_AbilityChain` for stage-based abilities
- `Verb_CastAbilityLineDash` and `JobDriver_LineDash` for line-dash behavior
- `PawnFlyer_LandingSlam` for landing impact sequences

### Charged strikes and hit effects

The framework provides a strike modifier model that can spend a weapon's reloadable charges during melee attacks and modify damage or apply effects on hit.

Examples:

- `CompProperties_ChargedStrikes`
- `CompChargedStrikes`
- `OnHitEffect`
- `OnHitEffect_AddHediff`
- `OnHitEffect_ExtraDamage`
- `OnHitEffect_Stun`
- `OnHitEffect_DrainMood`

This allows mod authors to build weapons with effects that trigger only when a charged hit connects.

### Weapon rendering and body anchoring

The `WeaponAnchor` subsystem analyzes pawn body shapes and weapon geometry so equipment can be rendered correctly and aligned with the character during animation.

Examples:

- `BodyShapeAnalyzer`
- `WeaponShapeAnalyzer`
- `WeaponShape`
- `WeaponDrawRecord`
- `Patch_AnimRenderer_Draw`
- `Patch_PawnRenderUtility_DrawEquipmentAiming`

This is useful for mods that add unusual weapon silhouettes or custom arm/weapon animation behaviors.

### Visual effects

The visual helpers include aura-type effects that can attach to pawns or related entities.

Examples:

- `HediffCompProperties_Aura`
- `WeaponEffects`

## Dependencies

- Harmony
- RimWorld 1.6

## Notes for mod authors

The framework's entry point is the mod class:

- `SingularityMod` - applies Harmony patches when the mod loads

The project also includes RimWorld 1.6-specific content under `1.6/` for definitions and compatibility support.

## License

This project is distributed under the MIT license. See `LICENSE` for details.
