# VampirDek Vertical Slice Readiness Report

Generated: 2026-06-06 18:48:34 UTC

## Summary

- Status: Blocked
- Errors: 1
- Warnings: 3
- Info: 36
- Cards: 14
- Decks: 6
- Encounters: 5
- Phase graphs: 1
- Worlds: 2

## Developer next actions

1. Fix every Error entry first; these can break play or content loading.
2. Review Warning entries before a full playtest; these are likely completeness or progression gaps.
3. Use Info entries as polish hooks; they are intentionally non-blocking.
4. After subjective content/art/audio changes, run this validator again.

## Errors

- **Rewards**: Encounter 'VS_Encounter01_HungryScouts' reward references missing card 'VampireFodder'.
  - Path: `Assets/_Project/Combat/Data/Encounters/VerticalSlice/VS_Encounter01_HungryScouts.asset`

## Warnings

- **CompletionReadiness**: Only 14 cards detected. A complete-feeling 1-hour slice likely wants 20-30+ available cards or very deliberate encounter scripting.
  - Path: `Assets/_Project/Combat/Data/Cards`
- **PhaseGraphs**: Node 'EndOfTurn' has a conditional transition after an unconditional/default transition. Verify runtime transition priority matches intent.
  - Path: `Assets/_Project/Combat/Data/PhaseNodes/EndOfTurn.asset`
- **Progression**: Encounter 'TestEncounter' uses the same WinFlag and LoseFlag. This is usually wrong for campaign progression.
  - Path: `Assets/_Project/Combat/Data/Encounters/Test_Encounter.asset`

## Info / polish hooks

- **Inventory**: Detected 14 CardDef assets.
  - Path: `Assets/_Project/Combat/Data/Cards`
- **Inventory**: Detected 6 DeckData assets.
  - Path: `Assets/_Project/Combat/Data/Decks`
- **Inventory**: Detected 5 CombatEncounter assets.
  - Path: `Assets/_Project/Combat/Data/Encounters`
- **Localization**: Card 'Town' has no explicit CardNameKey; runtime fallback should be checked during localization polish.
  - Path: `Assets/_Project/Combat/Data/Cards/BaseTown.asset`
- **Localization**: Card 'BloodAltar' has no explicit CardNameKey; runtime fallback should be checked during localization polish.
  - Path: `Assets/_Project/Combat/Data/Cards/BloodAltar.asset`
- **Localization**: Card 'BloodWitch' has no explicit CardNameKey; runtime fallback should be checked during localization polish.
  - Path: `Assets/_Project/Combat/Data/Cards/BloodWitch.asset`
- **Localization**: Card 'Crypt' has no explicit CardNameKey; runtime fallback should be checked during localization polish.
  - Path: `Assets/_Project/Combat/Data/Cards/Crypt.asset`
- **Localization**: Card 'Decoy' has no explicit CardNameKey; runtime fallback should be checked during localization polish.
  - Path: `Assets/_Project/Combat/Data/Cards/Decoy.asset`
- **Localization**: Card 'FreshSpawn' has no explicit CardNameKey; runtime fallback should be checked during localization polish.
  - Path: `Assets/_Project/Combat/Data/Cards/FreshSpawn.asset`
- **Localization**: Card 'Ghoul' has no explicit CardNameKey; runtime fallback should be checked during localization polish.
  - Path: `Assets/_Project/Combat/Data/Cards/Ghoul.asset`
- **Localization**: Card 'Gourmet' has no explicit CardNameKey; runtime fallback should be checked during localization polish.
  - Path: `Assets/_Project/Combat/Data/Cards/Gourmet.asset`
- **Localization**: Card 'Human' has no explicit CardNameKey; runtime fallback should be checked during localization polish.
  - Path: `Assets/_Project/Combat/Data/Cards/Human.asset`
- **Localization**: Card 'NightFury' has no explicit CardNameKey; runtime fallback should be checked during localization polish.
  - Path: `Assets/_Project/Combat/Data/Cards/NightFury.asset`
- **Localization**: Card 'Ritualist' has no explicit CardNameKey; runtime fallback should be checked during localization polish.
  - Path: `Assets/_Project/Combat/Data/Cards/Ritualist.asset`
- **Localization**: Card 'Building' has no explicit CardNameKey; runtime fallback should be checked during localization polish.
  - Path: `Assets/_Project/Combat/Data/Cards/UsualBuilding.asset`
- **Localization**: Card 'Vampire' has no explicit CardNameKey; runtime fallback should be checked during localization polish.
  - Path: `Assets/_Project/Combat/Data/Cards/VampireFodder.asset`
- **Localization**: Card 'VampireLoner' has no explicit CardNameKey; runtime fallback should be checked during localization polish.
  - Path: `Assets/_Project/Combat/Data/Cards/VampireLoner.asset`
- **Presentation**: Card 'BloodAltar' has no CombatVfxProfileId; default VFX will be used until subjective polish assigns a profile.
  - Path: `Assets/_Project/Combat/Data/Cards/BloodAltar.asset`
- **Presentation**: Card 'BloodWitch' has no CombatVfxProfileId; default VFX will be used until subjective polish assigns a profile.
  - Path: `Assets/_Project/Combat/Data/Cards/BloodWitch.asset`
- **Presentation**: Card 'Crypt' has no CombatVfxProfileId; default VFX will be used until subjective polish assigns a profile.
  - Path: `Assets/_Project/Combat/Data/Cards/Crypt.asset`
- **Presentation**: Card 'Decoy' has no CombatVfxProfileId; default VFX will be used until subjective polish assigns a profile.
  - Path: `Assets/_Project/Combat/Data/Cards/Decoy.asset`
- **Presentation**: Card 'FreshSpawn' has no CombatVfxProfileId; default VFX will be used until subjective polish assigns a profile.
  - Path: `Assets/_Project/Combat/Data/Cards/FreshSpawn.asset`
- **Presentation**: Card 'Ghoul' has no CombatVfxProfileId; default VFX will be used until subjective polish assigns a profile.
  - Path: `Assets/_Project/Combat/Data/Cards/Ghoul.asset`
- **Presentation**: Card 'Gourmet' has no CombatVfxProfileId; default VFX will be used until subjective polish assigns a profile.
  - Path: `Assets/_Project/Combat/Data/Cards/Gourmet.asset`
- **Presentation**: Card 'NightFury' has no CombatVfxProfileId; default VFX will be used until subjective polish assigns a profile.
  - Path: `Assets/_Project/Combat/Data/Cards/NightFury.asset`
- **Presentation**: Card 'Ritualist' has no CombatVfxProfileId; default VFX will be used until subjective polish assigns a profile.
  - Path: `Assets/_Project/Combat/Data/Cards/Ritualist.asset`
- **Presentation**: Card 'Building' has no CombatVfxProfileId; default VFX will be used until subjective polish assigns a profile.
  - Path: `Assets/_Project/Combat/Data/Cards/UsualBuilding.asset`
- **Presentation**: Card 'Vampire' has no CombatVfxProfileId; default VFX will be used until subjective polish assigns a profile.
  - Path: `Assets/_Project/Combat/Data/Cards/VampireFodder.asset`
- **Presentation**: Card 'VampireLoner' has no CombatVfxProfileId; default VFX will be used until subjective polish assigns a profile.
  - Path: `Assets/_Project/Combat/Data/Cards/VampireLoner.asset`
- **WorldProgression**: Vertical-slice flag 'vs.boss.loss' is emitted but not yet used as a WorldSceneInfo.RequiredFlags gate. This is a hook for authored progression wiring.
  - Path: `Assets/_Project/Data/SceneInfos`
- **WorldProgression**: Vertical-slice flag 'vs.encounter01.loss' is emitted but not yet used as a WorldSceneInfo.RequiredFlags gate. This is a hook for authored progression wiring.
  - Path: `Assets/_Project/Data/SceneInfos`
- **WorldProgression**: Vertical-slice flag 'vs.encounter01.win' is emitted but not yet used as a WorldSceneInfo.RequiredFlags gate. This is a hook for authored progression wiring.
  - Path: `Assets/_Project/Data/SceneInfos`
- **WorldProgression**: Vertical-slice flag 'vs.encounter02.loss' is emitted but not yet used as a WorldSceneInfo.RequiredFlags gate. This is a hook for authored progression wiring.
  - Path: `Assets/_Project/Data/SceneInfos`
- **WorldProgression**: Vertical-slice flag 'vs.encounter02.win' is emitted but not yet used as a WorldSceneInfo.RequiredFlags gate. This is a hook for authored progression wiring.
  - Path: `Assets/_Project/Data/SceneInfos`
- **WorldProgression**: Vertical-slice flag 'vs.encounter03.loss' is emitted but not yet used as a WorldSceneInfo.RequiredFlags gate. This is a hook for authored progression wiring.
  - Path: `Assets/_Project/Data/SceneInfos`
- **WorldProgression**: Vertical-slice flag 'vs.encounter03.win' is emitted but not yet used as a WorldSceneInfo.RequiredFlags gate. This is a hook for authored progression wiring.
  - Path: `Assets/_Project/Data/SceneInfos`

