# AGENTS

Repository workflow preferences for Codex agents.

## Plan Execution Workflow

When a plan has been agreed and implementation has started, treat the plan as an execution commitment:

- Continue working through the plan until every planned item is completed.
- Do not stop after a partial implementation just to hand back progress unless the user explicitly asks to pause.
- After finishing one plan item, immediately continue to the next item.
- If new information requires changing the plan, update the plan and keep executing instead of stopping at the replan step.
- Keep the user informed with concise progress updates while implementation continues.
- Only pause for user input when blocked by missing requirements, conflicting user changes, required approval/escalation, or an unsafe/destructive action.
- Before closing the task, verify completed work as far as feasible and clearly call out any remaining blockers or unverified areas.
