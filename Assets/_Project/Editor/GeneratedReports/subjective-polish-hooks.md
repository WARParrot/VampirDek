# VampirDek Subjective Polish Hooks

Generated: 2026-06-06 18:48:34 UTC

This file captures work that is intentionally not objectively completable by an automated code pass. Use it as a checklist for art/audio/story/playtest polish after the technical scaffold is in place.

## Generated or touched technical assets

- `Assets/_Project/Combat/Data/Decks/VerticalSlice/VS_Encounter01_HungryScoutsDeck.asset`
- `Assets/_Project/Combat/Data/Decks/VerticalSlice/VS_Encounter02_BloodlingAmbushDeck.asset`
- `Assets/_Project/Combat/Data/Decks/VerticalSlice/VS_Encounter03_CryptRitualDeck.asset`
- `Assets/_Project/Combat/Data/Decks/VerticalSlice/VS_Boss_NightMatriarchDeck.asset`
- `Assets/_Project/Combat/Data/Encounters/VerticalSlice/VS_Encounter01_HungryScouts.asset`
- `Assets/_Project/Combat/Data/Encounters/VerticalSlice/VS_Encounter02_BloodlingAmbush.asset`
- `Assets/_Project/Combat/Data/Encounters/VerticalSlice/VS_Encounter03_CryptRitual.asset`
- `Assets/_Project/Combat/Data/Encounters/VerticalSlice/VS_Boss_NightMatriarch.asset`

## Optional card IDs absent during scaffold generation

- `VampireFodder`

## Subjective polish checklist

- [ ] Rename scaffold encounters/decks to final player-facing names.
- [ ] Replace placeholder encounter reward pools with tuned rewards after playtesting.
- [ ] Assign final card/encounter art and inspect UI composition in the Unity Editor.
- [ ] Assign card-specific `CombatVfxProfileId`/tints where default VFX feels generic.
- [ ] Add/choose music and SFX for menu, exploration, card play, impact, death, victory, loss, and rewards.
- [ ] Write final intro, between-encounter, boss, victory, and loss copy through the Novel/localization systems.
- [ ] Play through the whole 1-hour slice and tune encounter difficulty by observed player win rate/turn count.
- [ ] Capture screenshots/video after Editor/Gate reconnection; layout cannot be certified from static code alone.

## Objective follow-up hooks

- Run `Tools/VampirDek11/Vertical Slice/Validate Content & Write Readiness Report` after every content pass.
- Run `Tools/VampirDek11/Developer/Clear Local Saves` before first-run tutorial/progression testing.
- Use `Tools/VampirDek11/Developer/Print Runtime Progression State` in Play Mode when diagnosing encounter completion/unlock bugs.
