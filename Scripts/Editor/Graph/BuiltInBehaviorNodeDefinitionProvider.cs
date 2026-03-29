using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rooms;

namespace STS2_Editor.Scripts.Editor.Graph;

public sealed class BuiltInBehaviorNodeDefinitionProvider : IBehaviorNodeDefinitionProvider
{
    public IEnumerable<BehaviorGraphNodeDefinitionDescriptor> GetDefinitions()
    {
        yield return FlowEntry();
        yield return FlowExit();
        yield return Sequence();
        yield return Branch();
        yield return SetValue();
        yield return AddValue();
        yield return MultiplyValue();
        yield return Compare();
        yield return RandomChoice();
        yield return LogMessage();
        yield return SelectCards();
        yield return DamageTarget();
        yield return GainBlock();
        yield return Heal();
        yield return DrawCards();
        yield return ApplyPower();
        yield return GainEnergy();
        yield return GainStars();
        yield return GainGold();
        yield return LoseEnergy();
        yield return LoseGold();
        yield return GainMaxPotionCount();
        yield return DiscardCards();
        yield return ExhaustCards();
        yield return CreateCard();
        yield return MoveCards();
        yield return RemoveCard();
        yield return TransformCard();
        yield return DiscardAndDraw();
        yield return ApplyCardKeyword();
        yield return RemoveCardKeyword();
        yield return UpgradeCard();
        yield return DowngradeCard();
        yield return EnchantCard();
        yield return AutoPlayCard();
        yield return ApplySingleTurnSly();
        yield return AutoPlayFromDrawPile();
        yield return ChannelOrb();
        yield return OrbPassive();
        yield return AddOrbSlots();
        yield return RemoveOrbSlots();
        yield return EvokeNextOrb();
        yield return ProcurePotion();
        yield return DiscardPotion();
        yield return ObtainRelic();
        yield return RemoveRelic();
        yield return ReplaceRelic();
        yield return MeltRelic();
        yield return AddPet();
        yield return Forge();
        yield return CompleteQuest();
        yield return MimicRestHeal();
        yield return EndTurn();
        yield return Repeat();
        yield return LoseBlock();
        yield return LoseHp();
        yield return GainMaxHp();
        yield return LoseMaxHp();
        yield return SetCurrentHp();
        yield return CreatureKill();
        yield return CreatureStun();
        yield return RemovePower();
        yield return ModifyPowerAmount();
        yield return ShuffleCardPile();
        yield return ModifierDamageAdditive();
        yield return ModifierDamageMultiplicative();
        yield return ModifierBlockAdditive();
        yield return ModifierBlockMultiplicative();
        yield return ModifierPlayCount();
        yield return ModifierHandDraw();
        yield return ModifierXValue();
        yield return ModifierMaxEnergy();
        yield return EnchantmentSetStatus();
        yield return CardSetCostDelta();
        yield return CardSetCostAbsolute();
        yield return CardSetCostThisCombat();
        yield return CardAddCostUntilPlayed();
        yield return EventPage();
        yield return EventOption();
        yield return EventGotoPage();
        yield return EventProceed();
        yield return EventStartCombat();
        yield return EventReward();
        yield return OfferCustomReward();
        yield return MarkCardRewardsRerollable();
        yield return CardRewardOptionsUpgrade();
        yield return CardRewardOptionsEnchant();
        yield return ReplaceGeneratedMap();
        yield return RemoveUnknownRoomType();
        yield return MonsterAttack();
        yield return MonsterGainBlock();
        yield return MonsterApplyPower();
        yield return MonsterHeal();
        yield return MonsterSummon();
        yield return MonsterTalk();
        yield return MonsterEscape();
        yield return MonsterInjectStatusCard();
        yield return MonsterSetState();
        yield return MonsterGetState();
        yield return MonsterCheckState();
        yield return MonsterAnimate();
        yield return MonsterPlaySfx();
        yield return MonsterRemovePlayerCard();
        yield return MonsterCheckAllyAlive();
        yield return MonsterCountAllies();
        yield return MonsterForceTransition();
    }

    private static BehaviorGraphNodeDefinitionDescriptor FlowEntry()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "flow.entry",
            DisplayName = "Entry",
            Description = "Graph entry point.",
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "next",
                    DisplayName = "Next",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor FlowExit()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "flow.exit",
            DisplayName = "Exit",
            Description = "Graph termination point.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor Sequence()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "flow.sequence",
            DisplayName = "Sequence",
            Description = "Executes each output branch in order.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "first",
                    DisplayName = "First",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                },
                new BehaviorGraphPortDefinition
                {
                    PortId = "next",
                    DisplayName = "Next",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor Branch()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "flow.branch",
            DisplayName = "Branch",
            Description = "Splits execution based on a boolean condition.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                },
                new BehaviorGraphPortDefinition
                {
                    PortId = "condition",
                    DisplayName = "Condition",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "bool"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "true",
                    DisplayName = "True",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                },
                new BehaviorGraphPortDefinition
                {
                    PortId = "false",
                    DisplayName = "False",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor SetValue()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "value.set",
            DisplayName = "Set Value",
            Description = "Stores a value in graph state.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                },
                new BehaviorGraphPortDefinition
                {
                    PortId = "key",
                    DisplayName = "Key",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "string"
                },
                new BehaviorGraphPortDefinition
                {
                    PortId = "value",
                    DisplayName = "Value",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "any"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor AddValue()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "value.add",
            DisplayName = "Add Value",
            Description = "Adds a numeric delta to a stored value.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                },
                new BehaviorGraphPortDefinition
                {
                    PortId = "key",
                    DisplayName = "Key",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "string"
                },
                new BehaviorGraphPortDefinition
                {
                    PortId = "delta",
                    DisplayName = "Delta",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "number"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor MultiplyValue()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "value.multiply",
            DisplayName = "Multiply Value",
            Description = "Multiplies a stored numeric value by a factor.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                },
                new BehaviorGraphPortDefinition
                {
                    PortId = "key",
                    DisplayName = "Key",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "string"
                },
                new BehaviorGraphPortDefinition
                {
                    PortId = "factor",
                    DisplayName = "Factor",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "number"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor Compare()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "value.compare",
            DisplayName = "Compare",
            Description = "Compares two values.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                },
                new BehaviorGraphPortDefinition
                {
                    PortId = "left",
                    DisplayName = "Left",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "any"
                },
                new BehaviorGraphPortDefinition
                {
                    PortId = "right",
                    DisplayName = "Right",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "any"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["operator"] = "eq",
                ["result_key"] = "last_compare"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor RandomChoice()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "flow.random_choice",
            DisplayName = "Random Choice",
            Description = "Selects one of several branches.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor LogMessage()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "debug.log",
            DisplayName = "Log Message",
            Description = "Writes a message to the log.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                },
                new BehaviorGraphPortDefinition
                {
                    PortId = "message",
                    DisplayName = "Message",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "string"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor SelectCards()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "card.select_cards",
            DisplayName = "Select Cards",
            Description = "Selects cards from a pile or custom list and stores them in graph state.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["state_key"] = "selected_cards",
                ["selection_mode"] = "simple_grid",
                ["source_pile"] = PileType.Deck.ToString(),
                ["count"] = "1",
                ["prompt_kind"] = "generic",
                ["allow_cancel"] = bool.FalseString,
                ["enchantment_id"] = string.Empty
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor DamageTarget()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "combat.damage",
            DisplayName = "Damage",
            Description = "Deals damage to one or more targets.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = "0",
                ["target"] = "current_target",
                ["props"] = "none"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor GainBlock()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "combat.gain_block",
            DisplayName = "Gain Block",
            Description = "Grants block to one or more targets.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = "0",
                ["target"] = "self",
                ["props"] = "none"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor Heal()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "combat.heal",
            DisplayName = "Heal",
            Description = "Heals one or more targets.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = "0",
                ["target"] = "self"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor DrawCards()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "combat.draw_cards",
            DisplayName = "Draw Cards",
            Description = "Draws cards for the owner player.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = "1"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor ApplyPower()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "combat.apply_power",
            DisplayName = "Apply Power",
            Description = "Applies a power model by id to one or more targets.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["power_id"] = string.Empty,
                ["amount"] = "1",
                ["target"] = "current_target"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor GainEnergy()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "player.gain_energy",
            DisplayName = "Gain Energy",
            Description = "Gives energy to the current owner player.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = "1"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor GainStars()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "player.gain_stars",
            DisplayName = "Gain Stars",
            Description = "Gives stars to the current owner player.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = "1"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor GainGold()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "player.gain_gold",
            DisplayName = "Gain Gold",
            Description = "Gives gold to the current owner player.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = "1"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor LoseEnergy()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "player.lose_energy",
            DisplayName = "Lose Energy",
            Description = "Removes energy from the owner player.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = "1"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor LoseGold()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "player.lose_gold",
            DisplayName = "Lose Gold",
            Description = "Removes gold from the owner player.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = "1",
                ["gold_loss_type"] = GoldLossType.Lost.ToString()
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor GainMaxPotionCount()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "player.gain_max_potion_count",
            DisplayName = "Gain Max Potion Count",
            Description = "Increases the owner's potion capacity.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = "1"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor DiscardCards()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "combat.discard_cards",
            DisplayName = "Discard Cards",
            Description = "Discards cards from hand.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = "1",
                ["card_ids"] = string.Empty,
                ["target"] = "hand"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor ExhaustCards()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "combat.exhaust_cards",
            DisplayName = "Exhaust Cards",
            Description = "Exhausts cards from hand or the selected card list.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = "1",
                ["card_ids"] = string.Empty,
                ["target"] = "hand"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor CreateCard()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "combat.create_card",
            DisplayName = "Create Card",
            Description = "Creates a card copy and adds it to the selected pile.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["card_id"] = string.Empty,
                ["count"] = "1",
                ["target_pile"] = "hand"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor MoveCards()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "cardpile.move_cards",
            DisplayName = "Move Cards",
            Description = "Moves existing cards between piles using optional pile, cost, and type filters.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["source_pile"] = "Discard",
                ["target_pile"] = "Hand",
                ["count"] = "0",
                ["exact_energy_cost"] = "-1",
                ["include_x_cost"] = bool.FalseString,
                ["card_type_scope"] = "attack_skill_power"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor RemoveCard()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "combat.remove_card",
            DisplayName = "Remove Card",
            Description = "Removes cards from the selected location.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["card_ids"] = string.Empty,
                ["target"] = "current"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor TransformCard()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "combat.transform_card",
            DisplayName = "Transform Card",
            Description = "Transforms a card into another one.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["card_id"] = string.Empty,
                ["card_state_key"] = "selected_cards",
                ["replacement_card_id"] = string.Empty,
                ["random_replacement"] = "false"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor DiscardAndDraw()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "card.discard_and_draw",
            DisplayName = "Discard And Draw",
            Description = "Discards the selected cards and then draws cards.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["card_state_key"] = "selected_cards",
                ["draw_count"] = "0"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor ApplyCardKeyword()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "card.apply_keyword",
            DisplayName = "Apply Card Keyword",
            Description = "Adds a keyword to the selected cards.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["card_state_key"] = "selected_cards",
                ["keyword"] = string.Empty
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor RemoveCardKeyword()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "card.remove_keyword",
            DisplayName = "Remove Card Keyword",
            Description = "Removes a keyword from the selected cards.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["card_state_key"] = "selected_cards",
                ["keyword"] = string.Empty
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor UpgradeCard()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "card.upgrade",
            DisplayName = "Upgrade Card",
            Description = "Upgrades the selected card(s).",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["card_preview_style"] = CardPreviewStyle.HorizontalLayout.ToString()
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor DowngradeCard()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "card.downgrade",
            DisplayName = "Downgrade Card",
            Description = "Downgrades the selected card(s).",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor EnchantCard()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "card.enchant",
            DisplayName = "Enchant Card",
            Description = "Applies an enchantment to the selected card(s).",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["enchantment_id"] = string.Empty,
                ["amount"] = "1"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor AutoPlayCard()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "card.autoplay",
            DisplayName = "Auto Play Card",
            Description = "Automatically plays the selected card.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["card_state_key"] = "selected_cards",
                ["auto_play_type"] = AutoPlayType.Default.ToString(),
                ["target"] = "current_target",
                ["skip_x_capture"] = bool.FalseString,
                ["skip_card_pile_visuals"] = bool.FalseString
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor ApplySingleTurnSly()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "card.apply_single_turn_sly",
            DisplayName = "Apply Single-Turn Sly",
            Description = "Grants single-turn Sly to the selected card(s).",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["card_state_key"] = "selected_cards"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor AutoPlayFromDrawPile()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "cardpile.auto_play_from_draw_pile",
            DisplayName = "Auto Play From Draw Pile",
            Description = "Automatically plays cards pulled from the draw pile.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["count"] = "1",
                ["position"] = CardPilePosition.Bottom.ToString(),
                ["force_exhaust"] = bool.FalseString
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor ChannelOrb()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "orb.channel",
            DisplayName = "Channel Orb",
            Description = "Channels an orb for the owner player.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["orb_id"] = string.Empty
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor OrbPassive()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "orb.passive",
            DisplayName = "Orb Passive",
            Description = "Triggers an orb's passive effect.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["orb_id"] = string.Empty,
                ["target"] = "current_target"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor AddOrbSlots()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "orb.add_slots",
            DisplayName = "Add Orb Slots",
            Description = "Adds orb slots to the owner player.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = "1"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor RemoveOrbSlots()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "orb.remove_slots",
            DisplayName = "Remove Orb Slots",
            Description = "Removes orb slots from the owner player.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = "1"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor EvokeNextOrb()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "orb.evoke_next",
            DisplayName = "Evoke Next Orb",
            Description = "Evokes the next orb in the owner's orb queue.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["dequeue"] = bool.TrueString
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor ProcurePotion()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "potion.procure",
            DisplayName = "Procure Potion",
            Description = "Adds a potion to the owner's potion belt.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["potion_id"] = string.Empty
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor DiscardPotion()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "potion.discard",
            DisplayName = "Discard Potion",
            Description = "Discards the selected potion or the current potion context.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["potion_id"] = string.Empty
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor ObtainRelic()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "relic.obtain",
            DisplayName = "Obtain Relic",
            Description = "Adds a relic to the owner player.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["relic_id"] = string.Empty
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor RemoveRelic()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "relic.remove",
            DisplayName = "Remove Relic",
            Description = "Removes a relic from the owner player.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["relic_id"] = string.Empty
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor ReplaceRelic()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "relic.replace",
            DisplayName = "Replace Relic",
            Description = "Replaces one relic with another.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["relic_id"] = string.Empty,
                ["replacement_relic_id"] = string.Empty
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor MeltRelic()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "relic.melt",
            DisplayName = "Melt Relic",
            Description = "Melts a relic owned by the player.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["relic_id"] = string.Empty
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor AddPet()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "player.add_pet",
            DisplayName = "Add Pet",
            Description = "Summons or adds a pet creature for the owner player.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["monster_id"] = string.Empty
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor Forge()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "player.forge",
            DisplayName = "Forge",
            Description = "Triggers the Sovereign Blade forge flow for the owner player.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = "1"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor CompleteQuest()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "player.complete_quest",
            DisplayName = "Complete Quest",
            Description = "Completes the current quest card.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor MimicRestHeal()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "player.rest_heal",
            DisplayName = "Rest Site Heal",
            Description = "Applies the rest site heal effect to the owner player.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["play_sfx"] = bool.TrueString
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor EndTurn()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "player.end_turn",
            DisplayName = "End Turn",
            Description = "Ends the current turn for the owner player.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor LoseBlock()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "combat.lose_block",
            DisplayName = "Lose Block",
            Description = "Removes block from the selected target.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = "0",
                ["target"] = "current_target"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor Repeat()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "combat.repeat",
            DisplayName = "Repeat",
            Description = "Repeats the outgoing branch a number of times.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["count"] = "1"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor LoseHp()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "player.lose_hp",
            DisplayName = "Lose HP",
            Description = "Deals unblockable damage to the target creature.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = "0",
                ["target"] = "self",
                ["props"] = "none"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor GainMaxHp()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "player.gain_max_hp",
            DisplayName = "Gain Max HP",
            Description = "Increases the target creature's max HP.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = "0",
                ["target"] = "self"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor LoseMaxHp()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "player.lose_max_hp",
            DisplayName = "Lose Max HP",
            Description = "Reduces the selected creature's max HP.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = "0",
                ["target"] = "self",
                ["is_from_card"] = bool.FalseString
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor SetCurrentHp()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "creature.set_current_hp",
            DisplayName = "Set Current HP",
            Description = "Sets the current HP of the selected target.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = "0",
                ["target"] = "self"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor CreatureKill()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "creature.kill",
            DisplayName = "Kill Creature",
            Description = "Kills the selected creature.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["target"] = "current_target",
                ["force"] = bool.FalseString
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor CreatureStun()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "creature.stun",
            DisplayName = "Stun Creature",
            Description = "Stuns the selected creature.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["target"] = "current_target",
                ["next_move_id"] = string.Empty
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor RemovePower()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "power.remove",
            DisplayName = "Remove Power",
            Description = "Removes a power from the selected creature.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["power_id"] = string.Empty,
                ["target"] = "current_target"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor ModifyPowerAmount()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "power.modify_amount",
            DisplayName = "Modify Power Amount",
            Description = "Adjusts the amount of an existing power.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["power_id"] = string.Empty,
                ["amount"] = "1",
                ["target"] = "current_target",
                ["silent"] = bool.FalseString
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor ShuffleCardPile()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "cardpile.shuffle",
            DisplayName = "Shuffle",
            Description = "Shuffles the owner's draw and discard piles.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor ModifierDamageAdditive()
    {
        return CreateModifierNode("modifier.damage_additive", "Damage Additive", "Provides an additive damage modifier.", "amount", "0");
    }

    private static BehaviorGraphNodeDefinitionDescriptor ModifierDamageMultiplicative()
    {
        return CreateModifierNode("modifier.damage_multiplicative", "Damage Multiplier", "Provides a multiplicative damage modifier.", "amount", "1");
    }

    private static BehaviorGraphNodeDefinitionDescriptor ModifierBlockAdditive()
    {
        return CreateModifierNode("modifier.block_additive", "Block Additive", "Provides an additive block modifier.", "amount", "0");
    }

    private static BehaviorGraphNodeDefinitionDescriptor ModifierBlockMultiplicative()
    {
        return CreateModifierNode("modifier.block_multiplicative", "Block Multiplier", "Provides a multiplicative block modifier.", "amount", "1");
    }

    private static BehaviorGraphNodeDefinitionDescriptor ModifierPlayCount()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "modifier.play_count",
            DisplayName = "Play Count Modifier",
            Description = "Provides an additional or absolute card play count.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = "0",
                ["mode"] = "delta"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor ModifierHandDraw()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "modifier.hand_draw",
            DisplayName = "Hand Draw Modifier",
            Description = "Provides an additive or absolute hand draw modification.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = "0",
                ["mode"] = "delta"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor ModifierXValue()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "modifier.x_value",
            DisplayName = "X Value Modifier",
            Description = "Provides an additive or absolute X-value modification.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = "0",
                ["mode"] = "delta"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor ModifierMaxEnergy()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "modifier.max_energy",
            DisplayName = "Max Energy Modifier",
            Description = "Provides an additive or absolute max-energy modification.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = "0",
                ["mode"] = "delta"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor EnchantmentSetStatus()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "enchantment.set_status",
            DisplayName = "Set Enchantment Status",
            Description = "Sets the active status of the current enchantment.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["status"] = "Disabled"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor CardSetCostDelta()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "card.set_cost_delta",
            DisplayName = "Set Cost Delta",
            Description = "Adjusts the selected card energy cost by a relative amount.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = "-1",
                ["card_state_key"] = "selected_cards"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor CardSetCostAbsolute()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "card.set_cost_absolute",
            DisplayName = "Set Cost Absolute",
            Description = "Sets the selected card energy cost to an absolute value.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = "0",
                ["card_state_key"] = "selected_cards"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor CardSetCostThisCombat()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "card.set_cost_this_combat",
            DisplayName = "Set Cost This Combat",
            Description = "Sets the selected card energy cost for the rest of the combat.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = "0",
                ["card_state_key"] = "selected_cards"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor CardAddCostUntilPlayed()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "card.add_cost_until_played",
            DisplayName = "Add Cost Until Played",
            Description = "Adds a relative energy cost modifier until the selected card is played.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = "-1",
                ["card_state_key"] = "selected_cards"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor CreateModifierNode(
        string nodeType,
        string displayName,
        string description,
        string propertyKey,
        string defaultValue)
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = nodeType,
            DisplayName = displayName,
            Description = description,
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [propertyKey] = defaultValue
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor EventPage()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "event.page",
            DisplayName = "Event Page",
            Description = "Defines an event page and its option order.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "next",
                    DisplayName = "Next",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["page_id"] = string.Empty,
                ["title"] = string.Empty,
                ["description"] = string.Empty,
                ["is_start"] = "false",
                ["option_order"] = string.Empty
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor EventOption()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "event.option",
            DisplayName = "Event Option",
            Description = "Defines an event choice and its outcome.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["page_id"] = string.Empty,
                ["option_id"] = string.Empty,
                ["title"] = string.Empty,
                ["description"] = string.Empty,
                ["next_page_id"] = string.Empty,
                ["encounter_id"] = string.Empty,
                ["resume_page_id"] = string.Empty,
                ["is_proceed"] = "false",
                ["save_choice_to_history"] = "true",
                ["reward_kind"] = string.Empty,
                ["reward_amount"] = string.Empty,
                ["reward_target"] = string.Empty,
                ["reward_props"] = string.Empty,
                ["reward_power_id"] = string.Empty
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor EventGotoPage()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "event.goto_page",
            DisplayName = "Go To Page",
            Description = "Routes the event to a different page.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["next_page_id"] = string.Empty
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor EventProceed()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "event.proceed",
            DisplayName = "Proceed",
            Description = "Ends the current event interaction.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor EventStartCombat()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "event.start_combat",
            DisplayName = "Start Combat",
            Description = "Starts combat from an event choice.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["encounter_id"] = string.Empty,
                ["resume_page_id"] = string.Empty
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor EventReward()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "event.reward",
            DisplayName = "Event Reward",
            Description = "Defines a simple event reward payload.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["reward_kind"] = string.Empty,
                ["reward_amount"] = string.Empty,
                ["reward_target"] = string.Empty,
                ["reward_props"] = string.Empty,
                ["reward_power_id"] = string.Empty
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor OfferCustomReward()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "reward.offer_custom",
            DisplayName = "Offer Custom Reward",
            Description = "Offers a reward screen entry such as gold, relic, potion, or a special card.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["reward_kind"] = "custom",
                ["amount"] = "0",
                ["reward_count"] = "1",
                ["card_count"] = "3",
                ["reward_room_type"] = string.Empty,
                ["card_id"] = string.Empty,
                ["relic_id"] = string.Empty,
                ["potion_id"] = string.Empty
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor MarkCardRewardsRerollable()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "reward.mark_card_rewards_rerollable",
            DisplayName = "Mark Card Rewards Rerollable",
            Description = "Marks all card rewards in the current reward list as rerollable.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor ReplaceGeneratedMap()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "map.replace_generated",
            DisplayName = "Replace Generated Map",
            Description = "Replaces the current generated map with a built-in variant.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["map_kind"] = string.Empty
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor RemoveUnknownRoomType()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "map.remove_unknown_room_type",
            DisplayName = "Remove Unknown Room Type",
            Description = "Removes a room type from the current unknown-map-point room type set.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["room_type"] = RoomType.Monster.ToString()
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor CardRewardOptionsUpgrade()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "reward.card_options_upgrade",
            DisplayName = "Upgrade Card Reward Options",
            Description = "Upgrades matching cards in the current card reward option list.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["card_type_scope"] = "any",
                ["require_hook_upgrades_enabled"] = bool.FalseString
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor CardRewardOptionsEnchant()
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = "reward.card_options_enchant",
            DisplayName = "Enchant Card Reward Options",
            Description = "Applies an enchantment to matching cards in the current card reward option list.",
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["enchantment_id"] = string.Empty,
                ["amount"] = "1",
                ["selection"] = "all"
            }
        };
    }

    private static BehaviorGraphNodeDefinitionDescriptor MonsterAttack()
    {
        return CreateMonsterNode("monster.attack", "Monster Attack", "Deals monster attack damage using the current monster as the attacker.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = "0",
                ["target"] = "current_target",
                ["hit_count"] = "1"
            });
    }

    private static BehaviorGraphNodeDefinitionDescriptor MonsterGainBlock()
    {
        return CreateMonsterNode("monster.gain_block", "Monster Gain Block", "Grants block from a monster-authored move.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = "0",
                ["target"] = "self"
            });
    }

    private static BehaviorGraphNodeDefinitionDescriptor MonsterApplyPower()
    {
        return CreateMonsterNode("monster.apply_power", "Monster Apply Power", "Applies a power from a monster-authored move.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["power_id"] = string.Empty,
                ["amount"] = "1",
                ["target"] = "current_target"
            });
    }

    private static BehaviorGraphNodeDefinitionDescriptor MonsterHeal()
    {
        return CreateMonsterNode("monster.heal", "Monster Heal", "Heals the selected target from a monster-authored move.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = "0",
                ["target"] = "self"
            });
    }

    private static BehaviorGraphNodeDefinitionDescriptor MonsterSummon()
    {
        return CreateMonsterNode("monster.summon", "Monster Summon", "Summons another monster into the current combat.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["monster_id"] = string.Empty
            });
    }

    private static BehaviorGraphNodeDefinitionDescriptor MonsterTalk()
    {
        return CreateMonsterNode("monster.talk", "Monster Talk", "Displays a speech bubble for the current monster.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["text"] = string.Empty,
                ["duration"] = "1.5"
            });
    }

    private static BehaviorGraphNodeDefinitionDescriptor MonsterEscape()
    {
        return CreateMonsterNode("monster.escape", "Monster Escape", "Makes the current monster escape from combat.",
            new Dictionary<string, string>(StringComparer.Ordinal));
    }

    private static BehaviorGraphNodeDefinitionDescriptor MonsterInjectStatusCard()
    {
        return CreateMonsterNode("monster.inject_status_card", "Monster Inject Status Card", "Creates status cards for target players and places them into the selected pile.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["card_id"] = string.Empty,
                ["count"] = "1",
                ["target_pile"] = PileType.Discard.ToString(),
                ["target"] = "all_enemies"
            });
    }

    private static BehaviorGraphNodeDefinitionDescriptor MonsterSetState()
    {
        return CreateMonsterNode("monster.set_state", "Monster Set State", "Writes a value into monster runtime state.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["variable_name"] = string.Empty,
                ["value"] = string.Empty
            });
    }

    private static BehaviorGraphNodeDefinitionDescriptor MonsterGetState()
    {
        return CreateMonsterNode("monster.get_state", "Monster Get State", "Reads a value from monster runtime state into graph state.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["variable_name"] = string.Empty,
                ["result_key"] = "monster_state_value"
            });
    }

    private static BehaviorGraphNodeDefinitionDescriptor MonsterCheckState()
    {
        return CreateMonsterNode("monster.check_state", "Monster Check State", "Compares a monster runtime state variable and stores the boolean result.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["variable_name"] = string.Empty,
                ["operator"] = "eq",
                ["value"] = string.Empty,
                ["result_key"] = "monster_state_check"
            });
    }

    private static BehaviorGraphNodeDefinitionDescriptor MonsterAnimate()
    {
        return CreateMonsterNode("monster.animate", "Monster Animate", "Triggers a monster animation.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["animation_id"] = "Attack",
                ["wait_duration"] = "0"
            });
    }

    private static BehaviorGraphNodeDefinitionDescriptor MonsterPlaySfx()
    {
        return CreateMonsterNode("monster.play_sfx", "Monster Play Sfx", "Plays a monster move sound effect.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["sfx_path"] = string.Empty
            });
    }

    private static BehaviorGraphNodeDefinitionDescriptor MonsterRemovePlayerCard()
    {
        return CreateMonsterNode("monster.remove_player_card", "Monster Remove Player Card", "Removes matching player cards from deck or combat piles.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["card_id"] = string.Empty,
                ["count"] = "1",
                ["target"] = "current_target"
            });
    }

    private static BehaviorGraphNodeDefinitionDescriptor MonsterCheckAllyAlive()
    {
        return CreateMonsterNode("monster.check_ally_alive", "Monster Check Ally Alive", "Stores whether a matching ally monster is alive.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["monster_id"] = string.Empty,
                ["result_key"] = "monster_ally_alive"
            });
    }

    private static BehaviorGraphNodeDefinitionDescriptor MonsterCountAllies()
    {
        return CreateMonsterNode("monster.count_allies", "Monster Count Allies", "Stores the number of living allied creatures.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["result_key"] = "monster_ally_count"
            });
    }

    private static BehaviorGraphNodeDefinitionDescriptor MonsterForceTransition()
    {
        return CreateMonsterNode("monster.force_transition", "Monster Force Transition", "Forces the current monster runtime to transition to another authored turn.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["target_turn_id"] = string.Empty
            });
    }

    private static BehaviorGraphNodeDefinitionDescriptor CreateMonsterNode(
        string nodeType,
        string displayName,
        string description,
        Dictionary<string, string> defaultProperties)
    {
        return new BehaviorGraphNodeDefinitionDescriptor
        {
            NodeType = nodeType,
            DisplayName = displayName,
            Description = description,
            Inputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "in",
                    DisplayName = "In",
                    Direction = BehaviorGraphPortDirection.Input,
                    ValueType = "flow"
                }
            },
            Outputs = new[]
            {
                new BehaviorGraphPortDefinition
                {
                    PortId = "out",
                    DisplayName = "Out",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "flow"
                }
            },
            DefaultProperties = defaultProperties
        };
    }

}
