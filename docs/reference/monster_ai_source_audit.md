# Monster AI Source Audit

Source: `F:/sts2_mod/STS2_Proj/src/Core/Models/Monsters/`

## Scope

- This pass is inheritance-aware: if a monster does not override `GenerateMoveStateMachine()`, HP, or lifecycle hooks locally, it is classified from the nearest ancestor implementation.
- HP is reported as `base / tough` when the value can be resolved from `AscensionHelper.GetValueIfAscension(...)` or a constant.
- The table covers all top-level monster source types in `src/Core/Models/Monsters`, including the shared abstract base `DecimillipedeSegment`.

## Summary Statistics

| Category | Count |
|----------|-------|
| Total source types | 121 |
| Concrete monster types | 120 |
| Abstract base types | 1 |
| Sequential topology | 80 |
| Random topology | 25 |
| Conditional topology | 14 |
| Hybrid topology | 2 |
| With `AfterAddedToRoom` override | 67 |
| With `BeforeRemovedFromRoom` override | 8 |
| With `OnDieToDoom` override | 1 |
| With any `.Died +=` subscription | 10 |
| With summon capability | 6 |
| With forced transition capability | 4 |
| With talk / banter | 12 |
| With status-card injection or card-choice logic | 18 |
| With music parameter writes | 9 |
| With `MustPerformOnceBeforeTransitioning` | 7 |
| With mutable runtime state | 47 |

## Corrections Versus Draft Audit

- `MysteriousKnight` does not have a local FSM override, but it inherits `FlailKnight`'s random-branch state machine and HP profile.
- `DecimillipedeSegmentBack`, `DecimillipedeSegmentFront`, and `DecimillipedeSegmentMiddle` inherit the random FSM, HP range, and `AfterAddedToRoom` hook from `DecimillipedeSegment`.
- The earlier draft's `None` topology bucket disappears once inherited behavior is taken into account.

## Per-Type Audit

| # | Monster | Base | Abstract | HP (base/tough) | Topology | FSM Source | Moves | Mutable Fields | Lifecycle | Special |
|---|---------|------|----------|-----------------|----------|------------|-------|----------------|-----------|---------|
| 1 | Architect | MonsterModel | - | base 9999 | tough 9999 | Sequential | Architect | 1 (NOTHING) | - | - | - |
| 2 | AssassinRubyRaider | MonsterModel | - | base 18-23 | tough 19-24 | Sequential | AssassinRubyRaider | 1 (KILLSHOT_MOVE) | - | - | - |
| 3 | AxeRubyRaider | MonsterModel | - | base 20-22 | tough 21-23 | Sequential | AxeRubyRaider | 3 (SWING_1, SWING_2, BIG_SWING) | - | - | - |
| 4 | Axebot | MonsterModel | - | base 40-44 | tough 42-46 | Random | Axebot | 4 (BOOT_UP_MOVE, ONE_TWO_MOVE, SHARPEN_MOVE, HAMMER_UPPERCUT_MOVE) | 2 (_shouldPlaySpawnAnimation, _stockOverrideAmount) | AAR | - |
| 5 | BattleFriendV1 | MonsterModel | - | base 75 | tough 75 | Sequential | BattleFriendV1 | 1 (NOTHING_MOVE) | - | AAR | TestOnly |
| 6 | BattleFriendV2 | MonsterModel | - | base 150 | tough 150 | Sequential | BattleFriendV2 | 1 (NOTHING_MOVE) | - | AAR | TestOnly |
| 7 | BattleFriendV3 | MonsterModel | - | base 300 | tough 300 | Sequential | BattleFriendV3 | 1 (NOTHING_MOVE) | - | AAR | TestOnly |
| 8 | BigDummy | MonsterModel | - | base 9999 | tough 9999 | Sequential | BigDummy | 1 (NOTHING) | - | - | TestOnly |
| 9 | BowlbugEgg | MonsterModel | - | base 21-22 | tough 23-24 | Sequential | BowlbugEgg | 1 (BITE_MOVE) | - | AAR | - |
| 10 | BowlbugNectar | MonsterModel | - | base 35-38 | tough 36-39 | Sequential | BowlbugNectar | 3 (THRASH_MOVE, BUFF_MOVE, THRASH2_MOVE) | - | - | - |
| 11 | BowlbugRock | MonsterModel | - | base 45-48 | tough 46-49 | Conditional | BowlbugRock | 2 (HEADBUTT_MOVE, DIZZY_MOVE) | 1 (_isOffBalance) | AAR | - |
| 12 | BowlbugSilk | MonsterModel | - | base 40-43 | tough 41-44 | Sequential | BowlbugSilk | 2 (TRASH_MOVE, TOXIC_SPIT_MOVE) | - | - | - |
| 13 | BruteRubyRaider | MonsterModel | - | base 30-33 | tough 31-34 | Sequential | BruteRubyRaider | 2 (BEAT_MOVE, ROAR_MOVE) | - | - | - |
| 14 | BygoneEffigy | MonsterModel | - | base 127 | tough 132 | Sequential | BygoneEffigy | 4 (INITIAL_SLEEP_MOVE, WAKE_MOVE, SLEEP_MOVE, SLASHES_MOVE) | - | AAR | Talk |
| 15 | Byrdonis | MonsterModel | - | base 91-94 | tough 99 | Sequential | Byrdonis | 2 (PECK_MOVE, SWOOP_MOVE) | - | AAR | - |
| 16 | Byrdpip | MonsterModel | - | base 9999 | tough 9999 | Sequential | Byrdpip | 1 (NOTHING_MOVE) | - | - | - |
| 17 | CalcifiedCultist | MonsterModel | - | base 38-41 | tough 39-42 | Sequential | CalcifiedCultist | 2 (INCANTATION_MOVE, DARK_STRIKE_MOVE) | 1 (_attackSfxStrength) | - | Talk |
| 18 | CeremonialBeast | MonsterModel | - | base 252 | tough 262 | Sequential | CeremonialBeast | 6 (STAMP_MOVE, PLOW_MOVE, STUN_MOVE, BEAST_CRY_MOVE, STOMP_MOVE, CRUSH_MOVE) | 3 (_beastCryState, _inMidCharge, _isStunnedByPlowRemoval) | - | MustPerformOnce |
| 19 | Chomper | MonsterModel | - | base 60-64 | tough 63-67 | Sequential | Chomper | 2 (CLAMP_MOVE, SCREECH_MOVE) | 1 (_screamFirst) | AAR | StatusCard, Talk |
| 20 | CorpseSlug | MonsterModel | - | base 25-27 | tough 27-29 | Sequential | CorpseSlug | 3 (WHIP_SLAP_MOVE, GLOMP_MOVE, GOOP_MOVE) | 2 (_isRavenous, _starterMoveIdx) | AAR | - |
| 21 | CrossbowRubyRaider | MonsterModel | - | base 18-21 | tough 19-22 | Sequential | CrossbowRubyRaider | 2 (FIRE_MOVE, RELOAD_MOVE) | 1 (_isCrossbowReloaded) | - | - |
| 22 | Crusher | MonsterModel | - | base 199 | tough 209 | Sequential | Crusher | 5 (THRASH_MOVE, ENLARGING_STRIKE_MOVE, BUG_STING_MOVE, ADAPT_MOVE, GUARDED_STRIKE_MOVE) | - | AAR | - |
| 23 | CubexConstruct | MonsterModel | - | base 65 | tough 70 | Sequential | CubexConstruct | 5 (CHARGE_UP_MOVE, REPEATER_MOVE, REPEATER_MOVE_2, EXPEL_BLAST, SUBMERGE_MOVE) | - | AAR, BRR | - |
| 24 | DampCultist | MonsterModel | - | base 51-53 | tough 52-54 | Sequential | DampCultist | 2 (INCANTATION_MOVE, DARK_STRIKE_MOVE) | 1 (_attackSfxStrength) | - | Talk |
| 25 | DecimillipedeSegment | MonsterModel | Y | base 42-48 | tough 48-56 | Random | DecimillipedeSegment | 5 (WRITHE_MOVE, BULK_MOVE, CONSTRICT_MOVE, DEAD_MOVE, REATTACH_MOVE) | 2 (_deadState, _starterMoveIdx) | AAR | MustPerformOnce |
| 26 | DecimillipedeSegmentBack | DecimillipedeSegment | - | base 42-48 | tough 48-56 | Random | DecimillipedeSegment | 5 (WRITHE_MOVE, BULK_MOVE, CONSTRICT_MOVE, DEAD_MOVE, REATTACH_MOVE) | 2 (_deadState, _starterMoveIdx) | AAR | InheritedFSM:DecimillipedeSegment, MustPerformOnce |
| 27 | DecimillipedeSegmentFront | DecimillipedeSegment | - | base 42-48 | tough 48-56 | Random | DecimillipedeSegment | 5 (WRITHE_MOVE, BULK_MOVE, CONSTRICT_MOVE, DEAD_MOVE, REATTACH_MOVE) | 2 (_deadState, _starterMoveIdx) | AAR | InheritedFSM:DecimillipedeSegment, MustPerformOnce |
| 28 | DecimillipedeSegmentMiddle | DecimillipedeSegment | - | base 42-48 | tough 48-56 | Random | DecimillipedeSegment | 5 (WRITHE_MOVE, BULK_MOVE, CONSTRICT_MOVE, DEAD_MOVE, REATTACH_MOVE) | 2 (_deadState, _starterMoveIdx) | AAR | InheritedFSM:DecimillipedeSegment, MustPerformOnce |
| 29 | DevotedSculptor | MonsterModel | - | base 162 | tough 172 | Sequential | DevotedSculptor | 2 (FORBIDDEN_INCANTATION_MOVE, SAVAGE_MOVE) | - | - | Talk |
| 30 | Door | MonsterModel | - | base 155 | tough 165 | Sequential | Door | 4 (DRAMATIC_OPEN_MOVE, ENFORCE_MOVE, DOOR_SLAM_MOVE, DEAD_MOVE) | 2 (_deadState, _doormaker) | AAR | - |
| 31 | Doormaker | MonsterModel | - | base 489 | tough 512 | Sequential | Doormaker | 3 (WHAT_IS_IT_MOVE, BEAM_MOVE, GET_BACK_IN_MOVE) | 1 (_timesGotBackIn) | - | Talk, Music |
| 32 | Entomancer | MonsterModel | - | base 145 | tough 155 | Sequential | Entomancer | 3 (PHEROMONE_SPIT_MOVE, BEES_MOVE, SPEAR_MOVE) | - | AAR | - |
| 33 | Exoskeleton | MonsterModel | - | base 24-28 | tough 25-29 | Hybrid | Exoskeleton | 3 (SKITTER_MOVE, MANDIBLE_MOVE, ENRAGE_MOVE) | - | AAR | - |
| 34 | EyeWithTeeth | MonsterModel | - | base 6 | tough 6 | Sequential | EyeWithTeeth | 1 (DISTRACT_MOVE) | - | AAR | StatusCard |
| 35 | Fabricator | MonsterModel | - | base 150 | tough 155 | Hybrid | Fabricator | 3 (FABRICATE_MOVE, FABRICATING_STRIKE_MOVE, DISINTEGRATE_MOVE) | - | - | Summon |
| 36 | FakeMerchantMonster | MonsterModel | - | base 165 | tough 175 | Random | FakeMerchantMonster | 4 (SWIPE_MOVE, SPEW_COINS_MOVE, THROW_RELIC_MOVE, ENRAGE_MOVE) | - | - | Talk |
| 37 | FatGremlin | MonsterModel | - | base 13-17 | tough 14-18 | Sequential | FatGremlin | 2 (SPAWNED_MOVE, FLEE_MOVE) | 1 (_isAwake) | - | Talk |
| 38 | FlailKnight | MonsterModel | - | base 101 | tough 108 | Random | FlailKnight | 3 (WAR_CHANT, FLAIL_MOVE, RAM_MOVE) | - | - | - |
| 39 | Flyconid | MonsterModel | - | base 47-49 | tough 51-53 | Random | Flyconid | 3 (VULNERABLE_SPORES_MOVE, FRAIL_SPORES_MOVE, SMASH_MOVE) | - | - | - |
| 40 | Fogmog | MonsterModel | - | base 74 | tough 78 | Random | Fogmog | 4 (ILLUSION_MOVE, SWIPE_MOVE, SWIPE_RANDOM_MOVE, HEADBUTT_MOVE) | - | - | Summon |
| 41 | FossilStalker | MonsterModel | - | base 51-53 | tough 54-56 | Random | FossilStalker | 3 (TACKLE_MOVE, LATCH_MOVE, LASH_MOVE) | - | AAR | - |
| 42 | FrogKnight | MonsterModel | - | base 191 | tough 199 | Conditional | FrogKnight | 4 (FOR_THE_QUEEN, STRIKE_DOWN_EVIL, TONGUE_LASH, BEETLE_CHARGE) | 1 (_hasBeetleCharged) | AAR | - |
| 43 | FuzzyWurmCrawler | MonsterModel | - | base 55-57 | tough 58-59 | Sequential | FuzzyWurmCrawler | 3 (FIRST_ACID_GOOP, ACID_GOOP, INHALE) | 1 (_isPuffed) | - | - |
| 44 | GasBomb | MonsterModel | - | base 10 | tough 12 | Sequential | GasBomb | 1 (EXPLODE_MOVE) | 1 (_hasExploded) | AAR | - |
| 45 | GlobeHead | MonsterModel | - | base 148 | tough 158 | Sequential | GlobeHead | 3 (THUNDER_STRIKE, SHOCKING_SLAP, GALVANIC_BURST) | - | AAR | - |
| 46 | GremlinMerc | MonsterModel | - | base 47-49 | tough 51-53 | Sequential | GremlinMerc | 3 (GIMME_MOVE, DOUBLE_SMASH_MOVE, HEHE_MOVE) | - | AAR | Talk |
| 47 | Guardbot | MonsterModel | - | base 21-25 | tough 22-26 | Sequential | Guardbot | 1 (GUARD_MOVE) | - | AAR | - |
| 48 | HauntedShip | MonsterModel | - | base 63 | tough 67 | Random | HauntedShip | 4 (RAMMING_SPEED_MOVE, SWIPE_MOVE, STOMP_MOVE, HAUNT_MOVE) | - | - | StatusCard |
| 49 | HunterKiller | MonsterModel | - | base 121 | tough 126 | Random | HunterKiller | 3 (TENDERIZING_GOOP_MOVE, BITE_MOVE, PUNCTURE_MOVE) | - | - | - |
| 50 | InfestedPrism | MonsterModel | - | base 200 | tough 215 | Sequential | InfestedPrism | 4 (JAB_MOVE, RADIATE_MOVE, WHIRLWIND_MOVE, PULSATE_MOVE) | - | AAR | - |
| 51 | Inklet | MonsterModel | - | base 11-17 | tough 12-18 | Random | Inklet | 3 (JAB_MOVE, WHIRLWIND_MOVE, PIERCING_GAZE_MOVE) | 1 (_middleInklet) | AAR | - |
| 52 | KinFollower | MonsterModel | - | base 58-59 | tough 62-63 | Sequential | KinFollower | 3 (QUICK_SLASH_MOVE, BOOMERANG_MOVE, POWER_DANCE_MOVE) | 1 (_startsWithDance) | AAR, DE | Music |
| 53 | KinPriest | MonsterModel | - | base 190 | tough 199 | Sequential | KinPriest | 4 (ORB_OF_FRAILTY_MOVE, ORB_OF_WEAKNESS_MOVE, BEAM_MOVE, RITUAL_MOVE) | 1 (_speechUsed) | AAR, DE | Talk, Music |
| 54 | KnowledgeDemon | MonsterModel | - | base 379 | tough 399 | Conditional | KnowledgeDemon | 4 (CURSE_OF_KNOWLEDGE_MOVE, SLAP_MOVE, KNOWLEDGE_OVERWHELMING_MOVE, PONDER_MOVE) | 2 (_curseOfKnowledgeCounter, _isBurnt) | - | StatusCard, Talk, Music |
| 55 | LagavulinMatriarch | MonsterModel | - | base 222 | tough 233 | Conditional | LagavulinMatriarch | 5 (SLEEP_MOVE, SLASH_MOVE, SLASH2_MOVE, DISEMBOWEL_MOVE, SOUL_SIPHON_MOVE) | 2 (_isAwake, _sleepingVfx) | AAR, DE | - |
| 56 | LeafSlimeM | MonsterModel | - | base 32-35 | tough 33-36 | Sequential | LeafSlimeM | 2 (CLUMP_SHOT, STICKY_SHOT) | - | - | StatusCard |
| 57 | LeafSlimeS | MonsterModel | - | base 11-15 | tough 12-16 | Random | LeafSlimeS | 2 (BUTT_MOVE, GOOP_MOVE) | - | - | StatusCard |
| 58 | LivingFog | MonsterModel | - | base 80 | tough 82 | Sequential | LivingFog | 3 (ADVANCED_GAS_MOVE, BLOAT_MOVE, SUPER_GAS_BLAST_MOVE) | 1 (_bloatAmount) | - | Summon |
| 59 | LivingShield | MonsterModel | - | base 55 | tough 65 | Conditional | LivingShield | 2 (SHIELD_SLAM_MOVE, SMASH_MOVE) | - | AAR | - |
| 60 | LouseProgenitor | MonsterModel | - | base 134-136 | tough 138-141 | Sequential | LouseProgenitor | 3 (WEB_CANNON_MOVE, POUNCE_MOVE, CURL_AND_GROW_MOVE) | 1 (_curled) | AAR | - |
| 61 | MagiKnight | MonsterModel | - | base 82 | tough 89 | Sequential | MagiKnight | 5 (FIRST_POWER_SHIELD_MOVE, DAMPEN_MOVE, PREP_MOVE, MAGIC_BOMB, RAM_MOVE) | - | - | - |
| 62 | Mawler | MonsterModel | - | base 72 | tough 76 | Random | Mawler | 3 (RIP_AND_TEAR_MOVE, ROAR_MOVE, CLAW_MOVE) | - | - | - |
| 63 | MechaKnight | MonsterModel | - | base 300 | tough 320 | Sequential | MechaKnight | 4 (CHARGE_MOVE, FLAMETHROWER_MOVE, WINDUP_MOVE, HEAVY_CLEAVE_MOVE) | 1 (_isWoundUp) | AAR | StatusCard |
| 64 | MultiAttackMoveMonster | MonsterModel | - | base 999 | tough 999 | Sequential | MultiAttackMoveMonster | 1 (POKE) | - | - | TestOnly |
| 65 | MysteriousKnight | FlailKnight | - | base 101 | tough 108 | Random | FlailKnight | 3 (WAR_CHANT, FLAIL_MOVE, RAM_MOVE) | - | AAR | InheritedFSM:FlailKnight |
| 66 | Myte | MonsterModel | - | base 61-67 | tough 64-69 | Conditional | Myte | 3 (TOXIC_MOVE, BITE_MOVE, SUCK_MOVE) | - | - | StatusCard |
| 67 | Nibbit | MonsterModel | - | base 42-46 | tough 44-48 | Conditional | Nibbit | 3 (BUTT_MOVE, SLICE_MOVE, HISS_MOVE) | 2 (_isAlone, _isFront) | - | - |
| 68 | Noisebot | MonsterModel | - | base 23-28 | tough 24-29 | Sequential | Noisebot | 1 (NOISE_MOVE) | - | AAR | StatusCard |
| 69 | OneHpMonster | MonsterModel | - | base 1 | tough 1 | Sequential | OneHpMonster | 1 (NOTHING) | - | - | TestOnly |
| 70 | Osty | MonsterModel | - | base 1 | tough 1 | Sequential | Osty | 1 (NOTHING_MOVE) | - | - | - |
| 71 | Ovicopter | MonsterModel | - | base 124-130 | tough 126-132 | Conditional | Ovicopter | 4 (LAY_EGGS_MOVE, SMASH_MOVE, TENDERIZER_MOVE, NUTRITIONAL_PASTE_MOVE) | - | AAR, BRR | Summon |
| 72 | OwlMagistrate | MonsterModel | - | base 234 | tough 243 | Sequential | OwlMagistrate | 4 (MAGISTRATE_SCRUTINY, PECK_ASSAULT, JUDICIAL_FLIGHT, VERDICT) | 1 (_isFlying) | BRR | - |
| 73 | PaelsLegion | MonsterModel | - | base 9999 | tough 9999 | Sequential | PaelsLegion | 1 (NOTHING_MOVE) | - | - | - |
| 74 | Parafright | MonsterModel | - | base 21 | tough 21 | Sequential | Parafright | 1 (SLAM_MOVE) | - | AAR | - |
| 75 | PhantasmalGardener | MonsterModel | - | base 28-32 | tough 29-33 | Conditional | PhantasmalGardener | 4 (BITE_MOVE, LASH_MOVE, FLAIL_MOVE, ENLARGE_MOVE) | 1 (_enlargeTriggers) | AAR | - |
| 76 | PhrogParasite | MonsterModel | - | base 61-64 | tough 66-68 | Random | PhrogParasite | 2 (INFECT_MOVE, LASH_MOVE) | - | AAR | StatusCard |
| 77 | PunchConstruct | MonsterModel | - | base 55 | tough 60 | Sequential | PunchConstruct | 3 (READY_MOVE, STRONG_PUNCH_MOVE, FAST_PUNCH_MOVE) | 2 (_startingHpReduction, _startsWithStrongPunch) | AAR | - |
| 78 | Queen | MonsterModel | - | base 400 | tough 419 | Conditional | Queen | 6 (PUPPET_STRINGS_MOVE, YOUR_MINE_MOVE, BURN_BRIGHT_FOR_ME_MOVE, OFF_WITH_YOUR_HEAD_MOVE, EXECUTION_MOVE, ENRAGE_MOVE) | 4 (_amalgam, _burnBrightForMeState, _enragedState, _hasAmalgamDied) | AAR, BRR, DE | ForcedTransition, Talk, Music |
| 79 | Rocket | MonsterModel | - | base 189 | tough 199 | Sequential | Rocket | 5 (TARGETING_RETICLE_MOVE, PRECISION_BEAM_MOVE, CHARGE_UP_MOVE, LASER_MOVE, RECHARGE_MOVE) | - | AAR | - |
| 80 | ScrollOfBiting | MonsterModel | - | base 31-38 | tough 32-39 | Random | ScrollOfBiting | 3 (CHOMP, CHEW, MORE_TEETH) | 1 (_starterMoveIdx) | AAR | - |
| 81 | Seapunk | MonsterModel | - | base 44-46 | tough 47-49 | Sequential | Seapunk | 3 (SEA_KICK_MOVE, SPINNING_KICK_MOVE, BUBBLE_BURP_MOVE) | - | AAR | - |
| 82 | SewerClam | MonsterModel | - | base 56 | tough 58 | Sequential | SewerClam | 2 (PRESSURIZE_MOVE, JET_MOVE) | - | AAR | - |
| 83 | ShrinkerBeetle | MonsterModel | - | base 38-40 | tough 40-42 | Sequential | ShrinkerBeetle | 3 (SHRINKER_MOVE, CHOMP_MOVE, STOMP_MOVE) | - | - | - |
| 84 | SingleAttackMoveMonster | MonsterModel | - | base 999 | tough 999 | Sequential | SingleAttackMoveMonster | 1 (POKE) | - | - | TestOnly |
| 85 | SkulkingColony | MonsterModel | - | base 79 | tough 84 | Sequential | SkulkingColony | 4 (INERTIA_MOVE, ZOOM_MOVE, SUPER_CRAB_MOVE, SMASH_MOVE) | - | AAR | StatusCard |
| 86 | SlimedBerserker | MonsterModel | - | base 266 | tough 276 | Sequential | SlimedBerserker | 4 (VOMIT_ICHOR_MOVE, LEECHING_HUG_MOVE, SMOTHER_MOVE, FURIOUS_PUMMELING_MOVE) | - | - | StatusCard |
| 87 | SlitheringStrangler | MonsterModel | - | base 53-55 | tough 54-56 | Random | SlitheringStrangler | 3 (CONSTRICT, TWACK, LASH) | - | - | - |
| 88 | SludgeSpinner | MonsterModel | - | base 37-39 | tough 41-42 | Random | SludgeSpinner | 3 (OIL_SPRAY_MOVE, SLAM_MOVE, RAGE_MOVE) | - | - | - |
| 89 | SlumberingBeetle | MonsterModel | - | base 86 | tough 89 | Conditional | SlumberingBeetle | 2 (SNORE_MOVE, ROLL_OUT_MOVE) | 2 (_isAwake, _sleepingVfx) | AAR, DE | - |
| 90 | SnappingJaxfruit | MonsterModel | - | base 31-33 | tough 34-36 | Sequential | SnappingJaxfruit | 1 (ENERGY_ORB_MOVE) | 1 (_isCharged) | AAR, BRR | - |
| 91 | SneakyGremlin | MonsterModel | - | base 10-14 | tough 11-15 | Sequential | SneakyGremlin | 2 (SPAWNED_MOVE, TACKLE_MOVE) | 1 (_isAwake) | - | - |
| 92 | SoulFysh | MonsterModel | - | base 211 | tough 221 | Sequential | SoulFysh | 5 (BECKON_MOVE, DE_GAS_MOVE, GAZE_MOVE, FADE_MOVE, SCREAM_MOVE) | 1 (_isInvisible) | - | StatusCard |
| 93 | SoulNexus | MonsterModel | - | base 234 | tough 254 | Random | SoulNexus | 3 (SOUL_BURN_MOVE, MAELSTROM_MOVE, DRAIN_LIFE_MOVE) | - | AAR, BRR, DE | - |
| 94 | SpectralKnight | MonsterModel | - | base 93 | tough 97 | Random | SpectralKnight | 3 (HEX, SOUL_SLASH, SOUL_FLAME) | - | - | - |
| 95 | SpinyToad | MonsterModel | - | base 116-119 | tough 121-124 | Sequential | SpinyToad | 3 (PROTRUDING_SPIKES_MOVE, SPIKE_EXPLOSION_MOVE, TONGUE_LASH_MOVE) | 1 (_isSpiny) | AAR | - |
| 96 | Stabbot | MonsterModel | - | base 23-28 | tough 24-29 | Sequential | Stabbot | 1 (STAB_MOVE) | - | AAR | - |
| 97 | TenHpMonster | MonsterModel | - | base 10 | tough 10 | Sequential | TenHpMonster | 1 (NOTHING) | - | - | TestOnly |
| 98 | TerrorEel | MonsterModel | - | base 140 | tough 150 | Sequential | TerrorEel | 4 (CRASH_MOVE, ThrashMove, STUN_MOVE, TERROR_MOVE) | 1 (_terrorState) | AAR | - |
| 99 | TestSubject | MonsterModel | - | base 100 | tough 111 | Conditional | TestSubject | 8 (RESPAWN_MOVE, BITE_MOVE, SKULL_BASH_MOVE, POUNCE_MOVE, MULTI_CLAW_MOVE, PHASE3_LACERATE_MOVE, BIG_POUNCE_MOVE, BURNING_GROWL_MOVE) | 3 (_deadState, _extraMultiClawCount, _respawns) | AAR, DE | StatusCard, ForcedTransition, Music, MustPerformOnce |
| 100 | TheAdversaryMkOne | MonsterModel | - | base 100 | tough 100 | Sequential | TheAdversaryMkOne | 3 (SMASH_MOVE, BEAM_MOVE, BARRAGE_MOVE) | - | AAR | - |
| 101 | TheAdversaryMkThree | MonsterModel | - | base 300 | tough 300 | Sequential | TheAdversaryMkThree | 3 (CRASH_MOVE, FLAME_BEAM_MOVE, BARRAGE_MOVE) | - | AAR | - |
| 102 | TheAdversaryMkTwo | MonsterModel | - | base 200 | tough 200 | Sequential | TheAdversaryMkTwo | 3 (BASH_MOVE, FLAME_BEAM_MOVE, BARRAGE_MOVE) | - | AAR | - |
| 103 | TheForgotten | MonsterModel | - | base 106 | tough 111 | Sequential | TheForgotten | 2 (MIASMA, DREAD) | - | AAR | - |
| 104 | TheInsatiable | MonsterModel | - | base 321 | tough 341 | Sequential | TheInsatiable | 5 (LIQUIFY_GROUND_MOVE, THRASH_MOVE_1, THRASH_MOVE_2, LUNGING_BITE_MOVE, SALIVATE_MOVE) | 1 (_hasLiquified) | AAR, DE | StatusCard, Music |
| 105 | TheLost | MonsterModel | - | base 93 | tough 99 | Sequential | TheLost | 2 (DEBILITATING_SMOG, EYE_LASERS) | - | AAR | - |
| 106 | TheObscura | MonsterModel | - | base 123 | tough 129 | Random | TheObscura | 4 (ILLUSION_MOVE, PIERCING_GAZE_MOVE, SAIL_MOVE, HARDENING_STRIKE_MOVE) | 1 (_hasSummoned) | - | Summon |
| 107 | ThievingHopper | MonsterModel | - | base 79 | tough 84 | Sequential | ThievingHopper | 5 (THIEVERY_MOVE, NAB_MOVE, HAT_TRICK_MOVE, FLUTTER_MOVE, ESCAPE_MOVE) | 1 (_isHovering) | AAR, BRR | - |
| 108 | Toadpole | MonsterModel | - | base 21-25 | tough 22-26 | Conditional | Toadpole | 3 (SPIKE_SPIT_MOVE, WHIRL_MOVE, SPIKEN_MOVE) | 1 (_isFront) | - | - |
| 109 | TorchHeadAmalgam | MonsterModel | - | base 199 | tough 211 | Sequential | TorchHeadAmalgam | 5 (TACKLE_1_MOVE, TACKLE_2_MOVE, BEAM_MOVE, TACKLE_3_MOVE, TACKLE_4_MOVE) | - | AAR, ODT | - |
| 110 | ToughEgg | MonsterModel | - | base 14-18 | tough 15-19 | Sequential | ToughEgg | 2 (HATCH_MOVE, NIBBLE_MOVE) | 3 (_afterHatchedState, _hatchPos, _isHatched) | AAR | ForcedTransition |
| 111 | TrackerRubyRaider | MonsterModel | - | base 21-25 | tough 22-26 | Sequential | TrackerRubyRaider | 2 (TRACK_MOVE, HOUNDS_MOVE) | - | - | - |
| 112 | Tunneler | MonsterModel | - | base 87 | tough 92 | Sequential | Tunneler | 4 (BITE_MOVE, BURROW_MOVE, BELOW_MOVE_1, DIZZY_MOVE) | - | - | - |
| 113 | TurretOperator | MonsterModel | - | base 41 | tough 51 | Sequential | TurretOperator | 3 (UNLOAD_MOVE_1, UNLOAD_MOVE_2, RELOAD_MOVE) | - | - | - |
| 114 | TwigSlimeM | MonsterModel | - | base 26-28 | tough 27-29 | Random | TwigSlimeM | 2 (CLUMP_SHOT_MOVE, STICKY_SHOT_MOVE) | - | - | StatusCard |
| 115 | TwigSlimeS | MonsterModel | - | base 7-11 | tough 8-12 | Sequential | TwigSlimeS | 1 (BUTT_MOVE) | - | - | - |
| 116 | TwoTailedRat | MonsterModel | - | base 17-21 | tough 18-22 | Random | TwoTailedRat | 4 (SCRATCH_MOVE, DISEASE_BITE_MOVE, SCREECH_MOVE, CALL_FOR_BACKUP_MOVE) | 3 (_callForBackupCount, _starterMoveIndex, _turnsUntilSummonable) | AAR | Summon |
| 117 | Vantom | MonsterModel | - | base 173 | tough 183 | Sequential | Vantom | 4 (INK_BLOT_MOVE, INKY_LANCE_MOVE, DISMEMBER_MOVE, PREPARE_MOVE) | - | AAR, DE | StatusCard, Music |
| 118 | VineShambler | MonsterModel | - | base 61 | tough 64 | Sequential | VineShambler | 3 (GRASPING_VINES_MOVE, SWIPE_MOVE, CHOMP_MOVE) | - | - | - |
| 119 | WaterfallGiant | MonsterModel | - | base 250 | tough 260 | Sequential | WaterfallGiant | 8 (PRESSURIZE_MOVE, STOMP_MOVE, RAM_MOVE, SIPHON_MOVE, PRESSURE_GUN_MOVE, PRESSURE_UP_MOVE, ABOUT_TO_BLOW_MOVE, EXPLODE_MOVE) | 5 (_aboutToBlowState, _currentPressureGunDamage, _isAboutToBlow, _pressureBuildupIdx, _steamEruptionDamage) | AAR, BRR, DE | ForcedTransition, Music, MustPerformOnce |
| 120 | Wriggler | MonsterModel | - | base 17-21 | tough 18-22 | Conditional | Wriggler | 3 (NASTY_BITE_MOVE, WRIGGLE_MOVE, SPAWNED_MOVE) | 1 (_startStunned) | - | StatusCard |
| 121 | Zapbot | MonsterModel | - | base 23-28 | tough 24-29 | Sequential | Zapbot | 1 (ZAP) | - | AAR | - |

## Notes

- `DE` in the lifecycle column means the type subscribes to a `Died` event somewhere in its effective inheritance chain.
- `FSM Source` identifies the class that actually provides the effective `GenerateMoveStateMachine()` implementation.
- The capability matrix in `monster_ai_gap_matrix.md` is a structural source scan, not a runtime proof. Dynamic lambdas and helper indirection are still called out conservatively.
