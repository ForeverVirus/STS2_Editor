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
        yield return Compare();
        yield return RandomChoice();
        yield return LogMessage();
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
                    PortId = "equal",
                    DisplayName = "Equal",
                    Direction = BehaviorGraphPortDirection.Output,
                    ValueType = "bool"
                }
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
}
